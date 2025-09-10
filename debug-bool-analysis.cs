using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

class DebugBoolPropertyAnalysis
{
    static async Task Main()
    {
        Console.WriteLine("=== Debugging Bool Property Analysis ===");
        
        var modelCode = @"using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        [ObservableProperty]
        private bool _hasData = true;
        
        [ObservableProperty]
        private bool? _nullableBool;
        
        [ObservableProperty]
        private int _simpleInt = 42;
        
        [ObservableProperty]
        private int? _nullableInt;
    }
}";

        var refs = System.AppContext.GetData(""TRUSTED_PLATFORM_ASSEMBLIES"") as string;
        var refList = refs?.Split(Path.PathSeparator).Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList() ?? new();

        var tempFile = Path.GetTempFileName() + "".cs"";
        File.WriteAllText(tempFile, modelCode);
        
        try
        {
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { tempFile },
                ""CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute"",
                ""CommunityToolkit.Mvvm.Input.RelayCommandAttribute"",
                refList);

            Console.WriteLine($""Found {props.Count} properties:"");
            
            var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
            
            Console.WriteLine($""\\nSimple Properties ({analysis.SimpleProperties.Count}):"");
            foreach (var prop in analysis.SimpleProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                Console.WriteLine($""  Property: {prop.Name} (Type: {prop.TypeString})"");
                Console.WriteLine($""    IsNonNullableValueType: {metadata.IsNonNullableValueType}"");
                Console.WriteLine($""    RequiresNullCheck: {metadata.RequiresNullCheck}"");
                Console.WriteLine($""    IsValueType (Roslyn): {prop.FullTypeSymbol?.IsValueType}"");
                Console.WriteLine($""    SpecialType: {prop.FullTypeSymbol?.SpecialType}"");
                Console.WriteLine($""    Generated code would use: {(metadata.RequiresNullCheck && !metadata.IsNonNullableValueType ? ""?.ToString()"" : "".ToString()"")}"");
                Console.WriteLine();
            }
            
            Console.WriteLine($""Boolean Properties ({analysis.BooleanProperties.Count}):"");
            foreach (var prop in analysis.BooleanProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                Console.WriteLine($""  Property: {prop.Name} (Type: {prop.TypeString})"");
                Console.WriteLine($""    IsNonNullableValueType: {metadata.IsNonNullableValueType}"");
                Console.WriteLine($""    RequiresNullCheck: {metadata.RequiresNullCheck}"");
                Console.WriteLine($""    IsValueType (Roslyn): {prop.FullTypeSymbol?.IsValueType}"");
                Console.WriteLine($""    SpecialType: {prop.FullTypeSymbol?.SpecialType}"");
                Console.WriteLine($""    Generated code would use: {(metadata.RequiresNullCheck && !metadata.IsNonNullableValueType ? ""?.ToString()"" : "".ToString()"")}"");
                Console.WriteLine();
            }
            
            // Test the displayProps logic
            var displayProps = analysis.SimpleProperties.Concat(analysis.BooleanProperties)
                                      .Concat(analysis.EnumProperties).Take(5);
            Console.WriteLine($""\\nDisplay Properties (first 5):"");
            foreach (var prop in displayProps)
            {
                var metadata = analysis.GetMetadata(prop);
                Console.WriteLine($""  {prop.Name}: Generated = {(metadata.RequiresNullCheck && !metadata.IsNonNullableValueType ? ""vm."" + metadata.SafePropertyAccess + ""?.ToString()"" : ""vm."" + metadata.SafePropertyAccess + "".ToString()"")}"");
            }
            
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}