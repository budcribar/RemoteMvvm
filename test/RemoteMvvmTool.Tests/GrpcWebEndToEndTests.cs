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
    [Fact]
    public async Task ThermalZoneViewModel_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using System.Collections.ObjectModel;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject 
                { 
                    public TestViewModel() 
                    {
                        ZoneList.Add(new ThermalZoneComponentViewModel 
                        { 
                            Zone = HP.Telemetry.Zone.CPUZ_0, 
                            Temperature = 42 
                        });
                        ZoneList.Add(new ThermalZoneComponentViewModel 
                        { 
                            Zone = HP.Telemetry.Zone.CPUZ_1, 
                            Temperature = 43 
                        });
                    }

                    [ObservableProperty] 
                    private ObservableCollection<ThermalZoneComponentViewModel> _zoneList = new();
                    
                    [ObservableProperty]
                    private string _status = "Ready";
                }

                public class ThermalZoneComponentViewModel 
                {
                    public HP.Telemetry.Zone Zone { get; set; }
                    public int Temperature { get; set; }
                }
            }

            namespace HP.Telemetry 
            {
                public enum Zone { CPUZ_0, CPUZ_1 }
            }
            """;

        // Expected data: Zone values (0,1) and Temperature values (42,43) - sorted
        var expectedDataValues = "0,1,42,43";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task SimpleStringProperty_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject 
                { 
                    public TestViewModel() 
                    {
                        Message = "44";
                        Counter = 42;
                        IsEnabled = true;
                    }

                    [ObservableProperty]
                    private string _message = "";
                    
                    [ObservableProperty]
                    private int _counter = 0;

                    [ObservableProperty]
                    private bool _isEnabled = false;

                    [RelayCommand]
                    private void Increment() => Counter++;
                }
            }
            """;

        // Expected data: Counter (42), number from Message string (44), bool as int (1 for true)
        var expectedDataValues = "1,42,44";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task TwoWayPrimitiveTypes_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using CommunityToolkit.Mvvm.ComponentModel;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject
                {
                    public TestViewModel()
                    {
                        Message = "123";
                        IsEnabled = true;
                        Counter = 9876543210;
                        PlayerLevel = 4000000000;
                        HasBonus = 3.14f;
                        BonusMultiplier = 6.28;
                    }

                    [ObservableProperty]
                    private string _message = "";

                    [ObservableProperty]
                    private bool _isEnabled = false;

                    [ObservableProperty]
                    private long _counter = 0;

                    [ObservableProperty]
                    private uint _playerLevel = 0;

                    [ObservableProperty]
                    private float _hasBonus = 0;

                    [ObservableProperty]
                    private double _bonusMultiplier = 0;
                }
            }
            """;

        var expectedDataValues = "1,3.14,6.28,123,4000000000,9876543210";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ServerOnlyPrimitiveTypes_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using CommunityToolkit.Mvvm.ComponentModel;

            namespace Generated.ViewModels
            {
                public enum Mode { Idle = 1, Done = 2 }

                public partial class TestViewModel : ObservableObject
                {
                    public TestViewModel()
                    {
                        Counter = (byte)1;
                        PlayerLevel = (ushort)4;
                        HasBonus = (sbyte)(-2);
                        BonusMultiplier = (Half)1.5;
                        IsEnabled = (nint)9;
                        Status = '8';
                        Message = new Guid("00000000-0000-0000-0000-000000000020");
                        CurrentStatus = Mode.Done;
                    }

                    [ObservableProperty]
                    private byte _counter;

                    [ObservableProperty]
                    private ushort _playerLevel;

                    [ObservableProperty]
                    private sbyte _hasBonus;

                    [ObservableProperty]
                    private Half _bonusMultiplier;

                    [ObservableProperty]
                    private nint _isEnabled;

                    [ObservableProperty]
                    private char _status;

                    [ObservableProperty]
                    private Guid _message = Guid.Empty;

                    [ObservableProperty]
                    private Mode _currentStatus = Mode.Idle;
                }
            }
            """;

        var expectedDataValues = "-2,0,0,0,0,1,1.5,2,4,8,9,20";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task DictionaryWithEnum_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using System.Collections.Generic;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject 
                { 
                    public TestViewModel() 
                    {
                        StatusMap = new Dictionary<Status, string>
                        {
                            { Status.Active, "System Running" },
                            { Status.Idle, "System Idle" },
                            { Status.Error, "System Error" }
                        };
                        CurrentStatus = Status.Active;
                    }

                    [ObservableProperty]
                    private Dictionary<Status, string> _statusMap = new();
                    
                    [ObservableProperty]
                    private Status _currentStatus = Status.Active;
                }

                public enum Status
                {
                    Active = 1,
                    Idle = 2, 
                    Error = 3
                }
            }
            """;

        // Expected data: Enum values (1,2,3) and CurrentStatus (1) - sorted would be 1,1,2,3
        var expectedDataValues = "1,1,2,3";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ComplexDataTypes_EndToEnd_Test()
    {
        var modelCode = """
            using System;
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject 
                { 
                    public TestViewModel() 
                    {
                        ScoreList.Add(100);
                        ScoreList.Add(200);
                        ScoreList.Add(300);
                        PlayerLevel = 15;
                        HasBonus = false;
                        BonusMultiplier = 2.5; // Will be converted to 2.5 as a double
                        Status = GameStatus.Playing;
                    }

                    [ObservableProperty]
                    private ObservableCollection<int> _scoreList = new();
                    
                    [ObservableProperty]
                    private int _playerLevel = 1;

                    [ObservableProperty]
                    private bool _hasBonus = false;

                    [ObservableProperty]
                    private double _bonusMultiplier = 1.0;

                    [ObservableProperty]
                    private GameStatus _status = GameStatus.Menu;
                }

                public enum GameStatus
                {
                    Menu = 10,
                    Playing = 20, 
                    Paused = 30,
                    GameOver = 40
                }
            }
            """;
        // Expected data: ScoreList (100,200,300), PlayerLevel (15), HasBonus (0 for false), 
        // BonusMultiplier (2.5), GameStatus.Playing (20 twice - status and gameStatus) - all sorted
        var expectedDataValues = "0,2.5,15,20,20,100,200,300";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    /// <summary>
    /// Helper method that runs a complete end-to-end test scenario with a TypeScript/JavaScript client.
    /// This tests the entire pipeline: C# model → protobuf generation → gRPC server → JavaScript client → data validation
    /// The validation extracts numeric data from the transferred JavaScript object and compares it against expected values.
    /// </summary>
    /// <param name="modelCode">Complete C# code for the ViewModel and supporting types using raw string literals</param>
    /// <param name="expectedDataValues">Comma-separated string of expected numeric values from the transferred data, sorted</param>
    public static async Task TestEndToEndScenario(string modelCode, string expectedDataValues)
    {
        // Kill any existing TestProject processes from previous test runs
        KillExistingTestProcesses();
        
        // Setup paths
        var paths = SetupTestPaths();
        bool testPassed = false;

        try
        {
            // Setup work directory with the provided model
            SetupWorkDirectoryWithModel(paths.WorkDir, paths.SourceProjectDir, paths.TestProjectDir, modelCode);
            
            // Analyze ViewModel and generate server code
            var (name, props, cmds) = await AnalyzeViewModelAndGenerateCode(paths.TestProjectDir);
            
            // Generate and run JavaScript protobuf generation if needed
            await GenerateJavaScriptProtobufIfNeeded(paths.TestProjectDir);
            
            // Build the .NET project
            BuildProject(paths.TestProjectDir);
            
            // Run the end-to-end test with data validation
            await RunEndToEndTest(paths.TestProjectDir, expectedDataValues);
            
            testPassed = true;
            Console.WriteLine("🎉 End-to-end test passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ End-to-end test failed: {ex.Message}");
            Console.WriteLine($"📁 Debug files preserved in: {paths.WorkDir}");
            throw;
        }
        finally
        {
            CleanupTestResources(paths.WorkDir, testPassed);
        }
    }

    private static void SetupWorkDirectoryWithModel(string workDir, string sourceProjectDir, string testProjectDir, string modelCode)
    {
        // Clean and setup work directory
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, true);
        }
        Directory.CreateDirectory(workDir);
        
        Console.WriteLine($"Setting up test project in work directory: {testProjectDir}");
        
        if (!Directory.Exists(sourceProjectDir))
        {
            throw new DirectoryNotFoundException($"Source project directory not found: {sourceProjectDir}");
        }
        
        // Copy all files from source to work directory EXCEPT TestViewModel.cs (we'll replace it)
        CopyDirectoryExceptFile(sourceProjectDir, testProjectDir, "TestViewModel.cs");
        
        // Write our custom model code
        var vmFile = Path.Combine(testProjectDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, modelCode);
        
        Console.WriteLine("✅ Set up work directory with custom model");
    }

    private static void CopyDirectoryExceptFile(string sourceDir, string destDir, string excludeFile)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Copy files (excluding the specified file)
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (!string.Equals(fileName, excludeFile, StringComparison.OrdinalIgnoreCase))
            {
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }
        }

        // Copy subdirectories recursively
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string subDirName = Path.GetFileName(subDir);
            string destSubDir = Path.Combine(destDir, subDirName);
            CopyDirectoryExceptFile(subDir, destSubDir, excludeFile);
        }
    }

    static void RunCmd(string file, string args, string workDir, out string stdout, out string stderr)
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

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = false };
        p.OutputDataReceived += (s, e) => { if (e.Data != null) { Console.WriteLine(e.Data); stdoutBuilder.AppendLine(e.Data); } };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) { Console.Error.WriteLine(e.Data); stderrBuilder.AppendLine(e.Data); } };

        if (!p.Start())
            throw new Exception($"Failed to start process: {file}");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();

        stdout = stdoutBuilder.ToString();
        stderr = stderrBuilder.ToString();

        if (p.ExitCode != 0)
        {
            throw new Exception($"{file} {args} failed with exit code {p.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
        }
    }

    static void RunCmd(string file, string args, string workDir)
    {
        RunCmd(file, args, workDir, out _, out _);
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
                        Console.WriteLine($"✅ Process {process.Id} terminated");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Could not kill process {process.Id}: {ex.Message}");
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
            Console.WriteLine($"⚠️ Error checking for existing processes: {ex.Message}");
            // Don't fail the test for this - it's just cleanup
        }
    }

    [Fact]
    public async Task TypeScript_Client_Can_Retrieve_Collection_From_Server()
    {
        // Use the existing GrpcWebEndToEnd TestViewModel for this test
        var modelCode = File.ReadAllText(Path.Combine(SetupTestPaths().SourceProjectDir, "TestViewModel.cs"));
        
        // Expected data from the existing TestViewModel: Zone values (0,1) and Temperature values (42,43)
        var expectedDataValues = "0,1,42,43";
        
        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    private static (string WorkDir, string SourceProjectDir, string TestProjectDir) SetupTestPaths()
    {
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        var workDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "Work");
        var sourceProjectDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd");
        var testProjectDir = Path.Combine(workDir, "TestProject");
        
        return (workDir, sourceProjectDir, testProjectDir);
    }

    private static void SetupWorkDirectory(string workDir, string sourceProjectDir, string testProjectDir)
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
        Console.WriteLine("✅ Copied existing project to work directory");
    }

    private static async Task<(string Name, List<GrpcRemoteMvvmModelUtil.PropertyInfo> Props, List<GrpcRemoteMvvmModelUtil.CommandInfo> Cmds)> AnalyzeViewModelAndGenerateCode(string testProjectDir)
    {
        // Load .NET assemblies for analysis
        var refs = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }

        // Analyze the ViewModel
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

        // Generate all server code files
        GenerateServerCodeFiles(testProjectDir, name, props, cmds, compilation);
        
        return (name, props, cmds);
    }

    private static void GenerateServerCodeFiles(string testProjectDir, string name, List<GrpcRemoteMvvmModelUtil.PropertyInfo> props, List<GrpcRemoteMvvmModelUtil.CommandInfo> cmds, Compilation compilation)
    {
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

        Console.WriteLine("✅ Generated server code files");
    }

    private static async Task GenerateJavaScriptProtobufIfNeeded(string testProjectDir)
    {
        var jsTestFile = Path.Combine(testProjectDir, "test-protoc.js");
        if (!File.Exists(jsTestFile))
        {
            throw new Exception($"Node.js test file not found at: {jsTestFile}. This test requires a JavaScript client test to verify end-to-end functionality.");
        }

        Console.WriteLine("Found Node.js test file - generating JavaScript protobuf files...");
        
        // Install npm packages if needed
        var nodeModulesDir = Path.Combine(testProjectDir, "node_modules");
        if (!Directory.Exists(nodeModulesDir))
        {
            Console.WriteLine("Installing npm packages...");
            await InstallNpmPackages(testProjectDir);
        }
        else
        {
            Console.WriteLine("✅ Node.js packages already installed");
        }

        // Generate JavaScript files using npm script
        await RunNpmProtocScript(testProjectDir);
        
        // List generated files for verification
        ListGeneratedJavaScriptFiles(testProjectDir);
    }

    private static async Task RunNpmProtocScript(string testProjectDir)
    {
        Console.WriteLine("Running npm protoc script to generate JavaScript protobuf files...");
        var npmPaths = new []
        {
            @"C:\Program Files\nodejs\npm.cmd",
            "npm.cmd",
            "npm"
        };
        
        foreach (var npmPath in npmPaths)
        {
            try
            {
                RunCmd(npmPath, "run protoc", testProjectDir);
                Console.WriteLine("✅ JavaScript protobuf files generated successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to run protoc with {npmPath}: {ex.Message}");
            }
        }
        
        throw new Exception("Could not generate JavaScript protobuf files using npm script. Ensure Node.js is installed and npm is in PATH, or that package.json has a 'protoc' script defined.");
    }

    private static void ListGeneratedJavaScriptFiles(string testProjectDir)
    {
        var jsFiles = Directory.GetFiles(testProjectDir, "*_pb.js")
            .Concat(Directory.GetFiles(testProjectDir, "*_grpc_web_pb.js"))
            .ToArray();
            
        if (jsFiles.Length > 0)
        {
            Console.WriteLine($"✅ Found JavaScript files: {string.Join(", ", jsFiles.Select(Path.GetFileName))}");
        }
        else
        {
            throw new Exception("No JavaScript protobuf files were generated. The test requires generated JavaScript files to validate client-server communication.");
        }
    }

    private static void BuildProject(string testProjectDir)
    {
        Console.WriteLine("Building project...");
        try
        {
            RunCmd("dotnet", "build", testProjectDir);
            Console.WriteLine("✅ Project built successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Build failed: {ex.Message}");
            throw;
        }
    }

    private static async Task RunEndToEndTest(string testProjectDir, string expectedDataValues)
    {
        // Check if we have Node.js test files
        var jsTestFile = Path.Combine(testProjectDir, "test-protoc.js");
        var packageJsonFile = Path.Combine(testProjectDir, "package.json");
        
        if (!File.Exists(jsTestFile))
        {
            throw new Exception($"Node.js test file not found at: {jsTestFile}. This test requires a JavaScript client test to verify end-to-end functionality.");
        }
        
        if (!File.Exists(packageJsonFile))
        {
            throw new Exception($"package.json file not found at: {packageJsonFile}. This test requires Node.js package configuration for JavaScript client testing.");
        }
        
        Console.WriteLine("✅ Node.js test files found - proceeding with end-to-end test");

        // Get a free port and start the server
        int port = GetFreePort();
        Console.WriteLine($"Using port: {port}");

        var serverProcess = CreateServerProcess(testProjectDir, port);
        
        try
        {
            Console.WriteLine($"Starting server: dotnet run --no-build {port}");
            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            // Wait for server to be ready
            await WaitForServerReady(port);

            // Run tests - both are required to pass
            await TestServerEndpoint(port);
            await TestNodeJsClient(testProjectDir, port, expectedDataValues);
        }
        finally
        {
            // Stop the server
            StopServerProcess(serverProcess);
        }
    }

    private static async Task TestNodeJsClient(string testProjectDir, int port, string expectedDataValues)
    {
        Console.WriteLine("Testing Node.js client with data validation...");
        
        // Check if required JavaScript protobuf files exist
        var requiredFiles = new[]
        {
            "testviewmodelservice_pb.js",
            "testviewmodelservice_grpc_web_pb.js",
            "TestViewModelService_pb.js", 
            "TestViewModelService_grpc_web_pb.js"
        };
        
        var existingFiles = requiredFiles.Where(f => File.Exists(Path.Combine(testProjectDir, f))).ToArray();
        
        if (existingFiles.Length < 2)
        {
            var foundFilesList = Directory.GetFiles(testProjectDir, "*.js").Select(Path.GetFileName).ToArray();
            throw new Exception($"Missing required JavaScript protobuf files for Node.js test. " +
                               $"Required files (at least 2): {string.Join(", ", requiredFiles)}. " +
                               $"Found .js files: {string.Join(", ", foundFilesList)}. " +
                               $"Ensure JavaScript protobuf generation completed successfully.");
        }
        
        Console.WriteLine($"✅ Found required files: {string.Join(", ", existingFiles)}");
        
        // Try different node executable locations
        var nodePaths = new[]
        {
            @"C:\Program Files\nodejs\node.exe",
            "node.exe", 
            "node"
        };
        
        bool testSuccess = false;
        string? actualOutput = null;
        string? lastError = null;
        
        foreach (var nodePath in nodePaths)
        {
            try
            {
                Console.WriteLine($"Running Node.js test with: {nodePath}");
                RunCmd(nodePath, $"test-protoc.js {port}", testProjectDir, out var stdout, out var stderr);
                
                actualOutput = stdout;
                
                // Check if the output contains the "Test passed" indicator
                if (stdout.Contains("Test passed") || stdout.Contains("✅ Test passed"))
                {
                    // Extract and validate the transferred data
                    var actualDataValues = ExtractNumericDataFromOutput(stdout);
                    
                    if (ValidateDataValues(actualDataValues, expectedDataValues))
                    {
                        testSuccess = true;
                        Console.WriteLine("✅ Node.js client test passed - data validation successful");
                        Console.WriteLine($"Expected data: [{expectedDataValues}], Actual data: [{actualDataValues}]");
                        break;
                    }
                    else
                    {
                        lastError = $"Node.js test passed but data validation failed. Expected: [{expectedDataValues}], Actual: [{actualDataValues}]";
                        Console.WriteLine($"⚠️ {lastError}");
                    }
                }
                else
                {
                    lastError = $"Node.js test ran but didn't find 'Test passed' message. Output: {stdout.Substring(0, Math.Min(500, stdout.Length))}";
                    Console.WriteLine($"⚠️ {lastError}");
                }
            }
            catch (Exception ex)
            {
                lastError = $"Failed to run Node.js test with {nodePath}: {ex.Message}";
                Console.WriteLine(lastError);
            }
        }
        
        if (!testSuccess)
        {
            var outputLength = actualOutput?.Length ?? 0;
            var truncatedOutput = outputLength > 500 ? actualOutput!.Substring(0, 500) : actualOutput ?? "";
            throw new Exception($"Node.js client test failed. {lastError ?? "No Node.js executable found or all attempts failed."} " +
                               $"Expected data values: [{expectedDataValues}]. " +
                               $"Actual output: [{truncatedOutput}...]");
        }
    }

    /// <summary>
    /// Extracts numeric values from the Node.js output by looking for the FLAT_DATA JSON line.
    /// Also converts booleans to 0/1 and handles both integers and doubles.
    /// Preserves duplicate values for validation.
    /// </summary>
    private static string ExtractNumericDataFromOutput(string output)
    {
        var numbers = new List<double>(); // Use List to preserve duplicates
        
        // Look for lines that might contain JSON data or numeric values
        var lines = output.Split('\n');
        bool foundFlatData = false;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and obvious log messages
            if (string.IsNullOrWhiteSpace(trimmedLine) || 
                trimmedLine.StartsWith("Starting gRPC-Web test") ||
                trimmedLine.StartsWith("npm ") ||
                trimmedLine.StartsWith("✅ Generated") ||
                trimmedLine.StartsWith("node:"))
            {
                continue;
            }
            
            // Look for the FLAT_DATA line which contains all our data in compact JSON format
            if (trimmedLine.StartsWith("FLAT_DATA:"))
            {
                // Extract the JSON part after "FLAT_DATA: "
                var jsonStart = trimmedLine.IndexOf("{");
                if (jsonStart >= 0)
                {
                    var jsonData = trimmedLine.Substring(jsonStart);
                    ExtractNumbersFromLine(jsonData, numbers);
                    foundFlatData = true;
                }
                break; // We found our data, no need to continue
            }
        }
        
        // Fallback: if no FLAT_DATA was found, try regular parsing (exclude structured markers)
        if (!foundFlatData)
        {
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip structured data markers and log messages
                if (string.IsNullOrWhiteSpace(trimmedLine) || 
                    trimmedLine.StartsWith("Starting gRPC-Web test") ||
                    trimmedLine.StartsWith("npm ") ||
                    trimmedLine.StartsWith("✅") ||
                    trimmedLine.StartsWith("node:") ||
                    trimmedLine.Contains("=== TestViewModel Data") || 
                    trimmedLine.StartsWith("RESPONSE_DATA:"))
                {
                    continue;
                }
                
                ExtractNumbersFromLine(trimmedLine, numbers);
            }
        }
        
        // Sort the numbers and return as comma-separated string
        var sortedNumbers = numbers.OrderBy(x => x).ToList();
        return string.Join(",", sortedNumbers.Select(n => n % 1 == 0 ? n.ToString("F0") : n.ToString("G")));
    }

    private static void ExtractNumbersFromLine(string line, List<double> numbers)
    {
        // Handle boolean values - convert to 0/1
        var processedLine = line
            .Replace("true", "1")
            .Replace("false", "0");
        
        // Look for numeric values in the line using various delimiters
        var delimiters = new char[] { ' ', ',', ':', '[', ']', '{', '}', '"', '=', '(', ')', ';', '\t' };
        var words = processedLine.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var cleanWord = word.Trim();
            
            // Try parsing as double (handles both integers and decimals)
            if (double.TryParse(cleanWord, out var number))
            {
                numbers.Add(number); // List preserves duplicates
            }
            else
            {
                // Try to extract numbers from within strings (like "44" or "2.5" from a longer string)
                var numericChars = new System.Text.StringBuilder();
                bool hasDecimalPoint = false;
                
                foreach (char c in cleanWord)
                {
                    if (char.IsDigit(c))
                    {
                        numericChars.Append(c);
                    }
                    else if (c == '.' && !hasDecimalPoint && numericChars.Length > 0)
                    {
                        // Include decimal point for potential double parsing
                        numericChars.Append(c);
                        hasDecimalPoint = true;
                    }
                    else if (numericChars.Length > 0)
                    {
                        // We hit a non-digit/non-decimal after collecting digits, parse what we have
                        var numberStr = numericChars.ToString();
                        if (double.TryParse(numberStr, out var extractedNumber))
                        {
                            numbers.Add(extractedNumber);
                        }
                        numericChars.Clear();
                        hasDecimalPoint = false;
                    }
                }
                
                // Don't forget to parse any remaining digits at the end
                if (numericChars.Length > 0)
                {
                    var numberStr = numericChars.ToString();
                    if (double.TryParse(numberStr, out var finalNumber))
                    {
                        numbers.Add(finalNumber);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that the actual data values match the expected values.
    /// Both strings should contain sorted, comma-separated numeric values (integers and doubles).
    /// </summary>
    private static bool ValidateDataValues(string actualValues, string expectedValues)
    {
        if (string.IsNullOrWhiteSpace(actualValues) && string.IsNullOrWhiteSpace(expectedValues))
            return true;
            
        if (string.IsNullOrWhiteSpace(actualValues) || string.IsNullOrWhiteSpace(expectedValues))
            return false;
            
        // Parse and sort both sets of values to ensure consistent comparison
        var actualNumbers = actualValues.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s.Trim())).OrderBy(x => x).ToArray();
            
        var expectedNumbers = expectedValues.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s.Trim())).OrderBy(x => x).ToArray();
        
        return actualNumbers.SequenceEqual(expectedNumbers);
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
                Console.WriteLine("✅ npm install completed successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with {npmPath}: {ex.Message}");
            }
        }
        
        throw new Exception("Could not find npm executable or npm install failed. Ensure Node.js is installed and npm is in PATH.");
    }

    private static Process CreateServerProcess(string testProjectDir, int port)
    {
        return new Process
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
    }

    private static async Task WaitForServerReady(int port)
    {
        Console.WriteLine("Waiting for server to start...");
        for (int i = 0; i < 30; i++)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(2);
                var response = await httpClient.GetAsync($"http://localhost:{port}");
                if (response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Server is responding");
                    return;
                }
            }
            catch
            {
                Console.WriteLine($"Server not ready yet, attempt {i + 1}/30...");
                await Task.Delay(1000);
            }
        }
        
        throw new Exception("Server failed to start within 30 seconds");
    }

    private static void StopServerProcess(Process serverProcess)
    {
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
            // Additional cleanup - ensure no TestProject processes are left running
            KillExistingTestProcesses();
        }
    }

    private static void CleanupTestResources(string workDir, bool testPassed)
    {
        if (testPassed)
        {
            Console.WriteLine("✅ Cleaning up work directory");
            try 
            { 
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, true); 
            } 
            catch (Exception ex)
            { 
                Console.WriteLine($"⚠️ Could not clean work directory: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"🔍 Work directory preserved for debugging: {workDir}");
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
            Console.WriteLine($"✅ Server responded with {responseBytes.Length} bytes");
        }
        else
        {
            Console.WriteLine($"⚠️ HTTP test: {response.StatusCode}");
        }
    }
}
