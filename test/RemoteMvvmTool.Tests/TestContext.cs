using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Reflection; // added for Assembly / AssemblyName
using RemoteMvvmTool.Tests;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Test context that manages server lifecycle and provides strongly-typed client access
/// </summary>
public class TestContext : IDisposable
{
    public ITestClient Client { get; private set; }
    private readonly Process _serverProcess;
    private readonly string _workDir;
    private bool _testPassed = false;

    private TestContext(ITestClient client, Process serverProcess, string workDir)
    {
        Client = client;
        _serverProcess = serverProcess;
        _workDir = workDir;
    }

    /// <summary>
    /// Mark the test as passed - this will allow cleanup of work directory
    /// </summary>
    public void MarkTestPassed() => _testPassed = true;

    /// <summary>
    /// Creates a new test context with server and strongly-typed client
    /// </summary>
    public static async Task<TestContext> CreateAsync(string modelCode, string platform = "wpf")
    {
        KillAnyRunningTestProjects();
        var paths = GrpcWpfEndToEndTests.SetupTestPaths(platform);
        try
        {
            GrpcWpfEndToEndTests.SetupWorkDirectoryWithModel(paths.WorkDir, paths.SourceProjectDir, paths.TestProjectDir, modelCode);
            var (name, props, cmds) = await GrpcWpfEndToEndTests.AnalyzeViewModelAndGenerateCode(paths.TestProjectDir, platform);
            await GrpcWpfEndToEndTests.BuildProject(paths.TestProjectDir);
            var port = GrpcWpfEndToEndTests.GetFreeWpfPort();
            var serverProcess = GrpcWpfEndToEndTests.CreateServerProcess(paths.TestProjectDir, port);
            Console.WriteLine($"Starting server: dotnet run --no-build {port}");
            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();
            await GrpcWpfEndToEndTests.WaitForServerReady(port);
            await GrpcWpfEndToEndTests.TestServerEndpoint(port);
            var client = new GeneratedTestClient(paths.TestProjectDir, port, name, platform);
            await client.InitializeAsync();
            return new TestContext(client, serverProcess, paths.WorkDir);
        }
        catch
        {
            GrpcWpfEndToEndTests.CleanupTestResources(paths.WorkDir, false);
            throw;
        }
    }

    private static void KillAnyRunningTestProjects()
    {
        try
        {
            var testProcesses = Process.GetProcessesByName("TestProject");
            if (testProcesses.Length > 0)
            {
                Console.WriteLine($"[DEBUG] Found {testProcesses.Length} leftover TestProject processes - cleaning up");
                foreach (var proc in testProcesses)
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Killing leftover process {proc.Id}: {proc.ProcessName}");
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(2000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Could not kill process {proc.Id}: {ex.Message}");
                        try { proc.Kill(); } catch { }
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error checking for leftover TestProject processes: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            Client?.Dispose();
            GrpcWpfEndToEndTests.StopServerProcess(_serverProcess);
            GrpcWpfEndToEndTests.CleanupTestResources(_workDir, _testPassed);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during test context cleanup: {ex.Message}");
        }
    }
}

/// <summary>
/// Interface for strongly-typed test clients
/// </summary>
public interface ITestClient : IDisposable
{
    Task<string> GetModelDataAsync();
    Task InitializeAsync();
    // New: structural snapshot (path=value per line) for equality-based verification
    Task<string> GetStructuralStateAsync();
    // Generic property update methods
    Task UpdatePropertyAsync(string propertyName, object value);
    Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value);
}

/// <summary>
/// Interface for indexed property updates (like ZoneList[1].Temperature)
/// </summary>
public interface IIndexedUpdater { Task UpdatePropertyAsync(string propertyName, object value); }

/// <summary>
/// Collectible load context for generated test clients (single-project harness) to avoid cross-test interference.
/// </summary>
internal sealed class GeneratedClientLoadContext : AssemblyLoadContext
{
    private readonly string _baseDir;
    public GeneratedClientLoadContext(string baseDir, string? name) : base(name: name, isCollectible: true)
    {
        _baseDir = baseDir;
    }
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            // Try resolve from baseDir first (isolation), else fall back to default context
            var candidate = Path.Combine(_baseDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate)) return LoadFromAssemblyPath(candidate);
        }
        catch { }
        return null; // default resolution
    }
}

/// <summary>
/// Implementation that wraps the generated test client (single-project harness) using a collectible ALC.
/// </summary>
public class GeneratedTestClient : ITestClient
{
    private readonly string _testProjectDir;
    private readonly int _port;
    private readonly string _viewModelName;
    private readonly string _platform;
    private dynamic _stronglyTypedClient = null!;
    private GeneratedClientLoadContext? _alc;
    private bool _disposed = false;

    public GeneratedTestClient(string testProjectDir, int port, string viewModelName, string platform)
    {
        _testProjectDir = testProjectDir;
        _port = port;
        _viewModelName = viewModelName;
        _platform = platform;
        _stronglyTypedClient = LoadGeneratedTestClient();
    }

    private dynamic LoadGeneratedTestClient()
    {
        try
        {
            var possible = new []{
                Path.Combine(_testProjectDir, "bin","Debug","net8.0","TestProject.dll"),
                Path.Combine(_testProjectDir, "bin","Debug","net8.0-windows","TestProject.dll"),
                Path.Combine(_testProjectDir, "obj","Debug","net8.0","TestProject.dll"),
                Path.Combine(_testProjectDir, "obj","Debug","net8.0-windows","TestProject.dll")
            };
            string? asmPath = possible.FirstOrDefault(File.Exists);
            if (asmPath == null)
            {
                Console.WriteLine("[GenClient] TestProject.dll not found");
                return null!;
            }

            // Create unique ALC name per platform + guid to improve isolation across parallel groups
            _alc = new GeneratedClientLoadContext(Path.GetDirectoryName(asmPath)!, $"GenClient_{_platform}_{Guid.NewGuid():N}");

            byte[] asmBytes = File.ReadAllBytes(asmPath);
            string pdbPath = Path.ChangeExtension(asmPath, ".pdb");
            byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;
            using var asmStream = new MemoryStream(asmBytes, writable:false);
            using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes, writable:false) : null;
            var asm = pdbStream == null ? _alc.LoadFromStream(asmStream) : _alc.LoadFromStream(asmStream, pdbStream);

            var type = asm.GetType($"Generated.TestClients.{_viewModelName}TestClient", throwOnError:false, ignoreCase:false);
            if (type == null)
            {
                Console.WriteLine("[GenClient] Generated client type missing");
                return null!;
            }
            var inst = Activator.CreateInstance(type, "localhost", _port);
            Console.WriteLine($"[GenClient] Loaded {type.FullName} in ALC '{_alc.Name}'");
            return inst!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenClient] Load error: {ex.Message}");
            return null!;
        }
    }

    public async Task InitializeAsync()
    {
        if (_stronglyTypedClient != null)
        {
            try { await _stronglyTypedClient.InitializeAsync(); } catch (Exception ex) { Console.WriteLine($"[GenClient] Initialize failed: {ex.Message}"); }
        }
        else
        {
            await Task.Delay(50);
        }
    }

    public async Task<string> GetModelDataAsync()
    {
        if (_stronglyTypedClient != null)
        {
            try { return await _stronglyTypedClient.GetModelDataAsync(); } catch (Exception ex) { Console.WriteLine($"[GenClient] GetModelData failed: {ex.Message}"); }
        }
        return string.Empty;
    }

    public async Task<string> GetStructuralStateAsync()
    {
        if (_stronglyTypedClient != null)
        {
            try
            {
                // Prefer dedicated method if generator added it
                var mi = _stronglyTypedClient.GetType().GetMethod("GetStructuralStateAsync");
                if (mi != null)
                {
                    var task = (Task<string>)mi.Invoke(_stronglyTypedClient, Array.Empty<object>())!;
                    return await task;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenClient] GetStructuralStateAsync reflection path failed: {ex.Message}");
            }
        }
        // Fallback to numeric digest if structural not available
        return await GetModelDataAsync();
    }

    public async Task UpdatePropertyAsync(string propertyName, object value)
    {
        if (_stronglyTypedClient != null)
        {
            try { await _stronglyTypedClient.UpdatePropertyAsync(propertyName, value); } catch (Exception ex) { Console.WriteLine($"[GenClient] UpdateProperty {propertyName} failed: {ex.Message}"); }
        }
    }

    public async Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value)
    {
        if (_stronglyTypedClient != null)
        {
            try { await _stronglyTypedClient.UpdateIndexedPropertyAsync(collectionName, index, propertyName, value); } catch (Exception ex) { Console.WriteLine($"[GenClient] UpdateIndexedProperty {collectionName}[{index}].{propertyName} failed: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try { if (_stronglyTypedClient is IDisposable d) d.Dispose(); } catch { }
        _stronglyTypedClient = null!;
        if (_alc != null)
        {
            try
            {
                _alc.Unload();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Console.WriteLine($"[GenClient] Unloaded ALC '{_alc.Name}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenClient] Unload warning: {ex.Message}");
            }
            _alc = null;
        }
    }
}

/// <summary>
/// Implementation for indexed property updates
/// </summary>
public class IndexedUpdater : IIndexedUpdater
{
    private readonly ITestClient _client; private readonly string _collection; private readonly int _index;
    public IndexedUpdater(ITestClient client, string collection, int index){ _client=client; _collection=collection; _index=index; }
    public Task UpdatePropertyAsync(string propertyName, object value) => _client.UpdateIndexedPropertyAsync(_collection, _index, propertyName, value);
}

/// <summary>
/// Helper for model verification
/// </summary>
public static class ModelVerifier
{
    public static async Task VerifyModelAsync(ITestClient client, string expectedData, string context)
    {
        var actual = await client.GetModelDataAsync();
        VerifyModelData(actual, expectedData, context);
        Console.WriteLine($"? {context}: Expected=[{expectedData}], Actual=[{actual}]");
    }

    public static async Task VerifyModelStructuralAsync(ITestClient client, string expectedNumericValues, string context)
    {
        var snapshot = await client.GetStructuralStateAsync();
        VerifyNumbersContained(snapshot, expectedNumericValues, context);
        Console.WriteLine($"? {context} (structural): Verified numbers [{expectedNumericValues}] present in structural snapshot");
    }

    public static async Task VerifyModelContainsAllDistinctAsync(ITestClient client, string expectedData, string context)
    {
        var actual = await client.GetModelDataAsync();
        VerifyModelDistinct(actual, expectedData, context);
        Console.WriteLine($"? {context} (distinct): ExpectedDistinctCount={expectedData.Split(',',StringSplitOptions.RemoveEmptyEntries).Distinct().Count()}, ActualDistinctCount={actual.Split(',',StringSplitOptions.RemoveEmptyEntries).Distinct().Count()}");
    }

    public static void VerifyModelData(string actualData, string expectedData, string context)
    {
        if (string.IsNullOrWhiteSpace(actualData) && string.IsNullOrWhiteSpace(expectedData)) return;
        if (string.IsNullOrWhiteSpace(actualData) || string.IsNullOrWhiteSpace(expectedData)) throw new Exception($"Model verification failed in {context}. Expected: [{expectedData}], Actual: [{actualData}]");
        var actualNumbers = actualData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s.Trim())).OrderBy(x=>x).ToArray();
        var expectedNumbers = expectedData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s.Trim())).OrderBy(x=>x).ToArray();
        if (!actualNumbers.SequenceEqual(expectedNumbers)) throw new Exception($"Model verification failed in {context}. Expected: [{expectedData}], Actual: [{actualData}]");
    }

    private static void VerifyNumbersContained(string structuralSnapshot, string expectedNumericValues, string context)
    {
        if (string.IsNullOrWhiteSpace(expectedNumericValues)) return;
        // Fast path exact substring checks first
        var raw = structuralSnapshot ?? string.Empty;
        // Extract all numeric tokens from structural snapshot (simple regex)
        var numberPattern = System.Text.RegularExpressions.Regex.Matches(raw, @"-?[0-9]+(?:\.[0-9]+)?");
        var actualNumberStrings = numberPattern.Select(m => m.Value).ToList();
        var actualNumbers = new List<double>();
        foreach (var s in actualNumberStrings)
        {
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                actualNumbers.Add(d);
        }

        bool ContainsTokenString(string token)
        {
            if (raw.Contains(token)) return true;
            if (!token.Contains('.') && raw.Contains(token+".0")) return true; // allow actual with trailing .0
            return false;
        }

        var expectedTokens = expectedNumericValues.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t=>t.Length>0).Distinct();
        var missing = new List<string>();
        foreach (var token in expectedTokens)
        {
            if (ContainsTokenString(token))
                continue;
            // Try tolerant numeric comparison
            if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expectedVal))
            {
                // Relative + absolute tolerance
                double relTol = Math.Max(Math.Abs(expectedVal) * 1e-9, 1e-9);
                double absTol = 1e-6; // baseline
                bool matched = actualNumbers.Any(a => Math.Abs(a - expectedVal) <= Math.Max(relTol, absTol));
                if (matched) continue;
            }
            missing.Add(token);
        }
        if (missing.Count > 0)
            throw new Exception($"Structural model verification failed in {context}. Missing numbers: {string.Join(",", missing)}\nSnapshot (truncated): {Truncate(raw,800)}");
    }

    private static void VerifyModelDistinct(string actualData, string expectedData, string context)
    {
        if (string.IsNullOrWhiteSpace(expectedData)) return; // nothing to verify
        if (string.IsNullOrWhiteSpace(actualData)) throw new Exception($"Model distinct verification failed in {context}. Actual empty");
        var actualSet = actualData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToHashSet();
        var expectedSet = expectedData.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToHashSet();
        var missing = expectedSet.Where(e=>!actualSet.Contains(e)).ToList();
        if (missing.Count>0)
            throw new Exception($"Model distinct verification failed in {context}. Missing: {string.Join(",", missing.Take(20))}{(missing.Count>20?"...":"")}");
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "...";
}