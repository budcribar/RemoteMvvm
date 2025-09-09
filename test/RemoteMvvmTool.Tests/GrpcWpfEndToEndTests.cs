using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using RemoteMvvmTool.Generators;
using Xunit;
using System.Text;
using System.Net.Http;

using ModelPropertyInfo = GrpcRemoteMvvmModelUtil.PropertyInfo;
using ModelCommandInfo = GrpcRemoteMvvmModelUtil.CommandInfo;

namespace RemoteMvvmTool.Tests
{
    /// <summary>
    /// End-to-end tests for WPF clients that replicate the functionality of GrpcWebEndToEndTests.
    /// These tests verify that WPF clients can connect to gRPC servers and properly receive/synchronize data.
    /// </summary>
    [Collection("GuiSequential")]
    public class GrpcWpfEndToEndTests
    {
        // ===== SERVER UI GENERATION FLAGS =====
        /// <summary>
        /// Controls whether server GUI is generated for WPF end-to-end tests.
        /// Set to false to generate console-only servers for faster test execution.
        /// </summary>
        private static readonly bool GenerateServerUI = true;
        
        /// <summary>
        /// Default server UI platform when GenerateServerUI is true.
        /// Can be "wpf" or "winforms". When "wpf" is used for client platform, 
        /// this determines what UI framework the server uses.
        /// </summary>
        private static readonly string DefaultServerUIPlatform = "wpf";

        public GrpcWpfEndToEndTests()
        {
            FailIfNamedGuiProcessesRunning();
        }

        private static void FailIfNamedGuiProcessesRunning()
        {
            var names = new[] { "ServerApp", "GuiClientApp" };
            foreach (var name in names)
            {
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine($"[GrpcWpfEndToEndTests] Found {processes.Length} leftover {name} process(es). Attempting cleanup...");
                        
                        // Attempt to kill leftover processes
                        foreach (var process in processes)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    Console.WriteLine($"[GrpcWpfEndToEndTests] Killing leftover process: {name} (PID: {process.Id})");
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(2000);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[GrpcWpfEndToEndTests] Failed to kill {name}: {ex.Message}");
                            }
                            finally
                            {
                                try { process.Dispose(); } catch { }
                            }
                        }
                        
                        // Wait a moment for cleanup to complete
                        System.Threading.Thread.Sleep(500);
                        
                        // Double-check if any are still running
                        var remainingProcesses = Process.GetProcessesByName(name);
                        if (remainingProcesses.Length > 0)
                        {
                            foreach (var p in remainingProcesses) { try { p.Dispose(); } catch { } }
                            throw new InvalidOperationException($"Blocked: Unable to cleanup leftover process '{name}'. Please manually terminate it before running GUI tests.");
                        }
                        
                        Console.WriteLine($"[GrpcWpfEndToEndTests] Successfully cleaned up leftover {name} processes.");
                    }
                }
                catch (PlatformNotSupportedException) { }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception) { }
            }
        }

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

        private static bool IsRunningInCI()
        {
            return Environment.GetEnvironmentVariable("CI") != null ||
                    Environment.GetEnvironmentVariable("CONTINUOUS_INTEGRATION") != null ||
                    Environment.GetEnvironmentVariable("BUILD_NUMBER") != null ||
                    Environment.GetEnvironmentVariable("TF_BUILD") != null;
        }

        private static bool IsDisplayAvailable()
        {
            var displayVar = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrEmpty(displayVar)) return true;
            try
            {
                var app = new System.Windows.Application();
                app.Dispatcher.InvokeShutdown();
                app = null;
                return true;
            }
            catch { return false; }
        }

        //private static void SkipIfNoGui() => Skip.If(IsRunningInCI() || !IsDisplayAvailable(), "GUI test skipped (CI environment or no display).");
        private static void SkipIfNoGui() => Skip.If(false, "GUI test skipped (CI environment or no display).");

        [Fact]
        public void Test_Infrastructure_Validation()
        {
            var modelCode = LoadModelCode("ThermalZoneViewModel");
            Assert.NotNull(modelCode);
            Assert.Contains("TestViewModel", modelCode);
            var actualValues = "1,2,42,43";
            var expectedValues = "1,2,42,43";
            Assert.True(ValidateDataValues(actualValues, expectedValues));
            
            // Log server UI generation settings for this test run
            Console.WriteLine($"[WPF EndToEnd] GenerateServerUI: {GenerateServerUI}");
            Console.WriteLine($"[WPF EndToEnd] DefaultServerUIPlatform: {DefaultServerUIPlatform}");
        }

        [SkippableFact]
        public async Task ThermalZoneViewModel_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("ThermalZoneViewModel");
            var expectedDataValues = "1,2,42,43";
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, expectedDataValues, "Split ThermalZone initial");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task NestedPropertyChange_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("NestedPropertyChangeModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,1,2", "Split initial state");
            await ctx.Client.UpdateTemperatureAsync(55);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "1,2,55", "After Temperature=55 (split)");
            await ctx.Client.ZoneList(1).UpdateTemperatureAsync(54);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "1,54,55", "After ZoneList[1].Temperature=54 (split)");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task SimpleStringProperty_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("SimpleStringPropertyModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,42,44", "Split initial string state");
            await ctx.Client.UpdateMessageAsync("TestValue123");
            await ctx.Client.UpdateCounterAsync(100);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "1,100,123", "Split after updates");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task TwoWayPrimitiveTypes_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("TwoWayPrimitiveTypesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,3.140000104904175,6.28,123,4000000000,9876543210", "Split primitive initial");
            await ctx.Client.UpdateEnabledAsync(false);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "0,3.140000104904175,6.28,123,4000000000,9876543210", "Split after bool update");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task ServerOnlyPrimitiveTypes_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("ServerOnlyPrimitiveTypesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "-2,1,1.5,2,4,8,9,20", "Split server-only primitives");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task DictionaryWithEnum_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("DictionaryWithEnumModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,2,3,4,5,6,7", "Split enum dictionary initial");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task ComplexDataTypes_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("ComplexDataTypesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,2.5,15,20,100,200,300", "Split complex types initial");
            await ctx.Client.UpdatePlayerLevelAsync(25);
            await ctx.Client.UpdateHasBonusAsync(false);
            await ctx.Client.UpdateBonusMultiplierAsync(3.5);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "0,3.5,25,20,100,200,300", "Split complex after updates");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task ListOfDictionaries_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("ListOfDictionariesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,3,42,55,60,75,78,85,88,92", "Split list of dictionaries");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task DictionaryOfLists_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("DictionaryOfListsModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "3,8.7,10.5,15.2,87.3,95.5,99.9,100", "Split dictionary of lists");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task EdgeCasePrimitives_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("EdgeCasePrimitivesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "-32768,42,255,99999.99999,18446744073709552000", "Split edge primitives");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task NestedCustomObjects_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("NestedCustomObjectsModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,25,150,200,1500,750000.75,3000000.25,5000000.5,946684800", "Split nested custom objects");
            ctx.MarkTestPassed();
        }
        
        [SkippableFact]
        public async Task EmptyCollectionsAndNullEdgeCases_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("EmptyCollectionsAndNullEdgeCasesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,2,3,4,7,42,999", "Split empty/null edge cases");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task MemoryAndByteArrayTypes_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("MemoryAndByteArrayTypesModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,2,3,4,8,9,10,16,20,30,32,50,64,100,128,150,200,255", "Split memory/byte arrays");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task MixedComplexTypesWithCommands_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("MixedComplexTypesWithCommandsModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelStructuralAsync(ctx.Client, "1,2,10,15,20,42,123.4,222,234.5,450.5,623.2,789.1,1500.5", "Split mixed complex types initial");
            await ctx.Client.UpdatePlayerLevelAsync(25);
            await ctx.Client.UpdateEnabledAsync(false);
            var updatedData = await ctx.Client.GetModelDataAsync();
            Console.WriteLine($"✅ Mixed complex types with commands test completed. Updated numeric digest: [{updatedData}]");
        }

        [SkippableFact]
        public async Task UpdatePropertyValue_Response_Wpf_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("UpdatePropertyTestModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ModelVerifier.VerifyModelAsync(ctx.Client, "0,100", "Initial state for update property test");
            await ctx.Client.UpdateCounterAsync(42);  // Update Counter from 100 to 42
            await ctx.Client.UpdateMessageAsync("Updated Message");  // Update Message property
            await ModelVerifier.VerifyModelAsync(ctx.Client, "0,42", "After property updates");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task UpdatePropertyValue_Simple_Wpf_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("UpdatePropertyTestModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ctx.Client.UpdateCounterAsync(55);  // Use Counter instead of Temperature
            var data = await ctx.Client.GetModelDataAsync();
            Console.WriteLine($"✅ Simple property update test completed. Data after Counter=55: [{data}]");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task UpdatePropertyValue_Add_Operation_Wpf_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("AddOperationTestModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ctx.Client.AddToZoneListAsync(new { Temperature = 99, Zone = 3 });
            var finalData = await ctx.Client.GetModelDataAsync();
            Console.WriteLine($"✅ Add operation test completed. Final data: [{finalData}]");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task UpdatePropertyValue_PropertyChange_No_Streaming_Wpf_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("PropertyChangeNoStreamingModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            await ctx.Client.UpdateTemperatureAsync(77);
            var finalData = await ctx.Client.GetModelDataAsync();
            Console.WriteLine($"✅ Property change no-streaming test completed. Final data: [{finalData}]");
            ctx.MarkTestPassed();
        }

        [SkippableFact]
        public async Task ExtremelyLargeCollections_Wpf_EndToEnd_Test()
        {
            SkipIfNoGui();
            var modelCode = LoadModelCode("ExtremelyLargeCollectionsModel");
            using var ctx = await SplitTestContext.CreateAsync(modelCode, "wpf", GenerateServerUI);
            var allNumbers = new List<int>();
            allNumbers.AddRange(Enumerable.Range(1, 1000));
            allNumbers.AddRange(Enumerable.Range(0, 100).Select(i => i * 10));
            allNumbers.AddRange(new[] { 1000, 100, 1000, 1 });
            var expectedDataValues = string.Join(",", allNumbers.OrderBy(x => x));
            await ModelVerifier.VerifyModelContainsAllDistinctAsync(ctx.Client, expectedDataValues, "Split WPF extremely large collections");
            ctx.MarkTestPassed();
        }

        /// <summary>
        /// Helper method that runs a complete WPF end-to-end test scenario.
        /// This tests the entire pipeline: C# model → protobuf generation → gRPC server → WPF client → data validation
        /// </summary>
        /// <param name="modelCode">Complete C# code for the ViewModel and supporting types using raw string literals</param>
        /// <param name="expectedDataValues">Comma-separated string of expected numeric values from the transferred data, sorted</param>
        /// <param name="expectedPropertyChange">Expected property change for testing property updates via C# client</param>
        internal static async Task TestWpfEndToEndScenario(string modelCode, string expectedDataValues, string? csTestType = null, string? expectedPropertyChange = null)
        {
            await TestGuiEndToEndScenario(modelCode, expectedDataValues, csTestType, expectedPropertyChange, "wpf");
        }

        private static async Task TestGuiEndToEndScenario(string modelCode, string expectedDataValues, string? csTestType, string? expectedPropertyChange, string platform)
        {
            var paths = SetupTestPaths(platform);
            bool testPassed = false;
            try
            {
                SetupWorkDirectoryWithModel(paths.WorkDir, paths.SourceProjectDir, paths.TestProjectDir, modelCode);
                var (name, props, cmds) = await AnalyzeViewModelAndGenerateCode(paths.TestProjectDir, platform);
                await BuildProject(paths.TestProjectDir);
                var actualDataValues = await RunGuiEndToEndTest(paths.TestProjectDir, expectedDataValues, csTestType, expectedPropertyChange, platform);
                if (!string.IsNullOrWhiteSpace(expectedDataValues))
                {
                    var dataValid = ValidateDataValues(actualDataValues, expectedDataValues);
                    if (!dataValid)
                        throw new Exception($"Data validation failed. Expected: [{expectedDataValues}], Actual: [{actualDataValues}]");
                }
                testPassed = true;
            }
            finally { /* directory retained intentionally */ _ = testPassed; }
        }

        public static (string WorkDir, string SourceProjectDir, string TestProjectDir) SetupTestPaths(string platform = "")
        {
            var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            var workDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "Work") + platform;
            var sourceProjectDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd");
            var testProjectDir = Path.Combine(workDir, "TestProject");
            return (workDir, sourceProjectDir, testProjectDir);
        }

        public static void SetupWorkDirectoryWithModel(string workDir, string sourceProjectDir, string testProjectDir, string modelCode)
        {
            if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);
            CopyDirectoryExceptFiles(sourceProjectDir, testProjectDir, new[] { "TestViewModel.cs", "TestViewModelRemoteClient.cs" });
            File.WriteAllText(Path.Combine(testProjectDir, "TestViewModel.cs"), modelCode);
        }

        public static async Task<(string Name, List<ModelPropertyInfo> Props, List<ModelCommandInfo> Cmds)> AnalyzeViewModelAndGenerateCode(string testProjectDir, string platform)
        {
            var refs = new List<string>();
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
                foreach (var p in tpa.Split(Path.PathSeparator)) if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
            var vmFile = Path.Combine(testProjectDir, "TestViewModel.cs");
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { vmFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                refs,
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
            GenerateServerCodeFiles(testProjectDir, name, props, cmds, compilation, platform);
            return (name, props, cmds);
        }

        public static async Task BuildProject(string testProjectDir)
        {
            await RunCmdAsync("dotnet", "build", testProjectDir);
        }

        public static int GetFreeWpfPort()
        {
            for (int port = 6000; port < 7000; port++)
            {
                try { var l = new TcpListener(IPAddress.Loopback, port); l.Start(); l.Stop(); return port; }
                catch (SocketException) { continue; }
            }
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int p = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return p;
        }

        public static Process CreateServerProcess(string testProjectDir, int port)
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
            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"[SERVER OUT] {e.Data}"); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"[SERVER ERR] {e.Data}"); };
            return process;
        }

        public static void StopServerProcess(Process serverProcess)
        {
            try
            {
                if (!serverProcess.HasExited)
                {
                    try { serverProcess.Kill(entireProcessTree: true); }
                    catch { serverProcess.Kill(); }
                    serverProcess.WaitForExit(5000);
                }
            }
            catch { }
            finally { serverProcess.Dispose(); }
        }

        public static async Task WaitForServerReady(int port)
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    using var httpClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    var r = await httpClient.GetAsync($"https://localhost:{port}/status");
                    if (r.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(1000);
            }
            throw new Exception("Server failed to start in time");
        }

        public static async Task TestServerEndpoint(int port)
        {
            using var httpClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
            var req = new HttpRequestMessage(HttpMethod.Post, $"https://localhost:{port}/test_protos.TestViewModelService/GetState")
            { Content = new ByteArrayContent(new byte[] { 0, 0, 0, 0, 0 }) };
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc-web+proto");
            await httpClient.SendAsync(req);
        }

        private static bool ValidateDataValues(string actualValues, string expectedValues)
        {
            if (string.IsNullOrWhiteSpace(actualValues) && string.IsNullOrWhiteSpace(expectedValues)) return true;
            if (string.IsNullOrWhiteSpace(actualValues) || string.IsNullOrWhiteSpace(expectedValues)) return false;
            var actualNumbers = actualValues.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => double.Parse(s.Trim())).OrderBy(x => x).ToArray();
            var expectedNumbers = expectedValues.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => double.Parse(s.Trim())).OrderBy(x => x).ToArray();
            return actualNumbers.SequenceEqual(expectedNumbers);
        }

        private static void CopyDirectoryExceptFiles(string sourceDir, string destDir, string[] excludeFiles)
        {
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                if (!excludeFiles.Any(ex => string.Equals(fileName, ex, StringComparison.OrdinalIgnoreCase)) &&
                    !fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, Path.Combine(destDir, fileName), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var subDirName = Path.GetFileName(subDir);
                if (!string.Equals(subDirName, "Models", StringComparison.OrdinalIgnoreCase))
                    CopyDirectoryExceptFiles(subDir, Path.Combine(destDir, subDirName), excludeFiles);
            }
        }

        private static void GenerateServerCodeFiles(string testProjectDir, string name, List<ModelPropertyInfo> props, List<ModelCommandInfo> cmds, Compilation compilation, string platform)
        {
            var protoDir = Path.Combine(testProjectDir, "protos");
            Directory.CreateDirectory(protoDir);
            File.WriteAllText(Path.Combine(protoDir, name + "Service.proto"), ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation));
            File.WriteAllText(Path.Combine(testProjectDir, name + "GrpcServiceImpl.cs"), ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", platform));
            var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", props.Select(p => p.FullTypeSymbol!), compilation);
            File.WriteAllText(Path.Combine(testProjectDir, "ProtoStateConverters.cs"), conv);
            var clientCode = ClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.Clients");
            File.WriteAllText(Path.Combine(testProjectDir, name + "RemoteClient.cs"), clientCode);
            var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "CommunityToolkit.Mvvm.ComponentModel.ObservableObject", platform, true, props);
            File.WriteAllText(Path.Combine(testProjectDir, name + ".Remote.g.cs"), partial);
            var testClientCode = StronglyTypedTestClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
            File.WriteAllText(Path.Combine(testProjectDir, name + "TestClient.cs"), testClientCode);
            var programPath = Path.Combine(testProjectDir, "Program.cs");
            var programCs = CsProjectGenerator.GenerateProgramCs("TestProject", platform, "Test.Protos", name + "Service", "Generated.Clients", props, cmds);
            File.WriteAllText(programPath, programCs);
            var csprojPath = Path.Combine(testProjectDir, "TestProject.csproj");
            if (File.Exists(csprojPath))
            {
                try { File.WriteAllText(csprojPath, CsProjectGenerator.GenerateCsProj("TestProject", name + "Service", platform)); }
                catch { }
            }
            var propertiesDir = Path.Combine(testProjectDir, "Properties");
            Directory.CreateDirectory(propertiesDir);
            File.WriteAllText(Path.Combine(propertiesDir, "launchSettings.json"), CsProjectGenerator.GenerateLaunchSettings());
        }

        private static async Task<(string stdout, string stderr)> RunCmdAsync(string file, string args, string workDir)
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
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
            if (!p.Start()) throw new Exception($"Failed to start: {file}");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            if (p.ExitCode != 0) throw new Exception($"{file} {args} failed with exit code {p.ExitCode}.\nSTDOUT:\n{sbOut}\nSTDERR:\n{sbErr}");
            return (sbOut.ToString(), sbErr.ToString());
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<string> RunGuiEndToEndTest(string testProjectDir, string expectedDataValues, string? csTestType, string? expectedPropertyChange, string platform)
        {
            int port = GetFreeWpfPort();
            var serverProcess = CreateServerProcess(testProjectDir, port);
            try
            {
                serverProcess.Start();
                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();
                await WaitForServerReady(port);
                await TestServerEndpoint(port);
                if (platform.ToLower() == "wpf")
                {
                    using var wpfRunner = new WpfTestRunner(testProjectDir, port, expectedDataValues, platform);
                    return await wpfRunner.RunWpfTestAsync();
                }
                throw new ArgumentException($"Unsupported platform: {platform}");
            }
            finally { StopServerProcess(serverProcess); }
        }
    }
}