using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace ToolExecution;

public class ServerGeneratorAdditionalBugTests
{
    private static async Task<(string Name, List<PropertyInfo> Props, List<CommandInfo> Cmds, string Namespace)> AnalyzeAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmFile = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels", "BuggyCommandViewModel.cs");
        var refs = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        return (name, props, cmds, sym?.ContainingNamespace.ToDisplayString() ?? string.Empty);
    }

    [Fact]
    public async Task CommandParameterGuid_ShouldBeParsedFromString()
    {
        var (name, props, cmds, ns) = await AnalyzeAsync();
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, ns);
        Assert.Contains("Guid.Parse", server);
    }

    [Fact]
    public async Task CommandParameterDateTime_ShouldConvertTimestamp()
    {
        var (name, props, cmds, ns) = await AnalyzeAsync();
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, ns);
        Assert.Contains(".ToDateTime()", server);
    }

    [Fact]
    public void WpfUpdatePropertyValue_ShouldUseDispatcher()
    {
        var server = ServerGenerator.Generate("SampleViewModel", "Generated.Protos", "SampleViewModelService",
            new List<PropertyInfo>(), new List<CommandInfo>(), "Generated.ViewModels", "wpf");
        Assert.Contains("_dispatcher?.Invoke", server);
    }
}
