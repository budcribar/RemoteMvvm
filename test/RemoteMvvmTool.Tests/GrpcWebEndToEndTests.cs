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
using System.Runtime.CompilerServices;

namespace RemoteMvvmTool.Tests;

public class GrpcWebEndToEndTests
{
    /// <summary>
    /// Helper method to load model code from external files
    /// </summary>
    /// <param name="modelFileName">Name of the model file (without extension) in TestData/GrpcWebEndToEnd/Models/</param>
    /// <returns>The model code as a string</returns>
    private static string LoadModelCode(string modelFileName)
    {
        var paths = SetupTestPaths();
        var modelPath = Path.Combine(paths.SourceProjectDir, "Models", $"{modelFileName}.cs");
        
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        }
        
        return File.ReadAllText(modelPath);
    }

    [Fact]
    public async Task ThermalZoneViewModel_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ThermalZoneViewModel");

        // Expected data: Zone values (0,1) and Temperature values (42,43) - sorted
        var expectedDataValues = "0,1,42,43";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task NestedPropertyChange_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("NestedPropertyChangeModel");

        await TestEndToEndScenario(modelCode, "1,55", "test-simple-update.js", "Temperature=55");
        await TestEndToEndScenario(modelCode, "0,1,55", "test-nested-update.js", "ZoneList[0].Temperature=55");
    }

    [Fact]
    public async Task SimpleStringProperty_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("SimpleStringPropertyModel");

        // Expected data: Counter (42), number from Message string (44), bool as int (1 for true)
        var expectedDataValues = "1,42,44";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task SubscribeToPropertyChanges_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("SubscribeToPropertyChangesModel");

        await TestEndToEndScenario(modelCode, "", "test-subscribe.js", "Status=Updated");
    }

    [Fact]
    public async Task TwoWayPrimitiveTypes_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("TwoWayPrimitiveTypesModel");

        var expectedDataValues = "1,3.140000104904175,6.28,123,4000000000,9876543210";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ServerOnlyPrimitiveTypes_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ServerOnlyPrimitiveTypesModel");

        var expectedDataValues = "-2,1,1.5,2,4,8,9,20";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task DictionaryWithEnum_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("DictionaryWithEnumModel");

        // Expected data: Enum keys (1,2,3), CurrentStatus (1), and string values (4,5,6) - sorted would be 1,1,2,3,4,5,6
        var expectedDataValues = "1,1,2,3,4,5,6";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ComplexDataTypes_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ComplexDataTypesModel");

        // Expected data: ScoreList (100,200,300), PlayerLevel (15), HasBonus (0 for false), 
        // BonusMultiplier (2.5), GameStatus.Playing (20) - all sorted
        var expectedDataValues = "0,2.5,15,20,100,200,300";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ListOfDictionaries_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ListOfDictionariesModel");

        // Expected: totalRegions(3), isAnalysisComplete(1), all dict values (75,60,85,42,78,92,88,55) - sorted
        var expectedDataValues = "1,3,42,55,60,75,78,85,88,92";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task DictionaryOfLists_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("DictionaryOfListsModel");

        // Expected: categoryCount(3), maxScore(100), all list values (10.5,15.2,8.7,95.5,87.3,99.9) sorted
        var expectedDataValues = "3,8.7,10.5,15.2,87.3,95.5,99.9,100";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }


    [Fact]
    public async Task EdgeCasePrimitives_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("EdgeCasePrimitivesModel");

        // Expected: tinyValue(42), bigValue(18446744073709552000), negativeShort(-32768), positiveByte(255)
        // Also extracting: preciseValue(99999.99999) - decimal value is now being transmitted
        // Note: DateOnly/TimeOnly/Guid are server-only and transferred as strings, so we don't expect numeric extraction for those
        var expectedDataValues = "-32768,42,255,99999.99999,18446744073709552000";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task NestedCustomObjects_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("NestedCustomObjectsModel");

        // Expected: isActiveCompany(1), lastUpdate.nanos(0), lastUpdate.seconds(946684800),
        //           company.employeeCount(1500), dept headcounts(200,150,25), budgets(5000000.5,3000000.25,750000.75)
        var expectedDataValues = "0,1,25,150,200,1500,750000.75,3000000.25,5000000.5,946684800";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task EmptyCollectionsAndNullEdgeCases_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("EmptyCollectionsAndNullEdgeCasesModel");

        // Expected: nullableInt(42), singleItemList(999), singleItemDict value(1 for true), zeroValues(0,0,0), hasData(0 for false)
        var expectedDataValues = "0,0,0,0,1,42,999";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task MemoryAndByteArrayTypes_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("MemoryAndByteArrayTypesModel");

        // Expected: dataLength(10), isCompressed(0 for false), bytesList values(10,20,30)
        // Plus imageData bytes (255,128,64,32,16,8,4,2,1,0) and bufferData bytes (100,200,50,150)
        var expectedDataValues = "0,0,1,2,4,8,10,10,16,20,30,32,50,64,100,128,150,200,255";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task ExtremelyLargeCollections_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ExtremelyLargeCollectionsModel");

        // Expected: ALL numbers from the data transmission
        // LargeNumberList: 1,2,3,...,1000 (1000 numbers)
        // LargeStringDict: 0,10,20,...,990 (100 numbers)  
        // Plus summary values: CollectionCount(1000), DictionarySize(100), MaxValue(1000), MinValue(1)
        // Total: 1000 + 100 + 4 = 1104 numbers
        // This verifies complete data transmission without filtering
        var allNumbers = new List<int>();
        
        // Add LargeNumberList values (1 to 1000)
        allNumbers.AddRange(Enumerable.Range(1, 1000));
        
        // Add LargeStringDict values (0, 10, 20, ..., 990)
        allNumbers.AddRange(Enumerable.Range(0, 100).Select(i => i * 10));
        
        // Add summary property values
        allNumbers.AddRange(new[] { 1000, 100, 1000, 1 }); // CollectionCount, DictionarySize, MaxValue, MinValue
        
        // Sort and create expected string
        var expectedDataValues = string.Join(",", allNumbers.OrderBy(x => x));

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task MixedComplexTypesWithCommands_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("MixedComplexTypesWithCommandsModel");

        // Expected: gameState(1), totalSessions(42), player levels(15,23), scores(1500.5,2300.75), isActive(1,0), stat values, enum values(10,20)
        // Plus extracted GUID trailing digits (222) from the deterministic SessionId
        var expectedDataValues = "0,1,1,10,15,20,23,42,123.4,222,234.5,450.5,623.2,789.1,1500.5,2300.75";

        await TestEndToEndScenario(modelCode, expectedDataValues);
    }

    [Fact]
    public async Task UpdatePropertyValue_Response_Test()
    {
        var modelCode = LoadModelCode("UpdatePropertyTestModel");

        await TestEndToEndScenario(modelCode, "", "test-update-property.js", null);
    }

    [Fact]
    public async Task UpdatePropertyValue_Simple_Test()
    {
        var modelCode = LoadModelCode("UpdatePropertyTestModel");

        await TestEndToEndScenario(modelCode, "", "test-update-simple.js", null);
    }

    [Fact]
    public async Task UpdatePropertyValue_Add_Operation_Test()
    {
        var modelCode = LoadModelCode("AddOperationTestModel");

        await TestEndToEndScenario(modelCode, "", "test-add-operation.js", null);
    }

    [Fact]
    public async Task UpdatePropertyValue_PropertyChange_No_Streaming_Test()
    {
        var modelCode = LoadModelCode("PropertyChangeNoStreamingModel");

        await TestEndToEndScenario(modelCode, "", "test-property-change-no-streaming.js", null);
    }

    /// <summary>
    /// Helper method that runs a complete end-to-end test scenario with a TypeScript/JavaScript client.
    /// This tests the entire pipeline: C# model → protobuf generation → gRPC server → JavaScript client → data validation
    /// The validation extracts numeric data from the transferred JavaScript object and compares it against expected values.
    /// </summary>
    /// <param name="modelCode">Complete C# code for the ViewModel and supporting types using raw string literals</param>
    /// <param name="expectedDataValues">Comma-separated string of expected numeric values from the transferred data, sorted</param>
    public static async Task TestEndToEndScenario(string modelCode, string expectedDataValues, string nodeTestFile = "test-protoc.js", string? expectedPropertyChange = null)
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
            await RunEndToEndTest(paths.TestProjectDir, expectedDataValues, nodeTestFile, expectedPropertyChange);
            
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

        // Copy subdirectories recursively, but EXCLUDE the Models directory
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string subDirName = Path.GetFileName(subDir);
            // Skip the Models directory to avoid conflicts
            if (!string.Equals(subDirName, "Models", StringComparison.OrdinalIgnoreCase))
            {
                string destSubDir = Path.Combine(destDir, subDirName);
                CopyDirectoryExceptFile(subDir, destSubDir, excludeFile);
            }
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
        
        var serverCode = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", "wpf");
        File.WriteAllText(Path.Combine(testProjectDir, name + "GrpcServiceImpl.cs"), serverCode);

        var rootTypes = props.Select(p => p.FullTypeSymbol!);
        var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", rootTypes, compilation);
        File.WriteAllText(Path.Combine(testProjectDir, "ProtoStateConverters.cs"), conv);

        var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "CommunityToolkit.Mvvm.ComponentModel.ObservableObject", "wpf", true);
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
                RunCmd(npmPath, "run protoc", testProjectDir);
                Console.WriteLine("✅ JavaScript protobuf files generated successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed with {npmPath}: {ex.Message}");
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

    private static async Task RunEndToEndTest(string testProjectDir, string expectedDataValues, string jsTestFileName, string? expectedPropertyChange)
    {
        // Check if we have Node.js test files
        var jsTestFile = Path.Combine(testProjectDir, jsTestFileName);
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
            await TestNodeJsClient(testProjectDir, port, expectedDataValues, jsTestFileName, expectedPropertyChange);
        }
        finally
        {
            // Stop the server
            StopServerProcess(serverProcess);
        }
    }

    private static async Task TestNodeJsClient(string testProjectDir, int port, string expectedDataValues, string jsTestFileName, string? expectedPropertyChange)
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
                               $"Ensure JavaScriptprotobuf generation completed successfully.");
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
                RunCmd(nodePath, $"{jsTestFileName} {port}", testProjectDir, out var stdout, out var stderr);

                actualOutput = stdout;

                if (stdout.Contains("Test passed") || stdout.Contains("✅ Test passed"))
                {
                    bool dataValid = true;
                    if (!string.IsNullOrWhiteSpace(expectedDataValues))
                    {
                        var actualDataValues = ExtractNumericDataFromOutput(stdout);
                        dataValid = ValidateDataValues(actualDataValues, expectedDataValues);
                        if (!dataValid)
                        {
                            lastError = $"Node.js test passed but data validation failed. Expected: [{expectedDataValues}], Actual:[{actualDataValues}]";
                            Console.WriteLine($"⚠️ {lastError}");
                        }
                        else
                        {
                            Console.WriteLine("✅ Node.js client test passed - data validation successful");
                            Console.WriteLine($"Expected data: [{expectedDataValues}], Actual data: [{actualDataValues}]");
                        }
                    }

                    bool propertyValid = true;
                    if (!string.IsNullOrEmpty(expectedPropertyChange))
                    {
                        propertyValid = ValidatePropertyChange(stdout, expectedPropertyChange);
                        if (!propertyValid)
                        {
                            lastError = $"Property change validation failed. Expected: [{expectedPropertyChange}]";
                            Console.WriteLine($"⚠️ {lastError}");
                        }
                    }

                    if (dataValid && propertyValid)
                    {
                        testSuccess = true;
                        break;
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
    /// Extracts ALL numbers to verify complete data transmission.
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
                    // Extract ALL numbers from FLAT_DATA, including base64 decoded bytes
                    ExtractAllNumbersFromJson(jsonData, numbers);
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

    /// <summary>
    /// Extracts all numbers from JSON data by traversing properties and values.
    /// Handles base64 encoded byte arrays that end with "_asb64" suffix.
    /// </summary>
    private static void ExtractAllNumbersFromJson(string jsonData, List<double> numbers)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonData);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Handle base64 properties - extract the decoded bytes
                if (property.Name.EndsWith("_asb64") && property.Value.ValueKind == JsonValueKind.String)
                {
                    var base64Value = property.Value.GetString();
                    if (!string.IsNullOrEmpty(base64Value))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(base64Value);
                            foreach (var b in bytes)
                            {
                                numbers.Add(b); // Add each individual byte value
                            }
                        }
                        catch (FormatException)
                        {
                            // Invalid base64, ignore
                        }
                    }
                }
                else
                {
                    // Extract from property value for non-base64 properties
                    ExtractAllNumbersFromJsonValue(property.Value, numbers);
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to string parsing if JSON parsing fails
            ExtractNumbersFromLine(jsonData, numbers);
        }
    }

    private static void ExtractAllNumbersFromJsonValue(JsonElement element, List<double> numbers)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetDouble(out var numValue))
                    numbers.Add(numValue);
                break;
            case JsonValueKind.String:
                var strValue = element.GetString();
                if (!string.IsNullOrEmpty(strValue))
                {
                    if (IsLikelyNumericString(strValue))
                    {
                        if (double.TryParse(strValue, out var parsedNum))
                            numbers.Add(parsedNum);
                    }
                    else if (strValue.Length == 36 && strValue.Count(c => c == '-') == 4)
                    {
                        // GUID handling - extract meaningful trailing number
                        var lastDash = strValue.LastIndexOf('-');
                        if (lastDash >= 0)
                        {
                            var lastSegment = strValue.Substring(lastDash + 1);
                            // Convert hex to decimal if it's not all zeros
                            if (lastSegment != "000000000000" && !lastSegment.All(c => c == '0'))
                            {
                                // Remove leading zeros and try to parse as decimal
                                var trailingDigits = lastSegment.TrimStart('0');
                                if (!string.IsNullOrEmpty(trailingDigits) && double.TryParse(trailingDigits, out var guidNum))
                                    numbers.Add(guidNum);
                            }
                        }
                    }
                }
                break;
            case JsonValueKind.True:
                numbers.Add(1);
                break;
            case JsonValueKind.False:
                numbers.Add(0);
                break;
            case JsonValueKind.Array:
                // Extract ALL elements from arrays - no size limits
                foreach (var item in element.EnumerateArray())
                    ExtractAllNumbersFromJsonValue(item, numbers);
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    // Extract numeric keys from object property names (for dictionary keys)
                    if (double.TryParse(prop.Name, out var keyNum))
                    {
                        numbers.Add(keyNum);
                    }
                    else if (prop.Name.Contains('.'))
                    {
                        // Handle flattened property names like "statusmap.1", "statusmap.2"
                        var lastPart = prop.Name.Substring(prop.Name.LastIndexOf('.') + 1);
                        if (double.TryParse(lastPart, out var flattenedKeyNum))
                        {
                            numbers.Add(flattenedKeyNum);
                        }
                    }
                    
                    // Also extract from the property value
                    ExtractAllNumbersFromJsonValue(prop.Value, numbers);
                }
                break;
        }
    }

    private static bool IsLikelyNumericString(string value)
    {
        // Special case: if it's a GUID pattern ending with meaningful numbers, handle it in the GUID section
        if (value.Length == 36 && value.Count(c => c == '-') == 4)
        {
            return false; // Let the GUID handling section deal with this
        }
        
        // Don't extract numbers from other long strings with dashes
        if (value.Contains('-') && value.Length > 10)
            return false;
        
        if (value.All(c => c == '0')) return false; // All zeros, likely padding
        if (value.Length > 10) return false; // Too long to be a simple number
        
        // Only extract if it's a reasonable numeric string
        return value.All(c => char.IsDigit(c) || c == '.' || c == '-');
    }

    private static void ExtractNumbersFromLine(string line, List<double> numbers)
    {
        // Handle boolean values - convert to 0/1
        var processedLine = line
            .Replace("true", "1")
            .Replace("false", "0");
        
        // For JSON data, try to parse it properly first
        if (line.TrimStart().StartsWith("{") && line.TrimEnd().EndsWith("}"))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    ExtractAllNumbersFromJsonValue(property.Value, numbers);
                }
                return; // Successfully parsed as JSON, don't do string splitting
            }
            catch (JsonException)
            {
                // Fall through to string parsing if JSON parsing fails
            }
        }
        
        // Look for numeric values in the line using various delimiters
        var delimiters = new char[] { ' ', ',', ':', '[', ']', '{', '}', '"', '=', '(', ')', ';', '\t' };
        var words = processedLine.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var cleanWord = word.Trim();
            
            // Skip common non-numeric words that might contain digits
            if (IsNonNumericWord(cleanWord))
                continue;
            
            // Try parsing as double (handles both integers and decimals)
            if (double.TryParse(cleanWord, out var number))
            {
                numbers.Add(number); // List preserves duplicates
            }
            else
            {
                // Try to extract numbers from within strings (like "44" or "2.5" from a longer string)
                ExtractNumbersFromString(cleanWord, numbers);
            }
        }
    }

    private static bool IsNonNumericWord(string word)
    {
        // Skip words that are obviously non-numeric contexts
        var nonNumericWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "starting", "grpc-web", "test", "using", "generated", "client",
            "npm", "node", "data", "start", "end", "response", "flat",
            "status", "message", "counter", "enabled", "level", "bonus",
            "multiplier", "current", "game", "testviewmodel"
        };
        
        return nonNumericWords.Contains(word) || 
               word.Length > 15 || // Very long strings are unlikely to be numbers
               (word.Contains('-') && word.Length > 10); // Long strings with dashes (like GUIDs)
    }

    private static void ExtractNumbersFromString(string cleanWord, List<double> numbers)
    {
        // Only extract from strings that don't look like GUIDs or other structured data
        if (cleanWord.Length > 15 || cleanWord.Count(c => c == '-') > 2)
            return;
            
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
            else if (c == '-' && numericChars.Length == 0)
            {
                // Include negative sign at the start
                numericChars.Append(c);
            }
            else if (numericChars.Length > 0)
            {
                // We hit a non-digit/non-decimal after collecting digits, parse what we have
                var numberStr = numericChars.ToString();
                if (numberStr.Length > 0 && numberStr != "-" && double.TryParse(numberStr, out var extractedNumber))
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
            if (numberStr.Length > 0 && numberStr != "-" && double.TryParse(numberStr, out var finalNumber))
            {
                numbers.Add(finalNumber);
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

    private static bool ValidatePropertyChange(string output, string expected)
    {
        var line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("PROPERTY_CHANGE:"));
        if (line == null)
            return false;

        var actual = line.Substring("PROPERTY_CHANGE:".Length);
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
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
        var process = new Process
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

        // Capture server output for debugging
        process.OutputDataReceived += (sender, args) => 
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                Console.WriteLine($"[SERVER OUT] {args.Data}");
                // Also write to test output
                Debug.WriteLine($"[SERVER OUT] {args.Data}");
            }
        };
        
        process.ErrorDataReceived += (sender, args) => 
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                Console.WriteLine($"[SERVER ERR] {args.Data}");
                // Also write to test output  
                Debug.WriteLine($"[SERVER ERR] {args.Data}");
            }
        };

        return process;
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
            new ByteArrayContent([0, 0, 0, 0, 0])
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

    [Fact]
    public async Task EnumMappings_Generation_Test()
    {
        var modelCode = LoadModelCode("EnumMappingsModel");

        // Create TypeScript generator and analyze the model
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, modelCode);
        
        try
        {
            var refs = new List<string>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (tpa != null)
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
            }

            var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { tempFile }, 
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", 
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", 
                refs, 
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");

            // Generate TypeScript client code
            var tsCode = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);

            // Verify that enum mappings are generated
            Assert.Contains("export const StatusMap: Record<number, string> = {", tsCode);
            Assert.Contains("1: 'Active'", tsCode);
            Assert.Contains("2: 'Idle'", tsCode);
            Assert.Contains("3: 'Error'", tsCode);

            Assert.Contains("export const TaskPriorityMap: Record<number, string> = {", tsCode);
            Assert.Contains("10: 'Low'", tsCode);
            Assert.Contains("20: 'Medium'", tsCode);
            Assert.Contains("30: 'High'", tsCode);

            // Verify helper functions are generated
            Assert.Contains("export function getStatusDisplay(value: number): string {", tsCode);
            Assert.Contains("return StatusMap[value] || value.toString();", tsCode);
            Assert.Contains("export function getTaskPriorityDisplay(value: number): string {", tsCode);

            // Verify enum mappings appear before class definition
            var statusMapIndex = tsCode.IndexOf("export const StatusMap");
            var classIndex = tsCode.IndexOf($"export class {name}RemoteClient");
            Assert.True(statusMapIndex >= 0 && statusMapIndex < classIndex, "Enum mappings should appear before class definition");

            Console.WriteLine("✅ All enum mapping tests passed!");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ListByte_RoundTrip_EndToEnd_Test()
    {
        var modelCode = LoadModelCode("ListByteRoundTripModel");

        // Expected: byteCount(6), hasData(1 for true), maxByte(66), minByte(11), 
        // bytesList values(11,22,33,44,55,66), plus ReadOnlyBuffer bytes(77,88)
        // Note: minByte(11) and maxByte(66) will appear as duplicates from the array values
        var expectedDataValues = "1,6,11,11,22,33,44,55,66,66,77,88";

        await TestEndToEndScenario(modelCode, expectedDataValues);
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
}
