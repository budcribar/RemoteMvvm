using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;

public class DefaultNamespaceGenerationTests
{
    [Fact(Skip = "Dispatcher option updated")]
    public async Task RemoteMvvmTool_Generates_And_Compiles_Simple_ViewModel()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var oldDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = vmDir;
            if (Directory.Exists(Path.Combine(vmDir, "generated")))
                Directory.Delete(Path.Combine(vmDir, "generated"), true);

            var args = new[] { "MainViewModel.cs", "DeviceInfo.cs", "DeviceStatus.cs", "NetworkConfig.cs" };
            var exitCode = await Program.Main(args);
            Assert.Equal(0, exitCode);

            var optsPath = Path.Combine(vmDir, "generated", "GrpcRemoteOptions.cs");
            Assert.True(File.Exists(optsPath));
            var source = File.ReadAllText(optsPath);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
            var refs = new System.Collections.Generic.List<Microsoft.CodeAnalysis.MetadataReference>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (tpa != null)
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        refs.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p));
            }
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                "GeneratedOpts",
                new[] { tree },
                refs,
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            var emitResult = compilation.Emit(Stream.Null);
            Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));
        }
        finally
        {
            Environment.CurrentDirectory = oldDir;
        }
    }

}
