using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;

namespace ToolExecution;

public class ViewModelAnalyzerTests
{
    static List<string> LoadDefaultRefs()
    {
        var list = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
        }
        return list;
    }

    static async Task<(string Name, List<PropertyInfo> Props, List<CommandInfo> Cmds)> AnalyzeAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmFile = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels", "MainViewModel.cs");
        var references = LoadDefaultRefs();
        var (sym, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            references);
        return (name, props, cmds);
    }

    [Fact]
    public async Task AnalyzerReturnsViewModelName()
    {
        var (name, _, _) = await AnalyzeAsync();
        Assert.Equal("MainViewModel", name);
    }

    [Fact]
    public async Task AnalyzerDetectsObservableProperties()
    {
        var (_, props, _) = await AnalyzeAsync();
        Assert.Single(props);
        Assert.Equal("Devices", props[0].Name);
    }

    [Fact]
    public async Task AnalyzerDetectsRelayCommands()
    {
        var (_, _, cmds) = await AnalyzeAsync();
        Assert.Single(cmds);
        Assert.Equal("UpdateStatus", cmds[0].MethodName);
    }
}

