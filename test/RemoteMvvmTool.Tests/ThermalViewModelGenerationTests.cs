using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using RemoteMvvmTool;

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

    static void CleanGeneratedDir(string vmDir)
    {
        var generatedDir = Path.Combine(vmDir, "generated");
        if (!Directory.Exists(generatedDir))
            return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(generatedDir))
        {
            if (Path.GetFileName(entry).Equals("tsProject", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Directory.Exists(entry))
                Directory.Delete(entry, true);
            else
                File.Delete(entry);
        }
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
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        Assert.NotNull(result.ViewModelSymbol);
        Assert.Contains(result.Properties, p => p.TypeString.Contains("TestSettingsModel"));
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
    public async Task Generated_Code_Handles_ObservableCollection_Property()
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
        };
        var refs = LoadDefaultRefs();
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);

        // Manually add an ObservableCollection-based property to ensure generators handle it
        var tzType = result.Compilation.GetTypeByMetadataName("HPSystemsTools.ViewModels.ThermalZoneComponentViewModel")!;
        var ocDef = result.Compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ObservableCollection`1")!;
        var ocType = ocDef.Construct(tzType);
        var zoneListProp = new PropertyInfo(
            "ZoneList",
            $"System.Collections.ObjectModel.ObservableCollection<{tzType.ToDisplayString()}>",
            ocType);
        var props = result.Properties.Concat(new[] { zoneListProp }).ToList();

        var client = ClientGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", props, result.Commands, string.Empty);
        Assert.Contains("new System.Collections.ObjectModel.ObservableCollection<HPSystemsTools.ViewModels.ThermalZoneComponentViewModel>(state.ZoneList.Select(ProtoStateConverters.FromProto))", client);

        var server = ServerGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", props, result.Commands, result.ViewModelSymbol!.ContainingNamespace.ToDisplayString());
        Assert.Contains("state.ZoneList.AddRange(propValue.Where(e => e != null).Select(HPSystemsTools.ViewModels.ProtoStateConverters.ToProto).Where(s => s != null))", server);

        var tsClient = TypeScriptClientGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", props, result.Commands);
        Assert.Contains("zoneList: ThermalZoneState[]", tsClient);

        var rootTypes = props.Select(p => p.FullTypeSymbol!)
            .Concat(result.Commands.SelectMany(c => c.Parameters.Select(p => p.FullTypeSymbol!)));
        var conv = ConversionGenerator.Generate("Generated.Protos", result.ViewModelSymbol!.ContainingNamespace.ToDisplayString(), rootTypes, result.Compilation);
        Assert.Contains("ThermalZoneComponentViewModelState", conv);
    }

    [Fact]
    public async Task Generators_Handle_Derived_ObservableCollections()
    {
        var code = """
public class ObservablePropertyAttribute : System.Attribute {}
public class RelayCommandAttribute : System.Attribute {}
namespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }
public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } }
public class ZoneCollection : System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel> {}
public partial class TestViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ZoneCollection Zones { get; set; } = new ZoneCollection();
}
public class ObservableObject {}
""";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, code);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");

        var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation);
        Assert.Contains("repeated ThermalZoneComponentViewModelState zones", proto);

        var client = ClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, string.Empty);
        Assert.Contains("new ZoneCollection(state.Zones", client);

        var server = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, string.Empty);
        Assert.Contains("state.Zones.AddRange(propValue.Where", server);

        var tsClient = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        Assert.Contains("zones: ThermalZoneState[]", tsClient);
    }

    [Fact]
    public async Task Generated_Server_Packs_PropertyChanges_For_Supported_Types()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "ThermalTest", "ViewModels");
        var refs = LoadDefaultRefs();

        var files = new[]
        {
            Path.Combine(vmDir, "HP3LSThermalTestViewModel.cs"),
            Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
            Path.Combine(vmDir, "ThermalStateEnum.cs"),
            Path.Combine(vmDir, "IHpMonitor.cs"),
            Path.Combine(vmDir, "TestSettingsModel.cs"),
        };

        var result = await ViewModelAnalyzer.AnalyzeAsync(
            files,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = result.ViewModelSymbol!.ContainingNamespace.ToDisplayString();
        var server = ServerGenerator.Generate(result.ViewModelName, "Generated.Protos", result.ViewModelName + "Service", result.Properties, result.Commands, vmNamespace);
        Assert.Contains("notification.NewValue = PackToAny(newValue);", server);
        Assert.Contains("private static Any PackToAny", server);
        Assert.DoesNotContain("newValue is HPSystemsTools.Models.TestSettingsModel", server);

        var tzResult = await ViewModelAnalyzer.AnalyzeAsync(
            new[]
            {
                Path.Combine(vmDir, "ThermalZoneComponentViewModel.cs"),
                Path.Combine(vmDir, "ThermalStateEnum.cs"),
                Path.Combine(vmDir, "IHpMonitor.cs"),
                Path.Combine(vmDir, "TestSettingsModel.cs"),
            },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var tzNs = tzResult.ViewModelSymbol!.ContainingNamespace.ToDisplayString();
        var tzServer = ServerGenerator.Generate(tzResult.ViewModelName, "Generated.Protos", tzResult.ViewModelName + "Service", tzResult.Properties, tzResult.Commands, tzNs);
        Assert.Contains("notification.NewValue = PackToAny(newValue);", tzServer);
        Assert.DoesNotContain("newValue is HP.Telemetry.Zone", tzServer);
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
            };
            CleanGeneratedDir(vmDir);
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
            };
            CleanGeneratedDir(vmDir);
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
