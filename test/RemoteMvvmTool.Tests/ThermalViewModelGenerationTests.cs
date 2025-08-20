using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

namespace ThermalTests;

public class ThermalViewModelGenerationTests
{
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
    public async Task Analyzer_Finds_Types_From_Other_Files()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "HP3LSThermalTestViewModel.cs");
        var additionalFiles = new[]
        {
            Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
            Path.Combine(vmDir, "ThermalStateEnum.cs"),
            Path.Combine(vmDir, "IHpMonitor.cs"),
            Path.Combine(vmDir, "TestSettingsModel.cs"),
            Path.Combine(vmDir, "Zone.cs")
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        Assert.NotNull(result.ViewModelSymbol);
        Assert.Contains(result.Properties, p => p.TypeString.Contains("ThermalZoneComponentViewModel"));
        Assert.Contains(result.Commands, c => c.MethodName == "StateChanged" && c.Parameters.Any(pr => pr.TypeString == "HPSystemsTools.Models.ThermalStateEnum"));
    }

    [Fact]
    public async Task Generated_Server_Uses_Correct_Enum_Namespace()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "HP3LSThermalTestViewModel.cs");
        var additionalFiles = new[]
        {
            Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
            Path.Combine(vmDir, "ThermalStateEnum.cs"),
            Path.Combine(vmDir, "IHpMonitor.cs"),
            Path.Combine(vmDir, "TestSettingsModel.cs"),
            Path.Combine(vmDir, "Zone.cs")
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = result.ViewModelSymbol!.ContainingNamespace.ToDisplayString();
        var server = RemoteMvvmTool.Generators.ServerGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", result.Properties, result.Commands, vmNamespace);
        Assert.Contains("IRelayCommand<HPSystemsTools.Models.ThermalStateEnum>", server);
        Assert.Contains("(HPSystemsTools.Models.ThermalStateEnum)request.State", server);
    }

    [Fact]
    public async Task Generated_Client_Handles_Dictionary_Property()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "HP3LSThermalTestViewModel.cs");
        var additionalFiles = new[]
        {
            Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
            Path.Combine(vmDir, "ThermalStateEnum.cs"),
            Path.Combine(vmDir, "IHpMonitor.cs"),
            Path.Combine(vmDir, "TestSettingsModel.cs"),
            Path.Combine(vmDir, "Zone.cs")
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var client = ClientGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", result.Properties, result.Commands, string.Empty);
        Assert.Contains("ToDictionary(k => (HP.Telemetry.Zone)k.Key, v => ProtoStateConverters.FromProto(v.Value))", client);
        var rootTypes = result.Properties.Select(p => p.FullTypeSymbol!)
            .Concat(result.Commands.SelectMany(c => c.Parameters.Select(p => p.FullTypeSymbol!)));
        var conv = ConversionGenerator.Generate("Generated.Protos", result.ViewModelSymbol!.ContainingNamespace.ToDisplayString(), rootTypes, result.Compilation);
        Assert.Contains("ThermalZoneComponentViewModelState", conv);
    }

    [Fact]
    public async Task Generated_Converters_Handle_Unresolved_Enum_As_Int()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "HP3LSThermalTestViewModel.cs");
        // Intentionally omit Zone.cs so the Zone type is unresolved
        var additionalFiles = new[]
        {
            Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
            Path.Combine(vmDir, "ThermalStateEnum.cs"),
            Path.Combine(vmDir, "IHpMonitor.cs"),
            Path.Combine(vmDir, "TestSettingsModel.cs")
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var rootTypes = result.Properties.Select(p => p.FullTypeSymbol!)
            .Concat(result.Commands.SelectMany(c => c.Parameters.Select(p => p.FullTypeSymbol!)));
        var conv = ConversionGenerator.Generate("Generated.Protos", result.ViewModelSymbol!.ContainingNamespace.ToDisplayString(), rootTypes, result.Compilation);
        Assert.Contains("state.Zone = (int)model.Zone", conv);
        Assert.Contains("model.Zone = (HP.Telemetry.Zone)state.Zone", conv);
        Assert.DoesNotContain("ZoneState", conv);
    }

    [Fact]
    public async Task RemoteMvvmTool_Generates_Code_Successfully()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var oldDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = vmDir;
            var args = new[]
            {
                "HP3LSThermalTestViewModel.cs",
                "ThermalZoneComponentViewModel.cs",
                "ThermalStateEnum.cs",
                "IHpMonitor.cs",
                "TestSettingsModel.cs",
                "Zone.cs"
            };
            if (Directory.Exists(Path.Combine(vmDir, "generated")))
                Directory.Delete(Path.Combine(vmDir, "generated"), true);
            if (Directory.Exists(Path.Combine(vmDir, "protos")))
                Directory.Delete(Path.Combine(vmDir, "protos"), true);

            var exitCode = await Program.Main(args);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = oldDir;
        }
    }

    [Fact]
    public async Task Generated_Code_Omits_Static_Properties()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var oldDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = vmDir;
            var args = new[]
            {
                "HP3LSThermalTestViewModel.cs",
                "ThermalZoneComponentViewModel.cs",
                "ThermalStateEnum.cs",
                "IHpMonitor.cs",
                "TestSettingsModel.cs",
                "Zone.cs"
            };
            if (Directory.Exists(Path.Combine(vmDir, "generated")))
                Directory.Delete(Path.Combine(vmDir, "generated"), true);
            if (Directory.Exists(Path.Combine(vmDir, "protos")))
                Directory.Delete(Path.Combine(vmDir, "protos"), true);

            var exitCode = await Program.Main(args);
            Assert.Equal(0, exitCode);

            var protoFile = Path.Combine(vmDir, "protos", "HP3LSThermalTestViewModelService.proto");
            var protoText = File.ReadAllText(protoFile);
            Assert.DoesNotContain("dts", protoText, StringComparison.OrdinalIgnoreCase);

            var converterFile = Path.Combine(vmDir, "generated", "ProtoStateConverters.cs");
            var converterText = File.ReadAllText(converterFile);
            Assert.DoesNotContain("DTS", converterText);
        }
        finally
        {
            Environment.CurrentDirectory = oldDir;
        }
    }
}
