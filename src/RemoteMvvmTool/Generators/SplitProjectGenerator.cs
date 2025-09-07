using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using GrpcRemoteMvvmModelUtil;
using System.Text.RegularExpressions;
using System.Threading;

namespace RemoteMvvmTool.Generators;

public static class SplitProjectGenerator
{
    public sealed record GeneratedSplitPaths(string BaseDir, string ServerDir, string ClientDir, string ProtoFile, string ViewModelName, string ServiceName);

    private static readonly object _optionsCopyLock = new();

    private static void SafeCopyWithRetry(string source, string destination, string? injectRunString = null)
    {
        const int MaxAttempts = 6;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                // Copy to temp first for atomic replace
                var tempFile = destination + ".tmp_" + Guid.NewGuid().ToString("N");
                using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dst = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    src.CopyTo(dst);
                }
                if (injectRunString != null)
                {
                    // Inline modification (small file) – safe to read all
                    var txt = File.ReadAllText(tempFile);
                    if (!txt.Contains("RunString", StringComparison.Ordinal))
                    {
                        const string serverClassToken = "public class ServerOptions";
                        int idx = txt.IndexOf(serverClassToken, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            int braceIdx = txt.IndexOf('{', idx + serverClassToken.Length);
                            if (braceIdx >= 0)
                            {
                                braceIdx++;
                                txt = txt.Insert(braceIdx, "\n        public string RunString { get; set; } = \"WPF\";\n");
                            }
                        }
                        File.WriteAllText(tempFile, txt);
                    }
                }
                // Replace existing file atomically
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(tempFile, destination);
                return; // success
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(40 * attempt); // back-off
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(40 * attempt);
            }
        }
        // Final attempt without try-catch to surface error
        var finalTemp = destination + ".tmp_final_" + Guid.NewGuid().ToString("N");
        using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var dst = new FileStream(finalTemp, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            src.CopyTo(dst);
        }
        if (injectRunString != null)
        {
            var txt = File.ReadAllText(finalTemp);
            if (!txt.Contains("RunString", StringComparison.Ordinal))
            {
                const string serverClassToken = "public class ServerOptions";
                int idx = txt.IndexOf(serverClassToken, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    int braceIdx = txt.IndexOf('{', idx + serverClassToken.Length);
                    if (braceIdx >= 0)
                    {
                        braceIdx++;
                        txt = txt.Insert(braceIdx, "\n        public string RunString { get; set; } = \"WPF\";\n");
                    }
                }
                File.WriteAllText(finalTemp, txt);
            }
        }
        if (File.Exists(destination)) File.Delete(destination);
        File.Move(finalTemp, destination);
    }

    public static GeneratedSplitPaths Generate(string baseDir, string viewModelName, List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation, string platform, string protoNamespace = "Test.Protos")
    {
        Directory.CreateDirectory(baseDir);
        var serverDir = Path.Combine(baseDir, "ServerApp");
        var clientDir = Path.Combine(baseDir, "GuiClientApp");
        Directory.CreateDirectory(serverDir);
        Directory.CreateDirectory(clientDir);

        string serviceName = viewModelName + "Service";
        var protoContent = ProtoGenerator.Generate(protoNamespace, serviceName, viewModelName, props, cmds, compilation);
        var conv = ConversionGenerator.Generate(protoNamespace, "Generated.ViewModels", props.Select(p => p.FullTypeSymbol!), compilation);

        // -------- Ensure model source present in both projects --------
        string[] modelCandidates =
        {
            Path.Combine(baseDir, viewModelName + ".cs"),
            Path.Combine(baseDir, "TestViewModel.cs")
        };
        string? modelExisting = modelCandidates.FirstOrDefault(File.Exists);
        string modelContent = modelExisting != null ? File.ReadAllText(modelExisting) : "// Model source missing";
        File.WriteAllText(Path.Combine(serverDir, viewModelName + ".cs"), modelContent);
        File.WriteAllText(Path.Combine(clientDir, viewModelName + ".cs"), modelContent);
        if (modelExisting != null)
        {
            var originalName = Path.GetFileName(modelExisting);
            if (!string.Equals(originalName, viewModelName + ".cs", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(Path.Combine(serverDir, originalName), modelContent);
                File.WriteAllText(Path.Combine(clientDir, originalName), modelContent);
            }
        }

        // -------- Locate or synthesize GrpcRemoteOptions --------
        string? FindGrpcRemoteOptions()
        {
            try
            {
                var dir = baseDir;
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    var candidate = Directory.GetFiles(dir, "GrpcRemoteOptions.cs", SearchOption.AllDirectories)
                        .FirstOrDefault(f => !f.Contains("WorkSplit", StringComparison.OrdinalIgnoreCase));
                    if (candidate != null) return candidate;
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }
            catch { }
            return null;
        }
        var optionsSource = FindGrpcRemoteOptions();

        string fallbackOptions = """// <auto-generated>\n// Fallback options generated by SplitProjectGenerator\n// </auto-generated>\n#nullable enable\nnamespace PeakSWC.Mvvm.Remote { public class ServerOptions { public int Port {get;set;} = 50052; public bool UseHttps {get;set;}=true; public string? CorsPolicyName {get;set;}=\"AllowAll\"; public string[]? AllowedOrigins {get;set;}=null; public string[]? AllowedHeaders {get;set;}=null; public string[]? AllowedMethods {get;set;}=null; public string[]? ExposedHeaders {get;set;}=null; public string? LogLevel {get;set;}=\"Debug\"; public string RunString {get;set;}=\"WPF\"; } public class ClientOptions { public string Address {get;set;}=\"https://localhost:50052\"; public bool UseHttps {get;set;}=true; } }""";

        void EnsureOptions(string targetDir)
        {
            string optionsPath = Path.Combine(targetDir, "GrpcRemoteOptions.cs");
            lock (_optionsCopyLock)
            {
                if (optionsSource != null)
                {
                    SafeCopyWithRetry(optionsSource, optionsPath, injectRunString: "RunString");
                }
                else
                {
                    // Write fallback with retry (rare case)
                    const int maxAttempts = 4;
                    for (int a = 1; a <= maxAttempts; a++)
                    {
                        try { File.WriteAllText(optionsPath, fallbackOptions); break; }
                        catch (IOException) when (a < maxAttempts) { Thread.Sleep(25 * a); }
                    }
                }
            }
        }

        // ---------------- Generate single combined partial then split ----------------
        var fullPartial = ViewModelPartialGenerator.Generate(viewModelName, protoNamespace, serviceName, "Generated.ViewModels", "Generated.Clients", "CommunityToolkit.Mvvm.ComponentModel.ObservableObject", platform, true, props);

        // Extract client constructor + GetRemoteModel
        string clientCtorPattern = @$"public {Regex.Escape(viewModelName)}\(ClientOptions options\) : this\([^)]*\)\s*{{"; // start of ctor
        // Simple brace matcher for ctor and GetRemoteModel
        string ExtractBlock(string source, int startIndex)
        {
            int depth = 0; int i = startIndex;
            while (i < source.Length && source[i] != '{') i++;
            if (i == source.Length) return string.Empty;
            int blockStart = i;
            for (; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}') { depth--; if (depth == 0) { i++; return source.Substring(startIndex, i - startIndex); } }
            }
            return string.Empty;
        }

        string clientCtorBlock = string.Empty;
        var ctorMatch = Regex.Match(fullPartial, clientCtorPattern);
        if (ctorMatch.Success)
        {
            clientCtorBlock = ExtractBlock(fullPartial, ctorMatch.Index);
            if (!string.IsNullOrEmpty(clientCtorBlock))
                fullPartial = fullPartial.Remove(ctorMatch.Index, clientCtorBlock.Length);
        }

        string getRemotePattern = @$"public\s+async\s+Task<{Regex.Escape(viewModelName)}RemoteClient>\s+GetRemoteModel\s*\(";
        string getRemoteBlock = string.Empty;
        var getMatch = Regex.Match(fullPartial, getRemotePattern);
        if (getMatch.Success)
        {
            getRemoteBlock = ExtractBlock(fullPartial, getMatch.Index);
            if (!string.IsNullOrEmpty(getRemoteBlock))
                fullPartial = fullPartial.Remove(getMatch.Index, getRemoteBlock.Length);
        }

        string serverPartial = fullPartial; // remainder after removals
        // Remove client-only usings and remote client fields from server partial
        serverPartial = Regex.Replace(serverPartial, @"^using\s+Generated\.Clients;\s*\r?\n", string.Empty, RegexOptions.Multiline);
        serverPartial = Regex.Replace(serverPartial, @"private\s+Generated\.Clients\.[A-Za-z0-9_]+RemoteClient\?\s+_remoteClient;?\s*", string.Empty);

        string clientPartial = $@"// <auto-generated>\n// Client partial extracted by SplitProjectGenerator\n// </auto-generated>\n#nullable enable\nusing System;\nusing System.Threading.Tasks;\nusing Grpc.Net.Client;\nusing Test.Protos;\nusing Generated.Clients;\nusing PeakSWC.Mvvm.Remote;\nnamespace Generated.ViewModels {{ public partial class {viewModelName} {{\n        // Client-side runtime fields added by split generator\n        private object? _dispatcher;\n        private GrpcChannel? _channel;\n        private {viewModelName}RemoteClient? _remoteClient;\n{clientCtorBlock}\n{getRemoteBlock}\n}} }}".Replace("\\n", Environment.NewLine);

        // ---------------- Server project ----------------
        var serverProtoDir = Path.Combine(serverDir, "protos");
        Directory.CreateDirectory(serverProtoDir);
        var serverProtoPath = Path.Combine(serverProtoDir, serviceName + ".proto");
        File.WriteAllText(serverProtoPath, protoContent);
        File.WriteAllText(Path.Combine(serverDir, viewModelName + "GrpcServiceImpl.cs"),
            ServerGenerator.Generate(viewModelName, protoNamespace, serviceName, props, cmds, "Generated.ViewModels", platform));
        File.WriteAllText(Path.Combine(serverDir, "ProtoStateConverters.cs"), conv);
        File.WriteAllText(Path.Combine(serverDir, viewModelName + ".Server.cs"), serverPartial);
        File.WriteAllText(Path.Combine(serverDir, "Program.cs"), CsProjectGenerator.GenerateServerProgram("ServerApp", protoNamespace, serviceName, platform));
        File.WriteAllText(Path.Combine(serverDir, "ServerApp.csproj"), CsProjectGenerator.GenerateCsProj("ServerApp", serviceName, platform));
        EnsureOptions(serverDir);

        // ---------------- Client project ----------------
        var clientProtoDir = Path.Combine(clientDir, "protos");
        Directory.CreateDirectory(clientProtoDir);
        File.WriteAllText(Path.Combine(clientProtoDir, serviceName + ".proto"), protoContent);
        var remoteClientCode = ClientGenerator.Generate(viewModelName, protoNamespace, serviceName, props, cmds, "Generated.Clients");
        File.WriteAllText(Path.Combine(clientDir, viewModelName + "RemoteClient.cs"), remoteClientCode);
        File.WriteAllText(Path.Combine(clientDir, viewModelName + ".Client.cs"), clientPartial);
        File.WriteAllText(Path.Combine(clientDir, "ProtoStateConverters.cs"), conv);
        File.WriteAllText(Path.Combine(clientDir, viewModelName + "TestClient.cs"), StronglyTypedTestClientGenerator.Generate(viewModelName, protoNamespace, serviceName, props, cmds));
        if (!string.Equals(platform, "wpf", StringComparison.OrdinalIgnoreCase))
        {
            // Only generate Program.cs for non-WPF (WPF uses App.xaml autogenerated entry point)
            File.WriteAllText(Path.Combine(clientDir, "Program.cs"), CsProjectGenerator.GenerateGuiClientProgram("GuiClientApp", platform, protoNamespace, serviceName, "Generated.Clients", props, cmds));
        }
        File.WriteAllText(Path.Combine(clientDir, "GuiClientApp.csproj"), CsProjectGenerator.GenerateGuiClientCsProj("GuiClientApp", serviceName, platform));

        // NEW: Generate required WPF XAML files when platform is WPF (tests/build expect them)
        if (string.Equals(platform, "wpf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var appXamlPath = Path.Combine(clientDir, "App.xaml");
                var appCsPath = Path.Combine(clientDir, "App.xaml.cs");
                var mainXamlPath = Path.Combine(clientDir, "MainWindow.xaml");
                var mainCsPath = Path.Combine(clientDir, "MainWindow.xaml.cs");
                File.WriteAllText(appXamlPath, CsProjectGenerator.GenerateWpfAppXaml());
                File.WriteAllText(appCsPath, CsProjectGenerator.GenerateWpfAppCodeBehind(serviceName, viewModelName + "RemoteClient"));
                File.WriteAllText(mainXamlPath, CsProjectGenerator.GenerateWpfMainWindowXaml("GuiClientApp", viewModelName + "RemoteClient", props, cmds));
                File.WriteAllText(mainCsPath, CsProjectGenerator.GenerateWpfMainWindowCodeBehind());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplitProjectGenerator] Failed generating WPF XAML files: {ex.Message}");
            }
        }
        EnsureOptions(clientDir);

        // LaunchSettings separated
        void WriteServerLaunch()
        {
            var propsDirLS = Path.Combine(serverDir, "Properties");
            Directory.CreateDirectory(propsDirLS);
            File.WriteAllText(Path.Combine(propsDirLS, "launchSettings.json"), CsProjectGenerator.GenerateServerLaunchSettings());
        }
        void WriteClientLaunch()
        {
            var propsDirLS = Path.Combine(clientDir, "Properties");
            Directory.CreateDirectory(propsDirLS);
            File.WriteAllText(Path.Combine(propsDirLS, "launchSettings.json"), CsProjectGenerator.GenerateClientLaunchSettings());
        }
        WriteServerLaunch();
        WriteClientLaunch();

        // ---------------- Solution + launch configuration (.slnx + .slnLaunch.user) ----------------
        try
        {
            var solutionPath = Path.Combine(baseDir, "ClientServer.slnx");
            var solutionXml = CsProjectGenerator.GenerateSolutionXml("ServerApp/ServerApp.csproj", "GuiClientApp/GuiClientApp.csproj");
            File.WriteAllText(solutionPath, solutionXml);

            var launchUserContent = """
[
  {
    "Name": "ClientAndServer",
    "Projects": [
      {
        "Path": "GuiClientApp\\GuiClientApp.csproj",
        "Action": "Start"
      },
      {
        "Path": "ServerApp\\ServerApp.csproj",
        "Action": "Start"
      }
    ]
  }
]
""";
            File.WriteAllText(Path.Combine(baseDir, "ClientServer.slnLaunch.user"), launchUserContent);

            // NEW: individual project solution files for convenience
            var serverSlnx = CsProjectGenerator.GenerateSingleProjectSolutionXml("ServerApp/ServerApp.csproj");
            File.WriteAllText(Path.Combine(baseDir, "ServerApp.slnx"), serverSlnx);
            var serverLaunch = CsProjectGenerator.GenerateSingleProjectLaunchUser("ServerApp/ServerApp.csproj");
            File.WriteAllText(Path.Combine(baseDir, "ServerApp.slnLaunch.user"), serverLaunch);

            var clientSlnx = CsProjectGenerator.GenerateSingleProjectSolutionXml("GuiClientApp/GuiClientApp.csproj");
            File.WriteAllText(Path.Combine(baseDir, "GuiClientApp.slnx"), clientSlnx);
            var clientLaunch = CsProjectGenerator.GenerateSingleProjectLaunchUser("GuiClientApp/GuiClientApp.csproj");
            File.WriteAllText(Path.Combine(baseDir, "GuiClientApp.slnLaunch.user"), clientLaunch);
        }
        catch (Exception ex)
        {
            // Non-fatal; tests can still run without solution file
            Console.WriteLine($"[SplitProjectGenerator] Failed to write .slnx or .slnLaunch.user: {ex.Message}");
        }

        return new GeneratedSplitPaths(baseDir, serverDir, clientDir, Path.Combine(serverProtoDir, serviceName + ".proto"), viewModelName, serviceName);
    }
}
