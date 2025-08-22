using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace RemoteMvvmTool.Tests;

public class GrpcWebEndToEndTests
{
    static List<string> LoadDefaultRefs()
    {
        var list = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
        }
        return list;
    }

    static void RunCmd(string file, string args, string workDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        Console.WriteLine($"Running command: {file} {args}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = false };
        p.OutputDataReceived += (s, e) => { if (e.Data != null) { Console.WriteLine(e.Data); stdout.AppendLine(e.Data); } };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) { Console.Error.WriteLine(e.Data); stderr.AppendLine(e.Data); } };

        if (!p.Start())
            throw new Exception($"Failed to start process: {file}");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            throw new Exception($"{file} {args} failed with exit code {p.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
        }
    }

    static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static void RunProtoc(string protoDir, string protoFile, string outDir)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolsRoot = Path.Combine(home, ".nuget", "packages", "grpc.tools");
        var versionDir = Directory.GetDirectories(toolsRoot).OrderBy(p => p).Last();
        string osPart = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macosx" : "linux";
        string archPart = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
        bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var protoc = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "protoc.exe" : "protoc");
        var plugin = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "grpc_csharp_plugin.exe" : "grpc_csharp_plugin");
        var includeDir = Path.Combine(versionDir, "build", "native", "include");
        var psi = new ProcessStartInfo
        {
            FileName = protoc,
            Arguments = $"--csharp_out \"{outDir}\" --grpc_out \"{outDir}\" --plugin=protoc-gen-grpc=\"{plugin}\" -I\"{protoDir}\" -I\"{includeDir}\" \"{protoFile}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var msg = proc.StandardError.ReadToEnd();
            throw new Exception($"protoc failed: {msg}");
        }
    }

    static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Copy files
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        // Copy subdirectories recursively
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string subDirName = Path.GetFileName(subDir);
            string destSubDir = Path.Combine(destDir, subDirName);
            CopyDirectory(subDir, destSubDir);
        }
    }

    static void SetupTestProject(string testDir)
    {
        Console.WriteLine($"Setting up test project in: {testDir}");

        // Get the base directory where this test is located
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rootDir = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        
        // Create test project directory structure
        var testProjectDir = Path.Combine(testDir, "TestProject");
        Directory.CreateDirectory(testProjectDir);
        
        // Copy pre-built test files from the TestData directory (excluding the manual JS stub)
        var testDataDir = Path.Combine(rootDir, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd");
        if (!Directory.Exists(testDataDir))
            throw new DirectoryNotFoundException($"Test data directory not found at {testDataDir}");

        foreach (var file in Directory.GetFiles(testDataDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == "testviewmodelservice_pb.js") // Skip the manual JS stub, copied only if needed later
                continue;
            File.Copy(file, Path.Combine(testProjectDir, fileName), true);
        }

        // Copy node_modules from ThermalTest if available
        var thermalTestNodeModules = Path.Combine(rootDir, "test", "ThermalTest", "ViewModels", "tsProjectUpdated", "node_modules");
        var targetNodeModules = Path.Combine(testProjectDir, "node_modules");
        
        if (Directory.Exists(thermalTestNodeModules))
        {
            Console.WriteLine($"Copying node_modules from: {thermalTestNodeModules}");
            CopyDirectory(thermalTestNodeModules, targetNodeModules);
        }
        else
        {
            Console.WriteLine("Creating minimal node_modules structure");
            CreateMinimalNodeModules(targetNodeModules);
        }
    }

    static void CreateProtobufStub(string outDir)
    {
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rootDir = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        var testDataDir = Path.Combine(rootDir, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd");
        var sourceFile = Path.Combine(testDataDir, "testviewmodelservice_pb.js");
        File.Copy(sourceFile, Path.Combine(outDir, "testviewmodelservice_pb.js"), true);
    }

    static void CreateMinimalNodeModules(string nodeModulesDir)
    {
        Directory.CreateDirectory(nodeModulesDir);
        
        // Create minimal grpc-web module structure
        var grpcWebDir = Path.Combine(nodeModulesDir, "grpc-web");
        Directory.CreateDirectory(grpcWebDir);
        
        File.WriteAllText(Path.Combine(grpcWebDir, "index.d.ts"), @"
export class GrpcWebClientBase {
  constructor(options: any);
}

export interface ClientReadableStream<T> {
  on(type: string, handler: (...args: any[]) => void): void;
  cancel(): void;
}

export interface UnaryResponse<T> {
  getResponseMessage(): T | undefined;
}");

        File.WriteAllText(Path.Combine(grpcWebDir, "index.js"), @"
const grpc = {};
grpc.GrpcWebClientBase = class {
  constructor(options) {
    this.hostname_ = options.hostname;
  }
  
  unaryCall(method, request, metadata, methodInfo, callback) {
    setTimeout(() => callback(null, new Uint8Array()), 100);
  }
};

module.exports = grpc;");

        // Create minimal google-protobuf module
        var protobufDir = Path.Combine(nodeModulesDir, "google-protobuf");
        Directory.CreateDirectory(protobufDir);
        
        File.WriteAllText(Path.Combine(protobufDir, "google-protobuf.d.ts"), @"
export namespace google.protobuf {
  class Empty {}
}

export class Message {
  static deserializeBinary(bytes: Uint8Array): Message;
  serializeBinary(): Uint8Array;
}");

        File.WriteAllText(Path.Combine(protobufDir, "google-protobuf.js"), @"
const protobuf = {};
protobuf.Message = class {
  static deserializeBinary(bytes) { return new this(); }
  serializeBinary() { return new Uint8Array(); }
};
protobuf.Empty = class extends protobuf.Message {};

if (typeof global !== 'undefined') {
  global.google = { protobuf: { Empty: protobuf.Empty } };
}

module.exports = protobuf;");
    }

    static void PatchPackageJsonForProto(string testProjectDir, string protoFileName)
    {
        var pkgJsonPath = Path.Combine(testProjectDir, "package.json");
        if (!File.Exists(pkgJsonPath)) return;
        var jsonText = File.ReadAllText(pkgJsonPath);
        var root = JsonNode.Parse(jsonText)!.AsObject();

        // Ensure output dir exists
        Directory.CreateDirectory(Path.Combine(testProjectDir, "src", "generated"));

        var scripts = root["scripts"] as JsonObject ?? new JsonObject();
        root["scripts"] = scripts;

        // Build protoc script targeting our generated proto (relative to project root)
        var protoArg = Path.Combine("protos", protoFileName).Replace("\\", "/");
        string newProtoc = string.Join(" ", new[]
        {
            "protoc",
            "--plugin=protoc-gen-ts=\".\\node_modules\\.bin\\protoc-gen-ts.cmd\"",
            "--plugin=protoc-gen-grpc-web=\".\\node_modules\\protoc-gen-grpc-web\\bin\\protoc-gen-grpc-web.exe\"",
            "--js_out=import_style=commonjs,binary:./src/generated",
            "--grpc-web_out=import_style=commonjs,mode=grpcwebtext:./src/generated",
            "-Iprotos",
            "-Inode_modules/protoc/protoc/include",
            protoArg
        });

        scripts["protoc"] = newProtoc;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(pkgJsonPath, root.ToJsonString(options));
    }

    static void UpdateNodeTestToUseGenerated(string testProjectDir, string protoBaseName)
    {
        var nodeTestPath = Path.Combine(testProjectDir, "test-protoc.js");
        if (!File.Exists(nodeTestPath)) return;

        var clientClass = protoBaseName + "Client"; // e.g., TestViewModelServiceClient
        var serviceJs = $"./src/generated/{protoBaseName}_grpc_web_pb.js";
        var messagesJs = $"./src/generated/{protoBaseName}_pb.js";

        var js = $@"const process = require('process');

(async () => {{
  console.log('Starting gRPC-Web test with protoc-generated grpc-web client...');
  const svc = require('{serviceJs}');
  const pb = require('{messagesJs}');
  const empty = require('google-protobuf/google/protobuf/empty_pb.js');

  const port = process.argv[2] || '5000';
  const host = `http://localhost:${{port}}`;

  const client = new svc.{clientClass}(host, null, null);

  console.log('Calling GetState via grpc-web client...');
  await new Promise((resolve, reject) => {{
    client.getState(new empty.Empty(), {{ 'content-type': 'application/grpc-web+proto' }}, (err, resp) => {{
      if (err) return reject(err);
      try {{
        console.log('Got response from grpc-web client');
        const zoneList = resp.getZoneListList ? resp.getZoneListList() : [];
        const zones = zoneList.map((z, i) => ({{ zone: z.getZone ? z.getZone() : i, temperature: z.getTemperature ? z.getTemperature() : 0 }}));
        console.log('Zones:', zones);
        if (zones.length < 2) return reject(new Error('Expected at least 2 zones'));
        if (zones[0].temperature !== 42 || zones[1].temperature !== 43)
          return reject(new Error(`Unexpected temperatures: ${{zones[0].temperature}}, ${{zones[1].temperature}}`));
        resolve();
      }} catch (e) {{ reject(e); }}
    }});
  }});
  console.log('? grpc-web client test passed');
}})().catch(e => {{ console.error('Unhandled error:', e); process.exit(1); }});
";
        File.WriteAllText(nodeTestPath, js);
    }

    static void DumpWorkCopy(string sourceDir)
    {
        // Copy the entire temp TestProject to a persistent Work folder for inspection
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        var workDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "Work");
        if (Directory.Exists(workDir))
        {
            try { Directory.Delete(workDir, true); } catch { /* ignore */ }
        }
        Directory.CreateDirectory(workDir);
        CopyDirectory(sourceDir, workDir);
        Console.WriteLine($"Copied TestProject to: {workDir}");
    }


    [Fact]
    public async Task TypeScript_Client_Can_Retrieve_Collection_From_Server()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Setup the test project with all necessary files
            SetupTestProject(tempDir);
            var testProjectDir = Path.Combine(tempDir, "TestProject");


            // Generate the server code using our generators
            var refs = LoadDefaultRefs();
            var vmFile = Path.Combine(testProjectDir, "TestViewModel.cs");
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "TestObservablePropertyAttribute", "TestRelayCommandAttribute", refs, "ObservableObject");

            var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation);
            var protoDir = Path.Combine(testProjectDir, "protos");
            Directory.CreateDirectory(protoDir);
            var protoFile = Path.Combine(protoDir, name + "Service.proto");
            File.WriteAllText(protoFile, proto);

            // Prepare npm project: clean node_modules (to avoid minimal stubs), patch package.json, and install deps
            var nodeModulesDir = Path.Combine(testProjectDir, "node_modules");
            if (Directory.Exists(nodeModulesDir))
            {
                try { Directory.Delete(nodeModulesDir, true); } catch { /* ignore */ }
            }

            // Patch package.json protoc script to point to the generated proto and generate JS clients
            PatchPackageJsonForProto(testProjectDir, Path.GetFileName(protoFile));
            UpdateNodeTestToUseGenerated(testProjectDir, name + "Service");

            // Install npm dependencies and generate JavaScript protobuf files
            Console.WriteLine("Installing npm packages for gRPC-Web...");
            RunCmd(@"C:\\Program Files\\nodejs\\npm.cmd", "install", testProjectDir);

            Console.WriteLine("Generating JavaScript protobuf files with npm run protoc...");
            RunCmd(@"C:\\Program Files\\nodejs\\npm.cmd", "run protoc", testProjectDir);

            // Persist a copy of the generated TestProject for inspection
            DumpWorkCopy(testProjectDir);

            var grpcOut = Path.Combine(testProjectDir, "grpc");
            Directory.CreateDirectory(grpcOut);
            RunProtoc(protoDir, protoFile, grpcOut);

            var serverCode = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", "console");
            File.WriteAllText(Path.Combine(testProjectDir, name + "GrpcServiceImpl.cs"), serverCode);

            var rootTypes = props.Select(p => p.FullTypeSymbol!);
            var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", rootTypes, compilation);
            File.WriteAllText(Path.Combine(testProjectDir, "ProtoStateConverters.cs"), conv);

            var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "ObservableObject", "console", true);
            File.WriteAllText(Path.Combine(testProjectDir, name + ".Remote.g.cs"), partial);
            

            // Compile and run the server
            var sourceFiles = Directory.GetFiles(testProjectDir, "*.cs").Concat(Directory.GetFiles(grpcOut, "*.cs"));
            var trees = sourceFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));
            var references = refs.Select(r => MetadataReference.CreateFromFile(r));
            var compilation2 = CSharpCompilation.Create("ServerAsm", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var dllPath = Path.Combine(testProjectDir, "server.dll");
            var emitResult = compilation2.Emit(dllPath);
            Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));

            var asm = Assembly.LoadFile(dllPath);
            var vmType = asm.GetType("Generated.ViewModels.TestViewModel")!;
            var serverOptsType = asm.GetType("PeakSWC.Mvvm.Remote.ServerOptions")!;
            var serverOpts = Activator.CreateInstance(serverOptsType)!;
            int port = GetFreePort();
            serverOptsType.GetProperty("Port")!.SetValue(serverOpts, port);
            var vm = Activator.CreateInstance(vmType, new object[] { serverOpts })!;
            
            // Setup test data
            var zoneListProp = vmType.GetProperty("ZoneList")!;
            var list = (System.Collections.IList)zoneListProp.GetValue(vm)!;
            var tzType = asm.GetType("Generated.ViewModels.ThermalZoneComponentViewModel")!;
            var zoneEnum = asm.GetType("HP.Telemetry.Zone")!;
            var z0 = Activator.CreateInstance(tzType)!;
            tzType.GetProperty("Zone")!.SetValue(z0, Enum.Parse(zoneEnum, "CPUZ_0"));
            tzType.GetProperty("Temperature")!.SetValue(z0, 42);
            list.Add(z0);
            var z1 = Activator.CreateInstance(tzType)!;
            tzType.GetProperty("Zone")!.SetValue(z1, Enum.Parse(zoneEnum, "CPUZ_1"));
            tzType.GetProperty("Temperature")!.SetValue(z1, 43);
            list.Add(z1);

            // Wait for server to be ready
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync($"http://localhost:{port}");
                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                        break; // Server is responding
                }
                catch
                {
                    await Task.Delay(500);
                }
            }

            await Task.Delay(2000); // Additional delay for gRPC services

            // Manual verification: try a direct HTTP call to confirm server works
            using var testHttpClient = new HttpClient();
            var testResponse = await testHttpClient.PostAsync(
                $"http://localhost:{port}/test_protos.TestViewModelService/GetState",
                new ByteArrayContent([0,0,0,0,0])
            );
            
            if (testResponse.IsSuccessStatusCode)
            {
                var responseBytes = await testResponse.Content.ReadAsByteArrayAsync();
                Console.WriteLine($"Server responded with {responseBytes.Length} bytes");
                Console.WriteLine($"Response bytes: [{string.Join(", ", responseBytes.Take(20))}]");
            }

            // Run the Node test that uses protoc-generated grpc-web JS client
            var nodeTestFile = Path.Combine(testProjectDir, "test-protoc.js");
            if (File.Exists(nodeTestFile))
            {
                Console.WriteLine("Running Node test with grpc-web generated client...");
                RunCmd("node", $"test-protoc.js {port}", testProjectDir);
                Console.WriteLine("Node test completed successfully");
            }
            else
            {
                Console.WriteLine("Node test script missing; skipping Node protoc test.");
            }
            
            (vm as IDisposable)?.Dispose();
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
