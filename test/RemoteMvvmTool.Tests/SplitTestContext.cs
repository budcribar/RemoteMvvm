using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using RemoteMvvmTool.Generators;
using System.Runtime.Loader;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Split project harness (ServerApp + GuiClientApp) for debugging server/client simultaneously.
/// Does NOT replace existing single-project tests; it is additive.
/// </summary>
public sealed class SplitTestContext : IDisposable
{
    public ITestClient Client => _client;
    private readonly ITestClient _client;
    private readonly Process _serverProcess;
    private readonly string _workDir;
    private readonly string _serverDir;
    private readonly string _clientDir;
    private bool _disposed;
    private bool _testPassed;

    private SplitTestContext(ITestClient client, Process serverProcess, string workDir, string serverDir, string clientDir)
    {
        _client = client;
        _serverProcess = serverProcess;
        _workDir = workDir;
        _serverDir = serverDir;
        _clientDir = clientDir;
    }

    public void MarkTestPassed() => _testPassed = true;

    public static async Task<SplitTestContext> CreateAsync(string modelCode, string platform = "wpf")
    {
        var paths = SetupSplitPaths(platform);
        PrepareDirectories(paths.WorkDir);
        // Copy base model source files (only model file is needed)
        File.WriteAllText(Path.Combine(paths.WorkDir, "TestViewModel.cs"), modelCode);

        // Analyze view model
        var refs = CollectTrustedPlatformAssemblies();
        var vmFile = Path.Combine(paths.WorkDir, "TestViewModel.cs");
        var (vmSymbol, vmName, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
            new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs,
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");

        // Generate split projects
        SplitProjectGenerator.Generate(paths.WorkDir, vmName, props, cmds, compilation, platform, "Test.Protos");

        // Build both projects
        await BuildProject(paths.ServerDir);
        await BuildProject(paths.ClientDir);

        // Start server
        var port = GetFreePort(6000, 6500);
        var serverProcess = StartServer(paths.ServerDir, port);
        await WaitForServerReadyHttps(port);
        await SmokeTestEndpoint(port, vmName);

        // Load strongly typed client from GuiClientApp
        var client = new SplitGeneratedClient(paths.ClientDir, port, vmName);
        await client.InitializeAsync();

        return new SplitTestContext(client, serverProcess, paths.WorkDir, paths.ServerDir, paths.ClientDir);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            try { _client?.Dispose(); } catch { }
            try
            {
                if (!_serverProcess.HasExited)
                {
                    try { _serverProcess.Kill(entireProcessTree: true); } catch { _serverProcess.Kill(); }
                    _serverProcess.WaitForExit(3000);
                }
            }
            catch { }
            _serverProcess.Dispose();
            if (_testPassed)
            {
                try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true); } catch { }
            }
            else
            {
                Console.WriteLine($"[SplitHarness] Preserved work dir: {_workDir}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SplitHarness] Cleanup error: {ex.Message}");
        }
    }

    // ---------------- Helper Methods ----------------
    private static (string WorkDir, string ServerDir, string ClientDir) SetupSplitPaths(string platform)
    {
        var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!; // bin/Debug/.../test assembly
        var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
        // Use a unique per-test directory to avoid file locking issues when assemblies are still loaded
        var unique = Guid.NewGuid().ToString("N");
        var workRootBase = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "WorkSplit" + platform);
        var workRoot = Path.Combine(workRootBase, unique);
        var serverDir = Path.Combine(workRoot, "ServerApp");
        var clientDir = Path.Combine(workRoot, "GuiClientApp");
        return (workRoot, serverDir, clientDir);
    }

    private static void PrepareDirectories(string workDir)
    {
        // Do not delete parent directories from earlier tests; they will be cleaned up on successful test completion.
        if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);
    }

    private static List<string> CollectTrustedPlatformAssemblies()
    {
        var result = new List<string>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (var p in tpa.Split(Path.PathSeparator)) if (!string.IsNullOrEmpty(p) && File.Exists(p)) result.Add(p);
        }
        return result;
    }

    private static async Task BuildProject(string projectDir)
    {
        await RunCmd("dotnet", "build", projectDir);
    }

    private static Process StartServer(string serverDir, int port)
    {
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build {port}",
                WorkingDirectory = serverDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        p.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[SERVER OUT] {e.Data}"); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[SERVER ERR] {e.Data}"); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static int GetFreePort(int start, int end)
    {
        for (int port = start; port < end; port++)
        {
            try { var l = new TcpListener(IPAddress.Loopback, port); l.Start(); l.Stop(); return port; } catch { }
        }
        var fallback = new TcpListener(IPAddress.Loopback, 0); fallback.Start(); int free = ((IPEndPoint)fallback.LocalEndpoint).Port; fallback.Stop(); return free;
    }

    private static async Task WaitForServerReadyHttps(int port)
    {
        for (int i = 0; i < 40; i++)
        {
            try
            {
                using var httpClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { Timeout = TimeSpan.FromSeconds(2) };
                var resp = await httpClient.GetAsync($"https://localhost:{port}/status");
                if (resp.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(100);
        }
        throw new Exception("Split server failed to report ready state");
    }

    private static async Task SmokeTestEndpoint(int port, string vmName)
    {
        using var httpClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
        var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"https://localhost:{port}/test_protos.{vmName}Service/GetState")
        { Content = new ByteArrayContent(new byte[] { 0, 0, 0, 0, 0 }) };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc-web+proto");
        await httpClient.SendAsync(req); // ignore result – just ensures pipeline reachable
    }

    private static async Task<(string stdout, string stderr)> RunCmd(string file, string args, string workDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
            throw new Exception($"{file} {args} failed:\n{sbOut}\n{sbErr}");
        return (sbOut.ToString(), sbErr.ToString());
    }
}

/// <summary>
/// Split harness strongly-typed client loader (uses already generated StronglyTypedTestClientGenerator output).
/// Reuses existing ITestClient interface.
/// </summary>
internal sealed class SplitGeneratedClient : ITestClient
{
    private readonly string _clientDir;
    private readonly int _port;
    private readonly string _vmName;
    private dynamic? _impl;
    private CollectibleTestLoadContext? _alc;
    private bool _disposed;

    public SplitGeneratedClient(string clientDir, int port, string vmName)
    {
        _clientDir = clientDir; _port = port; _vmName = vmName;
    }

    public async Task InitializeAsync()
    {
        _impl = Load();
        if (_impl != null)
        {
            try { await _impl.InitializeAsync(); } catch (Exception ex) { Console.WriteLine($"[SplitClient] Initialize failed: {ex.Message}"); }
        }
        else
        {
            await Task.Delay(50);
        }
    }

    private dynamic? Load()
    {
        var candidates = new[]
        {
            Path.Combine(_clientDir, "bin","Debug","net8.0-windows","GuiClientApp.dll"),
            Path.Combine(_clientDir, "bin","Debug","net8.0","GuiClientApp.dll"),
            Path.Combine(_clientDir, "obj","Debug","net8.0-windows","GuiClientApp.dll"),
            Path.Combine(_clientDir, "obj","Debug","net8.0","GuiClientApp.dll")
        };
        var asmPath = candidates.FirstOrDefault(File.Exists);
        if (asmPath == null)
        {
            Console.WriteLine("[SplitClient] GuiClientApp.dll not found");
            return null;
        }
        try
        {
            // Allow opt-out (use old behavior) for debugging if needed
            if (Environment.GetEnvironmentVariable("SPLITCLIENT_LEGACY_LOAD") == "1")
            {
                var legacyAsm = Assembly.LoadFrom(asmPath);
                var legacyType = legacyAsm.GetType($"Generated.TestClients.{_vmName}TestClient");
                if (legacyType == null) { Console.WriteLine("[SplitClient] Generated client type missing"); return null; }
                return Activator.CreateInstance(legacyType, "localhost", _port);
            }

            _alc = new CollectibleTestLoadContext();

            // Load assembly bytes to avoid file lock
            byte[] asmBytes = File.ReadAllBytes(asmPath);
            string pdbPath = Path.ChangeExtension(asmPath, ".pdb");
            byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;

            using var asmStream = new MemoryStream(asmBytes, writable: false);
            using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes, writable: false) : null;
            var asm = pdbStream == null
                ? _alc.LoadFromStream(asmStream)
                : _alc.LoadFromStream(asmStream, pdbStream);

            var type = asm.GetType($"Generated.TestClients.{_vmName}TestClient", throwOnError: false, ignoreCase: false);
            if (type == null) { Console.WriteLine("[SplitClient] Generated client type missing"); return null; }
            return Activator.CreateInstance(type, "localhost", _port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SplitClient] Load error: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GetModelDataAsync()
    {
        if (_impl != null)
        {
            try { return await _impl.GetModelDataAsync(); } catch (Exception ex) { Console.WriteLine($"[SplitClient] GetModelData failed: {ex.Message}"); }
        }
        return string.Empty;
    }

    public async Task<string> GetStructuralStateAsync()
    {
        if (_impl != null)
        {
            try
            {
                var mi = _impl.GetType().GetMethod("GetStructuralStateAsync");
                if (mi != null)
                {
                    var task = (Task<string>)mi.Invoke(_impl, Array.Empty<object>())!;
                    return await task;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplitClient] GetStructuralStateAsync failed: {ex.Message}");
            }
        }
        return await GetModelDataAsync();
    }

    public async Task UpdatePropertyAsync(string propertyName, object value)
    {
        if (_impl != null)
        {
            try { await _impl.UpdatePropertyAsync(propertyName, value); } catch (Exception ex) { Console.WriteLine($"[SplitClient] UpdateProperty {propertyName} failed: {ex.Message}"); }
        }
    }

    public async Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value)
    {
        if (_impl != null)
        {
            try { await _impl.UpdateIndexedPropertyAsync(collectionName, index, propertyName, value); } catch (Exception ex) { Console.WriteLine($"[SplitClient] UpdateIndexedProperty {collectionName}[{index}].{propertyName} failed: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try { if (_impl is IDisposable d) d.Dispose(); } catch { }
        _impl = null;

        if (_alc != null)
        {
            try
            {
                _alc.Unload();
                // Force collection to reclaim collectible context promptly
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplitClient] Unload warning: {ex.Message}");
            }
            _alc = null;
        }
    }
}

/// <summary>
/// Collectible load context to isolate dynamically generated GUI client assemblies per test.
/// </summary>
internal sealed class CollectibleTestLoadContext : AssemblyLoadContext
{
    public CollectibleTestLoadContext() : base(isCollectible: true) { }
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Let the default context resolve framework / shared assemblies.
        return null;
    }
}