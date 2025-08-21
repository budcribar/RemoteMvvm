using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Diagnostics;
using RemoteMvvmTool.Generators;

namespace SampleViewModel
{
    public class GenerationTests
    {
        static void AssertFileEqual(string expectedPath, string actualPath)
        {
            var expected = File.ReadAllText(expectedPath).Trim().Replace("\r\n", "\n");
            var actual = File.ReadAllText(actualPath).Trim().Replace("\r\n", "\n");
            if (expected != actual)
            {
                var actualDir = Path.Combine(Path.GetDirectoryName(expectedPath)!, "..", "actual");
                Directory.CreateDirectory(actualDir);
                var destPath = Path.Combine(actualDir, Path.GetFileName(expectedPath));
                File.Copy(actualPath, destPath, true);
                try
                {
                    var psi = new ProcessStartInfo("git", $"--no-pager diff --no-index \"{expectedPath}\" \"{destPath}\"")
                    {
                        UseShellExecute = false
                    };
                    var p = Process.Start(psi);
                    p?.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                Assert.Equal(expected, actual);
            }
        }
        static async Task<(string vmName, string[] outputs)> GenerateAsync(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var vmFile = Path.Combine(root, "test", "SampleViewModel", "SampleViewModel.cs");

            var refs = LoadDefaultRefs();
            var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",

                refs);
            if (sym == null) throw new Exception("ViewModel not found");
            File.WriteAllText(Path.Combine(outputDir, "SampleViewModelService.proto"), ProtoGenerator.Generate("SampleApp.ViewModels.Protos", "CounterService", name, props, cmds, comp));
            File.WriteAllText(Path.Combine(outputDir, "SampleViewModelRemoteClient.ts"), TypeScriptClientGenerator.Generate(name, "SampleApp.ViewModels.Protos", "CounterService", props, cmds));
            var vmNamespace = sym.ContainingNamespace.ToDisplayString();
            File.WriteAllText(Path.Combine(outputDir, "SampleViewModelGrpcServiceImpl.cs"), ServerGenerator.Generate(name, "SampleApp.ViewModels.Protos", "CounterService", props, cmds, vmNamespace));
            File.WriteAllText(Path.Combine(outputDir, "SampleViewModelRemoteClient.cs"), ClientGenerator.Generate(name, "SampleApp.ViewModels.Protos", "CounterService", props, cmds));
            return (name, Directory.GetFiles(outputDir));
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

        [Fact]
        public async Task GeneratedOutputs_MatchExpected()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var actualDir = Path.Combine(root, "test", "SampleViewModel", "actual");
           
            var (_, files) = await GenerateAsync(actualDir);
           
            var expectedDir = Path.Combine(root, "test", "SampleViewModel", "expected");
            foreach (var expected in Directory.GetFiles(expectedDir))
            {
                var generatedPath = Path.Combine(actualDir, Path.GetFileName(expected));
                Assert.True(File.Exists(generatedPath), $"Expected output {generatedPath} not found");
                AssertFileEqual(expected, generatedPath);
            }
        }
    }
}
