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

        // Add xhr2 dependency for XMLHttpRequest polyfill in Node.js (already in base package.json)
        var dependencies = root["dependencies"] as JsonObject ?? new JsonObject();
        if (!dependencies.ContainsKey("xhr2"))
        {
            dependencies["xhr2"] = "^0.2.1";
            root["dependencies"] = dependencies;
        }

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

// Polyfill XMLHttpRequest for Node.js environment
if (typeof global !== 'undefined' && typeof global.XMLHttpRequest === 'undefined') {{
  global.XMLHttpRequest = require('xhr2');
}}

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
        // Use Work directory directly instead of temp directory
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        var workDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "Work");
        
        var testProjectDir = Path.Combine(workDir, "TestProject");
        bool testPassed = false;

        try
        {
            // Only clean up existing Work directory at the start if we're confident
            // For debugging, we'll preserve the directory unless explicitly cleaning
            Console.WriteLine($"Working in directory: {workDir}");
            if (!Directory.Exists(workDir))
            {
                Directory.CreateDirectory(workDir);
            }

            // Setup the test project with all necessary files
            SetupTestProject(workDir);

            // Generate the server code using our generators
            var refs = LoadDefaultRefs();
            
            // Add CommunityToolkit.Mvvm reference if not already present
            var mvvmFound = refs.Any(r => r.Contains("CommunityToolkit.Mvvm"));
            Console.WriteLine($"CommunityToolkit.Mvvm reference found in existing refs: {mvvmFound}");
            
            if (!mvvmFound)
            {
                // Try to find CommunityToolkit.Mvvm in NuGet packages
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var nugetPackagesDir = Path.Combine(userProfile, ".nuget", "packages");
                var communityToolkitDir = Path.Combine(nugetPackagesDir, "communitytoolkit.mvvm");
                
                Console.WriteLine($"Looking for CommunityToolkit.Mvvm in: {communityToolkitDir}");
                Console.WriteLine($"Directory exists: {Directory.Exists(communityToolkitDir)}");
                
                if (Directory.Exists(communityToolkitDir))
                {
                    var versionDirs = Directory.GetDirectories(communityToolkitDir).OrderByDescending(d => d);
                    Console.WriteLine($"Found version directories: {string.Join(", ", versionDirs.Select(Path.GetFileName))}");
                    
                    foreach (var versionDir in versionDirs)
                    {
                        var libDir = Path.Combine(versionDir, "lib", "net6.0");
                        if (!Directory.Exists(libDir))
                        {
                            libDir = Path.Combine(versionDir, "lib", "netstandard2.0");
                        }
                        Console.WriteLine($"Checking lib directory: {libDir} (exists: {Directory.Exists(libDir)})");
                        
                        if (Directory.Exists(libDir))
                        {
                            var mvvmDllPath = Path.Combine(libDir, "CommunityToolkit.Mvvm.dll");
                            Console.WriteLine($"Checking for DLL: {mvvmDllPath} (exists: {File.Exists(mvvmDllPath)})");
                            
                            if (File.Exists(mvvmDllPath))
                            {
                                refs.Add(mvvmDllPath);
                                Console.WriteLine($"Added CommunityToolkit.Mvvm reference: {mvvmDllPath}");
                                break;
                            }
                        }
                    }
                }
            }
            
            // Also try to add the reference directly from the loaded assembly
            try
            {
                var mvvmAssemblyPath = typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject).Assembly.Location;
                Console.WriteLine($"CommunityToolkit.Mvvm loaded from: {mvvmAssemblyPath}");
                if (File.Exists(mvvmAssemblyPath) && !refs.Contains(mvvmAssemblyPath))
                {
                    refs.Add(mvvmAssemblyPath);
                    Console.WriteLine($"Added CommunityToolkit.Mvvm reference from loaded assembly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing CommunityToolkit.Mvvm assembly: {ex.Message}");
            }
            
            var vmFile = Path.Combine(testProjectDir, "TestViewModel.cs");
            
            Console.WriteLine($"TestViewModel file content:");
            Console.WriteLine(File.ReadAllText(vmFile));
            Console.WriteLine();
            
            Console.WriteLine($"References being used:");
            foreach (var refPath in refs.Take(5))
            {
                Console.WriteLine($"  - {Path.GetFileName(refPath)}");
            }
            if (refs.Count > 5) Console.WriteLine($"  ... and {refs.Count - 5} more");
            Console.WriteLine();
            
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", refs, "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");

            Console.WriteLine($"Compilation diagnostics:");
            var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
            foreach (var diag in diagnostics.Take(10))
            {
                Console.WriteLine($"  {diag.Severity}: {diag.Id} - {diag.GetMessage()}");
            }
            if (diagnostics.Length > 10) Console.WriteLine($"  ... and {diagnostics.Length - 10} more diagnostics");
            Console.WriteLine();

            // Debug information
            Console.WriteLine($"Found ViewModel: {name}");
            Console.WriteLine($"Found properties: {props.Count}");
            foreach (var prop in props)
            {
                Console.WriteLine($"  - Property: {prop.Name} ({prop.TypeString})");
            }
            Console.WriteLine($"Found commands: {cmds.Count}");
            foreach (var cmd in cmds)
            {
                Console.WriteLine($"  - Command: {cmd.MethodName}");
            }

            // Ensure we have at least one property to prevent malformed generation
            if (props.Count == 0)
            {
                throw new Exception($"No properties found in TestViewModel. The ViewModelAnalyzer may not be finding the ObservablePropertyAttribute correctly. " +
                                  $"TestViewModel file exists: {File.Exists(vmFile)}, " +
                                  $"CommunityToolkit.Mvvm reference added: {refs.Any(r => r.Contains("CommunityToolkit.Mvvm"))}");
            }

            var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation);
            var protoDir = Path.Combine(testProjectDir, "protos");
            Directory.CreateDirectory(protoDir);
            var protoFile = Path.Combine(protoDir, name + "Service.proto");
            File.WriteAllText(protoFile, proto);
            
            // Debug: Show the generated proto content
            Console.WriteLine("Generated proto file content:");
            Console.WriteLine(proto);

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

            var grpcOut = Path.Combine(testProjectDir, "grpc");
            Directory.CreateDirectory(grpcOut);
            RunProtoc(protoDir, protoFile, grpcOut);

            var serverCode = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", "console");
            Console.WriteLine("Generated server code preview (first 500 chars):");
            Console.WriteLine(serverCode.Substring(0, Math.Min(500, serverCode.Length)));
            File.WriteAllText(Path.Combine(testProjectDir, name + "GrpcServiceImpl.cs"), serverCode);

            var rootTypes = props.Select(p => p.FullTypeSymbol!);
            var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", rootTypes, compilation);
            Console.WriteLine("Generated converter code preview (first 500 chars):");
            Console.WriteLine(conv.Substring(0, Math.Min(500, conv.Length)));
            File.WriteAllText(Path.Combine(testProjectDir, "ProtoStateConverters.cs"), conv);

            var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "CommunityToolkit.Mvvm.ComponentModel.ObservableObject", "console", true);
            Console.WriteLine("Generated partial code preview (first 500 chars):");
            Console.WriteLine(partial.Substring(0, Math.Min(500, partial.Length)));
            File.WriteAllText(Path.Combine(testProjectDir, name + ".Remote.g.cs"), partial);

            // Compile and run the server
            var sourceFiles = Directory.GetFiles(testProjectDir, "*.cs").Concat(Directory.GetFiles(grpcOut, "*.cs"));
            
            // Create a stub file if TestViewModelStub.cs is needed
            var stubFile = Path.Combine(testProjectDir, "TestViewModelStub.cs");
            var stubContent = @"using CommunityToolkit.Mvvm.ComponentModel;
using Generated.ViewModels;

namespace Generated.ViewModels
{
    // Additional stub content for TestViewModel if needed
    public partial class TestViewModel
    {
        // Stub constructors and methods if needed for compilation
        public TestViewModel() { }
    }
}

// Dispatcher stub for non-WPF environments
namespace System.Windows.Threading 
{ 
    public class Dispatcher 
    { 
        public void Invoke(System.Action a) => a(); 
        public System.Threading.Tasks.Task InvokeAsync(System.Action a) 
        { 
            a(); 
            return System.Threading.Tasks.Task.CompletedTask; 
        } 
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher(); 
    } 
}";
            File.WriteAllText(stubFile, stubContent);
            
            var trees = sourceFiles.Concat(new[] { stubFile }).Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));
            var references = refs.Select(r => MetadataReference.CreateFromFile(r));
            var compilation2 = CSharpCompilation.Create("ServerAsm", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var dllPath = Path.Combine(testProjectDir, "server.dll");
            var emitResult = compilation2.Emit(dllPath);
            
            if (!emitResult.Success)
            {
                Console.WriteLine("Compilation errors:");
                foreach (var diag in emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.WriteLine($"  {diag}");
                }
            }
            
            Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));

            Console.WriteLine("Loading assembly and creating server...");
            var asm = Assembly.LoadFile(dllPath);
            Console.WriteLine($"Assembly loaded with types: {string.Join(", ", asm.GetTypes().Select(t => t.FullName))}");
            
            var vmType = asm.GetType("Generated.ViewModels.TestViewModel");
            if (vmType == null)
            {
                // Try without namespace
                vmType = asm.GetType("TestViewModel");
                if (vmType == null)
                {
                    Console.WriteLine($"Available types: {string.Join(", ", asm.GetTypes().Select(t => t.FullName))}");
                    throw new Exception("TestViewModel type not found in assembly");
                }
            }
            Console.WriteLine($"Found TestViewModel: {vmType.FullName}");
            
            var serverOptsType = asm.GetType("PeakSWC.Mvvm.Remote.ServerOptions");
            if (serverOptsType == null)
            {
                throw new Exception("ServerOptions type not found in assembly");
            }
            
            var serverOpts = Activator.CreateInstance(serverOptsType)!;
            int port = GetFreePort();
            serverOptsType.GetProperty("Port")!.SetValue(serverOpts, port);
            Console.WriteLine($"Using port: {port}");
            
            var vm = Activator.CreateInstance(vmType, new object[] { serverOpts })!;
            Console.WriteLine("Server instance created successfully");

            // Setup test data
            Console.WriteLine("Setting up test data...");
            var zoneListProp = vmType.GetProperty("ZoneList");
            if (zoneListProp == null)
            {
                Console.WriteLine($"Available properties: {string.Join(", ", vmType.GetProperties().Select(p => p.Name))}");
                throw new Exception("ZoneList property not found");
            }
            
            var list = (System.Collections.IList)zoneListProp.GetValue(vm)!;
            Console.WriteLine($"ZoneList type: {list.GetType()}");
            
            var tzType = asm.GetType("Generated.ViewModels.ThermalZoneComponentViewModel");
            if (tzType == null)
            {
                Console.WriteLine($"Available types: {string.Join(", ", asm.GetTypes().Select(t => t.FullName))}");
                throw new Exception("ThermalZoneComponentViewModel type not found");
            }
            
            var zoneEnum = asm.GetType("HP.Telemetry.Zone");
            if (zoneEnum == null)
            {
                Console.WriteLine($"Zone enum not found in assembly");
                throw new Exception("Zone enum not found");
            }
            
            var z0 = Activator.CreateInstance(tzType)!;
            tzType.GetProperty("Zone")!.SetValue(z0, Enum.Parse(zoneEnum, "CPUZ_0"));
            tzType.GetProperty("Temperature")!.SetValue(z0, 42);
            list.Add(z0);
            
            var z1 = Activator.CreateInstance(tzType)!;
            tzType.GetProperty("Zone")!.SetValue(z1, Enum.Parse(zoneEnum, "CPUZ_1"));
            tzType.GetProperty("Temperature")!.SetValue(z1, 43);
            list.Add(z1);
            
            Console.WriteLine($"Added {list.Count} zones to the collection");

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
            using var httpClient2 = new HttpClient();
            var httpResponse = await httpClient2.PostAsync(
                $"http://localhost:{port}/test_protos.TestViewModelService/GetState",
                new ByteArrayContent([0,0,0,0,0])
            );
            
            if (httpResponse.IsSuccessStatusCode)
            {
                var responseBytes = await httpResponse.Content.ReadAsByteArrayAsync();
                Console.WriteLine($"Server responded with {responseBytes.Length} bytes");
                Console.WriteLine($"Response bytes: [{string.Join(", ", responseBytes.Take(20))}]");
            }
            else
            {
                Console.WriteLine($"HTTP test failed: {httpResponse.StatusCode} - {httpResponse.ReasonPhrase}");
            }

            // Run the Node test that uses protoc-generated grpc-web JS client
            var jsTestFile = Path.Combine(testProjectDir, "test-protoc.js");
            if (File.Exists(jsTestFile))
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

            // If we reach this point, the test passed!
            testPassed = true;
            Console.WriteLine("?? Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Test failed with error: {ex.Message}");
            Console.WriteLine($"?? Debug files are preserved in: {workDir}");
            throw; // Re-throw to fail the test
        }
        finally
        {
            if (testPassed)
            {
                // Only clean up if the test actually passed
                Console.WriteLine("? Test passed - cleaning up Work directory for next run");
                try 
                { 
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true); 
                    }
                } 
                catch (Exception ex)
                { 
                    Console.WriteLine($"??  Warning: Could not clean Work directory: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"?? Test failed - Work directory preserved for debugging: {workDir}");
            }
        }
    }
}
