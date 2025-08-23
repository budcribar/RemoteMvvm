using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

public class DebugAnalyzer
{
    static async Task Main(string[] args)
    {
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }

        var vmFile = "HP3LSThermalTestViewModel.cs";
        var additionalFiles = new[]
        {
            "ThermalZoneComponentViewModel.cs",
            "ThermalStateEnum.cs",
            "IHpMonitor.cs",
            "TestSettingsModel.cs",
        };
        var allFiles = (new[] { vmFile }).Concat(additionalFiles).ToArray();
        
        var result = await ViewModelAnalyzer.AnalyzeAsync(
            allFiles,
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
            
        Console.WriteLine($"Found {result.Commands.Count} commands:");
        foreach (var cmd in result.Commands)
        {
            Console.WriteLine($"  Command: {cmd.MethodName}");
            foreach (var param in cmd.Parameters)
            {
                Console.WriteLine($"    Parameter: {param.Name} ({param.TypeString})");
            }
        }
    }
}