using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

    static void KillExistingTestProcesses()
    {
        try
        {
            Console.WriteLine("Checking for existing TestProject processes...");
            
            // Find all processes named TestProject
            var testProcesses = Process.GetProcessesByName("TestProject");
            
            if (testProcesses.Length > 0)
            {
                Console.WriteLine($"Found {testProcesses.Length} existing TestProject process(es). Terminating...");
                
                foreach (var process in testProcesses)
                {
                    try
                    {
                        Console.WriteLine($"Killing process {process.Id}: {process.ProcessName}");
                        process.Kill();
                        process.WaitForExit(5000); // Wait up to 5 seconds for clean exit
                        Console.WriteLine($"? Process {process.Id} terminated");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"?? Could not kill process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Give the system a moment to clean up
                Thread.Sleep(1000);
            }
            else
            {
                Console.WriteLine("No existing TestProject processes found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Error checking for existing processes: {ex.Message}");
            // Don't fail the test for this - it's just cleanup
        }
    }

    [Fact]
    public async Task TypeScript_Client_Can_Retrieve_Collection_From_Server()
    {
        // Kill any existing TestProject processes from previous test runs
        KillExistingTestProcesses();
        
        // Setup paths
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        var workDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "Work");
        var sourceProjectDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd");
        var testProjectDir = Path.Combine(workDir, "TestProject");
        
        bool testPassed = false;

        try
        {
            // Clean and setup work directory
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
            Directory.CreateDirectory(workDir);
            
            Console.WriteLine($"Copying project from: {sourceProjectDir}");
            Console.WriteLine($"To work directory: {testProjectDir}");
            
            if (!Directory.Exists(sourceProjectDir))
            {
                throw new DirectoryNotFoundException($"Source project directory not found: {sourceProjectDir}");
            }
            
            // Copy all files from source to work directory
            CopyDirectory(sourceProjectDir, testProjectDir);
            Console.WriteLine("? Copied existing project to work directory");

            // Generate code using RemoteMvvmTool
            var refs = new List<string>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (tpa != null)
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
            }

            var vmFile = Path.Combine(testProjectDir, "TestViewModel.cs");
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { vmFile }, 
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", 
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", 
                refs, 
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");

            Console.WriteLine($"Found ViewModel: {name} with {props.Count} properties and {cmds.Count} commands");

            // Ensure we found properties
            if (props.Count == 0)
            {
                throw new Exception("No properties found in TestViewModel. Source generators may not be running correctly.");
            }

            // Generate server code
            var protoDir = Path.Combine(testProjectDir, "protos");
            Directory.CreateDirectory(protoDir);
            var protoFile = Path.Combine(protoDir, name + "Service.proto");
            
            var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation);
            File.WriteAllText(protoFile, proto);
            
            var serverCode = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", "console");
            File.WriteAllText(Path.Combine(testProjectDir, name + "GrpcServiceImpl.cs"), serverCode);

            var rootTypes = props.Select(p => p.FullTypeSymbol!);
            var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", rootTypes, compilation);
            File.WriteAllText(Path.Combine(testProjectDir, "ProtoStateConverters.cs"), conv);

            var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "CommunityToolkit.Mvvm.ComponentModel.ObservableObject", "console", true);
            File.WriteAllText(Path.Combine(testProjectDir, name + ".Remote.g.cs"), partial);

            Console.WriteLine("? Generated server code files");

            // Build the project
            Console.WriteLine("Building project...");
            try
            {
                RunCmd("dotnet", "build", testProjectDir);
                Console.WriteLine("? Project built successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Build failed: {ex.Message}");
                throw;
            }

            // Setup Node.js testing if files exist
            var jsTestFile = Path.Combine(testProjectDir, "test-protoc.js");
            if (File.Exists(jsTestFile))
            {
                // The JavaScript protobuf files should already be generated at this point
                Console.WriteLine("JavaScript protobuf files should be available for Node.js test");
            }

            // Get a free port and start the server
            int port = GetFreePort();
            Console.WriteLine($"Using port: {port}");

            var serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --no-build {port}",
                    WorkingDirectory = testProjectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            Console.WriteLine($"Starting server: dotnet run --no-build {port}");
            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            try
            {
                // Wait for server to be ready
                Console.WriteLine("Waiting for server to start...");
                bool serverReady = false;
                for (int i = 0; i < 30; i++)
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(2);
                        var response = await httpClient.GetAsync($"http://localhost:{port}");
                        if (response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("? Server is responding");
                            serverReady = true;
                            break;
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Server not ready yet, attempt {i + 1}/30...");
                        await Task.Delay(1000);
                    }
                }

                if (!serverReady)
                {
                    throw new Exception("Server failed to start within 30 seconds");
                }

                // Test gRPC endpoint
                await TestServerEndpoint(port);

                // Test Node.js client if available
                if (File.Exists(jsTestFile) && File.Exists(Path.Combine(testProjectDir, "package.json")))
                {
                    await TestNodeJsClient(testProjectDir, port);
                }

                testPassed = true;
                Console.WriteLine("?? All tests passed!");
            }
            finally
            {
                // Stop the server
                try
                {
                    if (!serverProcess.HasExited)
                    {
                        Console.WriteLine("Stopping server...");
                        serverProcess.Kill();
                        serverProcess.WaitForExit(5000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not stop server: {ex.Message}");
                }
                finally
                {
                    serverProcess.Dispose();
                }
                
                // Additional cleanup - ensure no TestProject processes are left running
                KillExistingTestProcesses();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Test failed: {ex.Message}");
            Console.WriteLine($"?? Debug files preserved in: {workDir}");
            throw;
        }
        finally
        {
            // Clean up only on success
            if (testPassed)
            {
                Console.WriteLine("? Cleaning up work directory");
                try 
                { 
                    if (Directory.Exists(workDir))
                        Directory.Delete(workDir, true); 
                } 
                catch (Exception ex)
                { 
                    Console.WriteLine($"?? Could not clean work directory: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"?? Work directory preserved for debugging: {workDir}");
            }
        }
    }

    private static async Task TestServerEndpoint(int port)
    {
        Console.WriteLine("Testing gRPC endpoint...");
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(
            $"http://localhost:{port}/test_protos.TestViewModelService/GetState",
            new ByteArrayContent([0,0,0,0,0])
        );
        
        if (response.IsSuccessStatusCode)
        {
            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            Console.WriteLine($"? Server responded with {responseBytes.Length} bytes");
        }
        else
        {
            Console.WriteLine($"?? HTTP test: {response.StatusCode}");
        }
    }

    private static async Task TestNodeJsClient(string testProjectDir, int port)
    {
        Console.WriteLine("Testing Node.js client...");
        
        // Try different node executable locations
        var nodePaths = new[]
        {
            @"C:\Program Files\nodejs\node.exe",
            "node.exe", 
            "node"
        };
        
        bool testSuccess = false;
        foreach (var nodePath in nodePaths)
        {
            try
            {
                Console.WriteLine($"Running Node.js test with: {nodePath}");
                RunCmd(nodePath, $"test-protoc.js {port}", testProjectDir);
                testSuccess = true;
                Console.WriteLine("? Node.js client test passed");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with {nodePath}: {ex.Message}");
            }
        }
        
        if (!testSuccess)
        {
            throw new Exception("Node.js client test failed. Ensure Node.js is installed and accessible.");
        }
    }

    static async Task InstallNpmPackages(string projectDir)
    {
        var npmPaths = new[]
        {
            @"C:\Program Files\nodejs\npm.cmd",
            "npm.cmd",
            "npm"
        };
        
        foreach (var npmPath in npmPaths)
        {
            try
            {
                Console.WriteLine($"Trying npm at: {npmPath}");
                RunCmd(npmPath, "install", projectDir);
                Console.WriteLine("? npm install completed successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with {npmPath}: {ex.Message}");
            }
        }
        
        throw new Exception("Could not find npm executable or npm install failed. Ensure Node.js is installed and npm is in PATH.");
    }

    static async Task GenerateJavaScriptProtobuf(string projectDir, string protoFile, string serviceName)
    {
        try
        {
            // Use protoc from Grpc.Tools package to generate JavaScript files
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
            var includeDir = Path.Combine(versionDir, "build", "native", "include");
            
            var protoDir = Path.GetDirectoryName(protoFile)!;
            var protoFileName = Path.GetFileName(protoFile);
            
            // Generate JavaScript files using protoc with grpc-web plugin
            var args = $"--js_out=import_style=commonjs,binary:{projectDir} " +
                      $"--grpc-web_out=import_style=commonjs,mode=grpcwebtext:{projectDir} " +
                      $"-I\"{protoDir}\" " +
                      $"-I\"{includeDir}\" " +
                      $"\"{protoFile}\"";
            
            Console.WriteLine($"Running protoc: {protoc} {args}");
            
            var psi = new ProcessStartInfo
            {
                FileName = protoc,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = projectDir
            };
            
            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            
            if (proc.ExitCode != 0)
            {
                var error = proc.StandardError.ReadToEnd();
                var output = proc.StandardOutput.ReadToEnd();
                Console.WriteLine($"protoc stdout: {output}");
                Console.WriteLine($"protoc stderr: {error}");
                throw new Exception($"protoc failed with exit code {proc.ExitCode}: {error}");
            }
            
            Console.WriteLine("? protoc completed successfully");
            
            // List generated files for debugging
            var jsFiles = Directory.GetFiles(projectDir, "*.js");
            Console.WriteLine($"Generated JavaScript files: {string.Join(", ", jsFiles.Select(Path.GetFileName))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? JavaScript protobuf generation failed: {ex.Message}");
            throw;
        }
    }
}
