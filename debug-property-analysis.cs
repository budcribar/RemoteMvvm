using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

class DebugPropertyAnalysis
{
    static async Task Main()
    {
        var modelCode = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        [ObservableProperty]
        private ObservableCollection<string> _emptyList = new();

        [ObservableProperty]
        private Dictionary<int, string> _emptyDict = new();

        [ObservableProperty]
        private int? _nullableInt;

        [ObservableProperty]
        private double? _nullableDouble;

        [ObservableProperty]
        private List<int> _singleItemList = new();

        [ObservableProperty]
        private Dictionary<string, int> _singleItemDict = new();

        [ObservableProperty]
        private List<int> _zeroValues = new();

        [ObservableProperty]
        private bool _hasData = true;
    }
}";

        var refs = new List<string>();
        if (AppContext.GetData(""TRUSTED_PLATFORM_ASSEMBLIES"") is string tpa)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    refs.Add(p);
            }
        }

        var tempFile = Path.GetTempFileName() + "".cs"";
        File.WriteAllText(tempFile, modelCode);
        
        try
        {
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { tempFile },
                ""CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute"",
                ""CommunityToolkit.Mvvm.Input.RelayCommandAttribute"",
                refs,
                ""CommunityToolkit.Mvvm.ComponentModel.ObservableObject"");

            Console.WriteLine($""Found {props.Count} properties:"");
            
            var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
            
            Console.WriteLine($""Simple Properties ({analysis.SimpleProperties.Count}):"");
            foreach (var prop in analysis.SimpleProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                Console.WriteLine($""  - {prop.Name} ({prop.TypeString})"");
                Console.WriteLine($""    IsNonNullableValueType: {metadata.IsNonNullableValueType}"");
                Console.WriteLine($""    RequiresNullCheck: {metadata.RequiresNullCheck}"");
                Console.WriteLine($""    SafeVariableName: {metadata.SafeVariableName}"");
                Console.WriteLine($""    SafePropertyAccess: {metadata.SafePropertyAccess}"");
            }
            
            Console.WriteLine($""Boolean Properties ({analysis.BooleanProperties.Count}):"");
            foreach (var prop in analysis.BooleanProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                Console.WriteLine($""  - {prop.Name} ({prop.TypeString})"");
                Console.WriteLine($""    IsNonNullableValueType: {metadata.IsNonNullableValueType}"");
                Console.WriteLine($""    RequiresNullCheck: {metadata.RequiresNullCheck}"");
            }
            
            Console.WriteLine($""Collection Properties ({analysis.CollectionProperties.Count}):"");
            foreach (var prop in analysis.CollectionProperties)
            {
                Console.WriteLine($""  - {prop.Name} ({prop.TypeString})"");
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}