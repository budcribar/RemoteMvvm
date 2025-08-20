using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

public class RunOptionTests
{
    [Fact]
    public async Task ServerGeneration_ConsoleMode_OmitsDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, vmNamespace, "console");
        Assert.DoesNotContain("Dispatcher", server);
    }
}
