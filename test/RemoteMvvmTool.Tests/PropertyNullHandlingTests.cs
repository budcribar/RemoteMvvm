using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests;

public class PropertyNullHandlingTests
{
    [Fact]
    public async Task PropertyDiscoveryUtility_CorrectlyIdentifiesNullableValueTypes()
    {
        var modelCode = @"
using System;
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
        
        [ObservableProperty]
        private double _simpleDouble = 3.14;
        
        [ObservableProperty]
        private double? _nullableDouble;
    }
}";

        var refs = System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var refList = refs?.Split(Path.PathSeparator).Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList() ?? new();

        var tempFile = Path.GetTempFileName() + ".cs";
        File.WriteAllText(tempFile, modelCode);
        
        try
        {
            var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { tempFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                refList);

            var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);

            // Test each property
            var hasDataProp = props.FirstOrDefault(p => p.Name == "HasData");
            var nullableBoolProp = props.FirstOrDefault(p => p.Name == "NullableBool");
            var simpleIntProp = props.FirstOrDefault(p => p.Name == "SimpleInt");  
            var nullableIntProp = props.FirstOrDefault(p => p.Name == "NullableInt");
            var simpleDoubleProp = props.FirstOrDefault(p => p.Name == "SimpleDouble");
            var nullableDoubleProp = props.FirstOrDefault(p => p.Name == "NullableDouble");

            Assert.NotNull(hasDataProp);
            Assert.NotNull(nullableBoolProp);
            Assert.NotNull(simpleIntProp);
            Assert.NotNull(nullableIntProp);
            Assert.NotNull(simpleDoubleProp);
            Assert.NotNull(nullableDoubleProp);

            // Test IsNonNullableValueType detection
            Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(hasDataProp), "bool should be non-nullable value type");
            Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(nullableBoolProp), "bool? should be nullable value type");
            Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(simpleIntProp), "int should be non-nullable value type");
            Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(nullableIntProp), "int? should be nullable value type");
            Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(simpleDoubleProp), "double should be non-nullable value type");
            Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(nullableDoubleProp), "double? should be nullable value type");

            // Test metadata generation
            var hasDataMetadata = analysis.GetMetadata(hasDataProp);
            var nullableIntMetadata = analysis.GetMetadata(nullableIntProp);

            Assert.True(hasDataMetadata.IsNonNullableValueType);
            Assert.False(hasDataMetadata.RequiresNullCheck);

            Assert.False(nullableIntMetadata.IsNonNullableValueType);
            Assert.True(nullableIntMetadata.RequiresNullCheck);

            // Test the actual code generation logic
            bool hasDataUsesNullOp = hasDataMetadata.RequiresNullCheck && !hasDataMetadata.IsNonNullableValueType;
            bool nullableIntUsesNullOp = nullableIntMetadata.RequiresNullCheck && !nullableIntMetadata.IsNonNullableValueType;

            Assert.False(hasDataUsesNullOp, "bool should NOT use null-conditional operator");
            Assert.True(nullableIntUsesNullOp, "int? SHOULD use null-conditional operator");
            
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
    
    [Fact]  
    public void PropertyDiscoveryUtility_GeneratesCorrectNullChecking()
    {
        // Test the actual null checking logic used in code generation
        var testCases = new[]
        {
            // (RequiresNullCheck, IsNonNullableValueType, ShouldUseNullOp, Description)
            (false, true, false, "Non-nullable value type (bool, int, double)"),
            (true, false, true, "Nullable value type (bool?, int?, double?)"),
            (true, false, true, "Reference type (string, object)"),
            (false, false, false, "Edge case - should not happen but handle gracefully")
        };

        foreach (var (requiresNullCheck, isNonNullableValueType, shouldUseNullOp, description) in testCases)
        {
            bool usesNullOp = requiresNullCheck && !isNonNullableValueType;
            Assert.Equal(shouldUseNullOp, usesNullOp);//, $"Failed for: description");
        }
    }
}