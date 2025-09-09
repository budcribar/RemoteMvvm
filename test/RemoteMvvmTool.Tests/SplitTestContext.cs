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
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace RemoteMvvmTool.Tests;

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
    { _client = client; _serverProcess = serverProcess; _workDir = workDir; _serverDir = serverDir; _clientDir = clientDir; }

    public void MarkTestPassed() => _testPassed = true;

    private static void FailIfNamedGuiProcessesRunning()
    {
        var names = new[] { "ServerApp", "GuiClientApp" };
        foreach (var n in names)
        {
            try
            {
                var processes = Process.GetProcessesByName(n);
                if (processes.Length > 0)
                {
                    Console.WriteLine($"[SplitTestContext] Found {processes.Length} leftover {n} process(es). Attempting cleanup...");
                    
                    // Attempt to kill leftover processes
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                Console.WriteLine($"[SplitTestContext] Killing leftover process: {n} (PID: {process.Id})");
                                process.Kill(entireProcessTree: true);
                                process.WaitForExit(2000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SplitTestContext] Failed to kill {n}: {ex.Message}");
                        }
                        finally
                        {
                            try { process.Dispose(); } catch { }
                        }
                    }
                    
                    // Wait a moment for cleanup to complete
                    System.Threading.Thread.Sleep(500);
                    
                    // Double-check if any are still running
                    var remainingProcesses = Process.GetProcessesByName(n);
                    if (remainingProcesses.Length > 0)
                    {
                        foreach (var p in remainingProcesses) { try { p.Dispose(); } catch { } }
                        throw new InvalidOperationException($"Blocked: Unable to cleanup leftover process '{n}'. Please manually terminate it before running split GUI tests.");
                    }
                    
                    Console.WriteLine($"[SplitTestContext] Successfully cleaned up leftover {n} processes.");
                }
            }
            catch (PlatformNotSupportedException) { }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception) { }
        }
    }

    // ===== Build Cache Integration (single authoritative definitions) =====
    private static readonly ConcurrentDictionary<string, Task<CachedBuild>> _buildCache = new();
    private record CachedBuild(string Hash, string Platform, string CacheRoot, string ServerDir, string ClientDir, string ViewModelName, bool UseServerGui);
    private static bool IsCacheDisabled() => string.Equals(Environment.GetEnvironmentVariable("REMOTEMVVM_BUILD_CACHE"), "0", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("REMOTEMVVM_BUILD_CACHE"), "false", StringComparison.OrdinalIgnoreCase);
    private static string ComputeHash(string text){ using var sha=SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))); }
    private static readonly string _generatorAsmHash = ComputeGeneratorAssemblyHash();
    private static readonly string _envSignature = _generatorAsmHash + "_v9_" + DateTime.UtcNow.ToString("yyyyMMddHH"); // Force invalidation with timestamp
    private static string ComputeGeneratorAssemblyHash(){ try { var asm = typeof(SplitProjectGenerator).Assembly; var path = asm.Location; if (!File.Exists(path)) return "NO_ASM_FILE"; using var sha=SHA256.Create(); using var fs=File.OpenRead(path); return Convert.ToHexString(sha.ComputeHash(fs)); } catch { return "GEN_HASH_ERROR"; } }
    private static void EnsureEnvironmentSignatureConsistent(string cacheRoot){ try { Directory.CreateDirectory(cacheRoot); var sigFile=Path.Combine(cacheRoot,"env.sig"); var existing=File.Exists(sigFile)?File.ReadAllText(sigFile):null; if(!string.Equals(existing,_envSignature,StringComparison.Ordinal)){ foreach(var d in Directory.GetDirectories(cacheRoot,"h_*")){ try{Directory.Delete(d,true);}catch{}} File.WriteAllText(sigFile,_envSignature); Console.WriteLine("[GuiBuildCache] Purged due to generator assembly change."); } } catch(Exception ex){ Console.WriteLine($"[GuiBuildCache] Signature check warning: {ex.Message}"); } }
    private static async Task<CachedBuild> GetOrBuildAsync(string modelCode,string platform, bool useServerGui = true){ 
        if(IsCacheDisabled()) 
        {
            Console.WriteLine("[GuiBuildCache] Cache disabled by environment variable, building fresh");
            return await BuildFreshAsync(modelCode,platform,useServerGui); 
        }
        var key=ComputeHash(modelCode+"|"+platform+"|"+useServerGui+"|"+_envSignature); 
        return await _buildCache.GetOrAdd(key,_=>BuildFreshAsync(modelCode,platform,useServerGui,key)); 
    }
    private static async Task<CachedBuild> BuildFreshAsync(string modelCode,string platform,bool useServerGui = true,string? hash=null){ var baseTestDir=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!; var repoRoot=Path.GetFullPath(Path.Combine(baseTestDir,"../../../../..")); var cacheRoot=Path.Combine(repoRoot,"test","RemoteMvvmTool.Tests","TestData","GuiBuildCache",platform); EnsureEnvironmentSignatureConsistent(cacheRoot); Directory.CreateDirectory(cacheRoot); hash ??= ComputeHash(modelCode+"|"+platform+"|"+useServerGui+"|"+_envSignature); var buildDir=Path.Combine(cacheRoot,"h_"+hash); var serverDir=Path.Combine(buildDir,"ServerApp"); var clientDir=Path.Combine(buildDir,"GuiClientApp"); if(Directory.Exists(clientDir)&&Directory.GetFiles(clientDir,"GuiClientApp.dll",SearchOption.AllDirectories).Any()){ var inferred=InferViewModelName(buildDir)??"TestViewModel"; return new CachedBuild(hash,platform,buildDir,serverDir,clientDir,inferred,useServerGui);} Directory.CreateDirectory(buildDir); var modelPath=Path.Combine(buildDir,"TestViewModel.cs"); File.WriteAllText(modelPath,modelCode); var refs=CollectTrustedPlatformAssemblies(); var (_,vmName,props,cmds,comp)=await ViewModelAnalyzer.AnalyzeAsync(new[]{modelPath},"CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute","CommunityToolkit.Mvvm.Input.RelayCommandAttribute",refs,"CommunityToolkit.Mvvm.ComponentModel.ObservableObject"); SplitProjectGenerator.Generate(buildDir,vmName,props,cmds,comp,platform,"Test.Protos",useServerGui); await RunCmdInternal("dotnet","build --nologo",serverDir); await RunCmdInternal("dotnet","build --nologo",clientDir); return new CachedBuild(hash,platform,buildDir,serverDir,clientDir,vmName,useServerGui);}    
    private static string? InferViewModelName(string buildDir){ try{ var file=Directory.GetFiles(buildDir,"*TestClient.cs",SearchOption.AllDirectories).FirstOrDefault(); if(file!=null){ var name=Path.GetFileNameWithoutExtension(file); if(name.EndsWith("TestClient",StringComparison.Ordinal)) return name.Substring(0,name.Length-"TestClient".Length);} }catch{} return null; }
    private static void CopyDirectoryRecursive(string sourceDir, string destDir){ foreach(var dir in Directory.GetDirectories(sourceDir,"*",SearchOption.AllDirectories)){ var rel=dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar); Directory.CreateDirectory(Path.Combine(destDir,rel)); } foreach(var file in Directory.GetFiles(sourceDir,"*",SearchOption.AllDirectories)){ var rel=file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar); var target=Path.Combine(destDir,rel); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file,target,true);} }

    public static async Task<SplitTestContext> CreateAsync(string modelCode, string platform = "wpf", bool useServerGui = true)
    {
        FailIfNamedGuiProcessesRunning();
        var paths = SetupSplitPaths(platform);
        PrepareDirectories(paths.WorkDir);
        var cached = await GetOrBuildAsync(modelCode, platform, useServerGui);
        CopyDirectoryRecursive(cached.ServerDir, paths.ServerDir);
        CopyDirectoryRecursive(cached.ClientDir, paths.ClientDir);

        // Log server UI generation setting
        Console.WriteLine($"[SplitTestContext] Server GUI generation: {(useServerGui ? "Enabled" : "Disabled")} for platform: {platform}");

        // Simplest patch: emit combined solution + launch into transient work dir
        try
        {
            var sln = CsProjectGenerator.GenerateSolutionXml("ServerApp/ServerApp.csproj", "GuiClientApp/GuiClientApp.csproj");
            File.WriteAllText(Path.Combine(paths.WorkDir, "ClientServer.slnx"), sln);
            var launch = """
[
  {
    "Name": "ClientAndServer",
    "Projects": [
      { "Path": "GuiClientApp\\GuiClientApp.csproj", "Action": "Start" },
      { "Path": "ServerApp\\ServerApp.csproj", "Action": "Start" }
    ]
  }
]
""";
            File.WriteAllText(Path.Combine(paths.WorkDir, "ClientServer.slnLaunch.user"), launch);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SplitTestContext] Failed to emit transient work solution: " + ex.Message);
        }

        var port = GetFreePort(6000, 6500);
        var serverProcess = StartServer(paths.ServerDir, port);
        await WaitForServerReadyHttps(port);
        await SmokeTestEndpoint(port, cached.ViewModelName);
        var client = new SplitGeneratedClient(paths.ClientDir, port, cached.ViewModelName);
        await client.InitializeAsync();
        return new SplitTestContext(client, serverProcess, paths.WorkDir, paths.ServerDir, paths.ClientDir);
    }

    private static (string WorkDir,string ServerDir,string ClientDir) SetupSplitPaths(string platform){ var baseTestDir=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!; var repoRoot=Path.GetFullPath(Path.Combine(baseTestDir,"../../../../..")); var workRootBase=Path.Combine(repoRoot,"test","RemoteMvvmTool.Tests","TestData","WorkSplit"+platform); Directory.CreateDirectory(workRootBase); var unique=Guid.NewGuid().ToString("N"); var workRoot=Path.Combine(workRootBase,unique); var serverDir=Path.Combine(workRoot,"ServerApp"); var clientDir=Path.Combine(workRoot,"GuiClientApp"); return (workRoot,serverDir,clientDir);}    
    private static void PrepareDirectories(string workDir){ if(!Directory.Exists(workDir)) Directory.CreateDirectory(workDir); }
    private static List<string> CollectTrustedPlatformAssemblies(){ var result=new List<string>(); if(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa){ foreach(var p in tpa.Split(Path.PathSeparator)) if(!string.IsNullOrEmpty(p)&&File.Exists(p)) result.Add(p);} return result; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            try { _client?.Dispose(); } catch { }
            
            // Enhanced process cleanup to handle both dotnet run and the actual ServerApp/GuiClientApp processes
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
            
            // Additional cleanup: Kill any leftover ServerApp and GuiClientApp processes by name
            KillProcessesByName("ServerApp");
            KillProcessesByName("GuiClientApp");
            
            Console.WriteLine($"[SplitHarness] Work dir retained (passed={_testPassed}): {_workDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SplitHarness] Cleanup error: {ex.Message}");
        }
    }

    private static void KillProcessesByName(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        Console.WriteLine($"[SplitHarness] Killing leftover process: {processName} (PID: {process.Id})");
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SplitHarness] Failed to kill {processName}: {ex.Message}");
                }
                finally
                {
                    try { process.Dispose(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SplitHarness] Error getting processes by name {processName}: {ex.Message}");
        }
    }

    private static int _warmStarted;
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void StartWarmupModuleInit()
    {
        if (IsCacheDisabled()) return;
        if (string.Equals(Environment.GetEnvironmentVariable("REMOTEMVVM_WARMUP"), "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("REMOTEMVVM_WARMUP"), "false", StringComparison.OrdinalIgnoreCase)) return;
        if (System.Threading.Interlocked.Exchange(ref _warmStarted, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
                var modelsDir = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd", "Models");
                if (!Directory.Exists(modelsDir)) return;
                var modelFiles = Directory.GetFiles(modelsDir, "*.cs", SearchOption.TopDirectoryOnly);
                var limited = modelFiles.Take(20).ToArray();
                var sem = new System.Threading.SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));
                var tasks = new List<Task>();
                foreach (var file in limited)
                {
                    var code = File.ReadAllText(file);
                    foreach (var platform in new[] { "wpf", "winforms" })
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            await sem.WaitAsync();
                            try { await GetOrBuildAsync(code, platform); }
                            catch (Exception ex) { Console.WriteLine($"[SplitWarmup] {Path.GetFileName(file)} {platform} failed: {ex.Message}"); }
                            finally { sem.Release(); }
                        }));
                    }
                }
                await Task.WhenAll(tasks);
                Console.WriteLine($"[SplitWarmup] Prefetched {limited.Length} model(s) * 2 platforms.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplitWarmup] Warmup error: {ex.Message}");
            }
        });
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
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc-web+proto");
        await httpClient.SendAsync(req);
    }

    private static async Task<(string stdout, string stderr)> RunCmdInternal(string file, string args, string workDir)
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
        if (p.ExitCode != 0) throw new Exception($"{file} {args} failed:\n{sbOut}\n{sbErr}");
        return (sbOut.ToString(), sbErr.ToString());
    }

    internal sealed class SplitGeneratedClient : ITestClient
    {
        private readonly string _clientDir;
        private readonly int _port;
        private readonly string _vmName;
        private dynamic? _impl;
        private CollectibleTestLoadContext? _alc;
        private bool _disposed;

        public SplitGeneratedClient(string clientDir, int port, string vmName)
        { _clientDir = clientDir; _port = port; _vmName = vmName; }

        public async Task InitializeAsync()
        {
            _impl = Load();
            if (_impl != null)
            {
                try { await _impl.InitializeAsync(); } catch (Exception ex) { Console.WriteLine($"[SplitClient] Initialize failed: {ex.Message}"); }
            }
            else { await Task.Delay(25); }
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
            if (asmPath == null) { Console.WriteLine("[SplitClient] GuiClientApp.dll not found"); return null; }
            try
            {
                if (Environment.GetEnvironmentVariable("SPLITCLIENT_LEGACY_LOAD") == "1")
                {
                    var legacyAsm = Assembly.LoadFrom(asmPath);
                    var type = legacyAsm.GetType($"Generated.TestClients.{_vmName}TestClient");
                    if (type == null) return null;
                    return Activator.CreateInstance(type, "localhost", _port);
                }
                _alc = new CollectibleTestLoadContext();
                var asmBytes = File.ReadAllBytes(asmPath);
                var pdbPath = Path.ChangeExtension(asmPath, ".pdb");
                byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;
                using var asmStream = new MemoryStream(asmBytes, false);
                using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes, false) : null;
                var asm = pdbStream == null ? _alc.LoadFromStream(asmStream) : _alc.LoadFromStream(asmStream, pdbStream);
                var clientType = asm.GetType($"Generated.TestClients.{_vmName}TestClient", false, false);
                if (clientType == null) { Console.WriteLine("[SplitClient] Generated client type missing"); return null; }
                return Activator.CreateInstance(clientType, "localhost", _port);
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
                        var t = (Task<string>)mi.Invoke(_impl, Array.Empty<object>())!;
                        return await t;
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[SplitClient] GetStructuralStateAsync failed: {ex.Message}"); }
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
            if (_disposed) return;
            _disposed = true;
            try { if (_impl is IDisposable d) d.Dispose(); } catch { }
            _impl = null;
            if (_alc != null)
            {
                try { _alc.Unload(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { }
                _alc = null;
            }
        }
    }

    internal sealed class CollectibleTestLoadContext : AssemblyLoadContext
    {
        public CollectibleTestLoadContext() : base(isCollectible: true) { }
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}