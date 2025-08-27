using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using RemoteMvvmTool.Generators;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;

namespace ToolExecution;

public class TsProjectGeneratorTests
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

    static async Task<(string VmName, List<PropertyInfo> Props, List<CommandInfo> Cmds)> AnalyzeAsync()
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
    public async Task GenerateAppTs_ImportsServiceClient()
    {
        var (name, props, cmds) = await AnalyzeAsync();
        string ts = TsProjectGenerator.GenerateAppTs(name, name + "Service", props, cmds);
        Assert.Contains("ServiceClientPb", ts);
        Assert.Contains($"{name}RemoteClient", ts);
    }

    [Fact]
    public async Task GenerateIndexHtml_IncludesConnectionStatusDiv()
    {
        var (name, props, cmds) = await AnalyzeAsync();
        string html = TsProjectGenerator.GenerateIndexHtml(name, props, cmds);
        Assert.Contains("id='connection-status'", html);
    }

    [Fact]
    public void GeneratePackageJson_IncludesGrpcWebDependency()
    {
        string pkg = TsProjectGenerator.GeneratePackageJson("TestProject");
        Assert.Contains("\"grpc-web\"", pkg);
    }

    [Fact]
    public void GenerateTsConfig_SetsEs2020Target()
    {
        string cfg = TsProjectGenerator.GenerateTsConfig();
        Assert.Contains("\"target\": \"es2020\"", cfg);
    }

    [Fact]
    public void GenerateWebpackConfig_SpecifiesEntryPoint()
    {
        string cfg = TsProjectGenerator.GenerateWebpackConfig();
        Assert.Contains("entry: './src/app.ts'", cfg);
    }

    [Fact]
    public void GenerateReadme_ContainsSetupInstructions()
    {
        string readme = TsProjectGenerator.GenerateReadme("TestProject");
        Assert.Contains("npm run build", readme);
    }

    [Fact]
    public void GenerateAppTs_HandlesCollectionsAndComplexTypes()
    {
        var props = new List<PropertyInfo>
        {
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("TestSettings", "TestSettingsModel", null!)
        };
        string ts = TsProjectGenerator.GenerateAppTs("Vm", "VmService", props, new List<CommandInfo>());
        Assert.Contains("zoneList", ts);
        Assert.Contains("testSettings", ts);
        Assert.Contains("JSON.stringify(currentValue) !== newValue", ts);
    }
}

