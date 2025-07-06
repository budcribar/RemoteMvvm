using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Xunit;
using System.Diagnostics;
using RemoteMvvmTool.Generators;

namespace GameViewModel
{
    public class GameViewModelGenerationTests
    {
        static void AssertEqualWithDiff(string expectedPath, string actualText)
        {
            var expected = File.ReadAllText(expectedPath);
            var normExpected = expected.Replace("\r\n", "\n");
            var normActual = actualText.Replace("\r\n", "\n");
            if (normExpected != normActual)
            {
                var actualDir = Path.Combine(Path.GetDirectoryName(expectedPath)!, "..", "actual");
                Directory.CreateDirectory(actualDir);
                var actualPath = Path.Combine(actualDir, Path.GetFileName(expectedPath));
                File.WriteAllText(actualPath, actualText);
                try
                {
                    var psi = new ProcessStartInfo("code", $"--diff \"{expectedPath}\" \"{actualPath}\"")
                    {
                        UseShellExecute = true // Required to open a window
                    };
                    var p = Process.Start(psi);
                    p?.WaitForExit();

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                Assert.Equal(normExpected, normActual);
            }
        }
        static System.Collections.Generic.List<string> LoadDefaultRefs()
        {
            var list = new System.Collections.Generic.List<string>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (tpa != null)
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
            }
            return list;
        }

        private static async Task<(string Proto, string Server, string Client, string Ts)> GenerateAsync()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            string vmFile = Path.Combine(root, "src", "demo", "MonsterClicker", "ViewModels", "GameViewModel.cs");
            var references = LoadDefaultRefs();
            var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                references);
            Assert.NotNull(sym);
            const string protoNs = "MonsterClicker.ViewModels.Protos";
            const string serviceName = "GameViewModelService";
            var proto = ProtoGenerator.Generate(protoNs, serviceName, name, props, cmds, comp);
            var vmNamespace = sym!.ContainingNamespace.ToDisplayString();
            var server = ServerGenerator.Generate(name, protoNs, serviceName, props, cmds, vmNamespace);
            var client = ClientGenerator.Generate(name, protoNs, serviceName, props, cmds);
            var ts = TypeScriptClientGenerator.Generate(name, protoNs, serviceName, props, cmds);
            return (proto, server, client, ts);
        }

        [Fact]
        public async Task ProtoMatchesExpected()
        {
            var (proto, _, _, _) = await GenerateAsync();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            AssertEqualWithDiff(Path.Combine(root, "test", "GameViewModel", "expected", "GameViewModelService.proto"), proto);
        }

        [Fact]
        public async Task ServerMatchesExpected()
        {
            var (_, server, _, _) = await GenerateAsync();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            AssertEqualWithDiff(Path.Combine(root, "test", "GameViewModel", "expected", "GameViewModelGrpcServiceImpl.cs"), server);
        }

        [Fact]
        public async Task ClientMatchesExpected()
        {
            var (_, _, client, _) = await GenerateAsync();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            AssertEqualWithDiff(Path.Combine(root, "test", "GameViewModel", "expected", "GameViewModelRemoteClient.cs"), client);
        }

        [Fact]
        public async Task TypeScriptMatchesExpected()
        {
            var (_, _, _, ts) = await GenerateAsync();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            AssertEqualWithDiff(Path.Combine(root, "test", "GameViewModel", "expected", "GameViewModelRemoteClient.ts"), ts);
        }
    }
}
