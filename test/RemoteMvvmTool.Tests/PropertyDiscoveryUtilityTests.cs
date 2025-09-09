using System;
using System.Collections.Generic;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Unit tests for PropertyDiscoveryUtility structured data analysis using Roslyn
/// </summary>
public class PropertyDiscoveryUtilityTests
{
    // Helper method to create a PropertyInfo with proper ITypeSymbol for testing
    private static PropertyInfo CreatePropertyInfo(string name, string typeCode, bool isReadOnly = false)
    {
        try
        {
            // Create a comprehensive compilation as a library (not executable) to avoid Main method requirement
            var compilation = CSharpCompilation.Create(
                "TestLibrary", 
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.ObjectModel.ObservableCollection<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.IEnumerable).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location)); // System.Runtime

            // Create a simple syntax tree for library compilation without complex interfaces
            var sourceCode = $@"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TestNamespace
{{
    public class TestClass
    {{
        public {typeCode} {name} {{ get; {(isReadOnly ? "" : "set;")} }}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            compilation = compilation.AddSyntaxTrees(syntaxTree);

            // Check for compilation errors (excluding warnings)
            var diagnostics = compilation.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()));
                throw new InvalidOperationException($"Compilation errors for {typeCode}: {errorMessages}");
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            
            // Find the property declaration
            var propertyDeclaration = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == name);

            if (propertyDeclaration == null)
            {
                throw new InvalidOperationException($"Could not find property declaration for {name}");
            }

            // Get the type symbol with full semantic analysis
            var typeInfo = semanticModel.GetTypeInfo(propertyDeclaration.Type);
            var typeSymbol = typeInfo.Type;
            
            if (typeSymbol == null)
            {
                throw new InvalidOperationException($"Could not resolve type symbol for {typeCode} in property {name}. TypeInfo returned null.");
            }
            
            return new PropertyInfo(name, typeCode, typeSymbol, isReadOnly);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create PropertyInfo for {name} ({typeCode}): {ex.Message}", ex);
        }
    }

    [Fact]
    public void AnalyzeProperties_EmptyList_ReturnsEmptyAnalysis()
    {
        var props = new List<PropertyInfo>();
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Empty(analysis.SimpleProperties);
        Assert.Empty(analysis.BooleanProperties);
        Assert.Empty(analysis.EnumProperties);
        Assert.Empty(analysis.CollectionProperties);
        Assert.Empty(analysis.ComplexProperties);
    }

    [Fact]
    public void AnalyzePropertyMetadata_ByteArray_UsesLengthProperty()
    {
        var prop = CreatePropertyInfo("ImageData", "byte[]");
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Length", metadata.CountProperty);
        Assert.True(metadata.IsArrayType);
        Assert.Equal("imagedata", metadata.SafeVariableName);
        Assert.Equal("ImageData", metadata.SafePropertyAccess);
    }

    [Fact]
    public void AnalyzePropertyMetadata_List_UsesCountProperty()
    {
        var prop = CreatePropertyInfo("Items", "List<string>");
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Count", metadata.CountProperty);
        Assert.False(metadata.IsArrayType);
    }

    [Fact]
    public void AnalyzePropertyMetadata_ObservableCollection_UsesCountProperty()
    {
        var prop = CreatePropertyInfo("Items", "ObservableCollection<int>");
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Count", metadata.CountProperty);
        Assert.False(metadata.IsArrayType);
    }

    [Fact]
    public void AnalyzeProperties_BooleanProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("IsEnabled", "bool"),
            CreatePropertyInfo("IsVisible", "bool?")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(2, analysis.BooleanProperties.Count);
        Assert.Empty(analysis.SimpleProperties);
        
        var enabledMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Boolean", enabledMetadata.TypeCategory);
        Assert.Equal("CheckBox", enabledMetadata.UIHints.DefaultControlType);
        Assert.True(enabledMetadata.IsNonNullableValueType);

        var visibleMetadata = analysis.GetMetadata(props[1]);
        Assert.Equal("Boolean", visibleMetadata.TypeCategory);
        Assert.False(visibleMetadata.IsNonNullableValueType); // nullable bool
    }

    [Fact]
    public void AnalyzeProperties_EnumProperty_CategorizesCorrectly()
    {
        // Create enum types for testing
        var statusProp = CreatePropertyInfo("Status", "System.DayOfWeek"); // Built-in enum
        var props = new List<PropertyInfo> { statusProp };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Single(analysis.EnumProperties);
        Assert.Empty(analysis.SimpleProperties);
        
        var statusMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Enum", statusMetadata.TypeCategory);
        Assert.Equal("ComboBox", statusMetadata.UIHints.DefaultControlType);
        Assert.True(statusMetadata.IsNonNullableValueType); // Enums are value types
    }

    [Fact]
    public void AnalyzeProperties_CollectionProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("Items", "List<string>"),
            CreatePropertyInfo("Data", "byte[]"),
            CreatePropertyInfo("Numbers", "ObservableCollection<int>")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        // Check each property individually for better diagnostics
        var itemsMetadata = analysis.GetMetadata(props[0]);
        var dataMetadata = analysis.GetMetadata(props[1]);
        var numbersMetadata = analysis.GetMetadata(props[2]);
        
        // If this fails, we'll get specific information about which property failed
        Assert.True(itemsMetadata.TypeCategory == "Collection", $"Items should be Collection but was {itemsMetadata.TypeCategory}");
        Assert.True(dataMetadata.TypeCategory == "Collection", $"Data should be Collection but was {dataMetadata.TypeCategory}");
        Assert.True(numbersMetadata.TypeCategory == "Collection", $"Numbers should be Collection but was {numbersMetadata.TypeCategory}");
        
        Assert.Equal(3, analysis.CollectionProperties.Count);
        Assert.Empty(analysis.SimpleProperties);
        
        // Test array metadata
        Assert.Equal("Collection", dataMetadata.TypeCategory);
        Assert.Equal("Length", dataMetadata.CountProperty);
        Assert.Equal("TreeView", dataMetadata.UIHints.DefaultControlType);
        Assert.False(dataMetadata.UIHints.ShowInPropertyEditor);
        
        // Test list metadata
        Assert.Equal("Count", itemsMetadata.CountProperty);
    }

    [Fact]
    public void AnalyzeProperties_SimpleProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("Name", "string"),
            CreatePropertyInfo("Age", "int"),
            CreatePropertyInfo("Score", "double")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(3, analysis.SimpleProperties.Count);
        Assert.Empty(analysis.CollectionProperties);
        
        var nameMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Simple", nameMetadata.TypeCategory);
        Assert.Equal("TextBox", nameMetadata.UIHints.DefaultControlType);
        Assert.False(nameMetadata.IsNonNullableValueType); // string is nullable
        
        var ageMetadata = analysis.GetMetadata(props[1]);
        Assert.True(ageMetadata.IsNonNullableValueType); // int is non-nullable value type
    }

    [Fact]
    public void AnalyzeProperties_MemoryType_TreatedAsSimple()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("Buffer", "Memory<byte>"),
            CreatePropertyInfo("ReadOnlyBuffer", "ReadOnlyMemory<byte>")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(2, analysis.SimpleProperties.Count);
        Assert.Empty(analysis.CollectionProperties);
        
        var bufferMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Simple", bufferMetadata.TypeCategory);
        Assert.True(bufferMetadata.UIHints.IsReadOnlyRecommended);
        Assert.Equal("Hex", bufferMetadata.UIHints.DisplayFormat);
    }

    [Fact]
    public void IsNonNullableValueType_BasicValueTypes_ReturnsTrue()
    {
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestInt", "int")));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestDouble", "double")));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestBool", "bool")));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestDateTime", "DateTime")));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestGuid", "Guid")));
    }

    [Fact]
    public void IsNonNullableValueType_ReferenceTypes_ReturnsFalse()
    {
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestString", "string")));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestObject", "object")));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestList", "List<int>")));
    }

    [Fact]
    public void IsNonNullableValueType_NullableValueTypes_ReturnsFalse()
    {
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestNullableInt", "int?")));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestNullableBool", "bool?")));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestNullableDateTime", "DateTime?")));
    }

    [Fact]
    public void IsNonNullableValueType_MemoryTypes_ReturnsTrue()
    {
        // Note: Span<T> and ReadOnlySpan<T> cannot be used as auto-implemented properties
        // due to C# language constraints (they must be ref struct members), so we test only Memory types
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestMemory", "Memory<byte>")));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestReadOnlyMemory", "ReadOnlyMemory<int>")));
        
        // These would fail compilation as properties, so we skip them:
        // Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestSpan", "Span<char>")));
        // Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType(CreatePropertyInfo("TestReadOnlySpan", "ReadOnlySpan<byte>")));
    }

    [Fact]
    public void IsArrayType_Arrays_ReturnsTrue()
    {
        Assert.True(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestByteArray", "byte[]")));
        Assert.True(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestStringArray", "string[]")));
        Assert.True(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestIntArray", "int[]")));
    }

    [Fact]
    public void IsArrayType_NonArrays_ReturnsFalse()
    {
        Assert.False(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestList", "List<string>")));
        Assert.False(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestString", "string")));
        Assert.False(PropertyDiscoveryUtility.IsArrayType(CreatePropertyInfo("TestInt", "int")));
    }

    [Fact]
    public void MakeSafeVariableName_CSharpKeywords_AddsAtSymbol()
    {
        Assert.Equal("@string", PropertyDiscoveryUtility.MakeSafeVariableName("string"));
        Assert.Equal("@int", PropertyDiscoveryUtility.MakeSafeVariableName("int"));
        Assert.Equal("@class", PropertyDiscoveryUtility.MakeSafeVariableName("class"));
        Assert.Equal("@new", PropertyDiscoveryUtility.MakeSafeVariableName("new"));
    }

    [Fact]
    public void MakeSafeVariableName_ValidIdentifiers_ReturnsUnchanged()
    {
        Assert.Equal("myVariable", PropertyDiscoveryUtility.MakeSafeVariableName("myVariable"));
        Assert.Equal("_private", PropertyDiscoveryUtility.MakeSafeVariableName("_private"));
        Assert.Equal("Property1", PropertyDiscoveryUtility.MakeSafeVariableName("Property1"));
    }

    [Fact]
    public void MakeSafeVariableName_StartsWithNumber_AddsUnderscore()
    {
        Assert.Equal("_123abc", PropertyDiscoveryUtility.MakeSafeVariableName("123abc"));
        Assert.Equal("_1stProperty", PropertyDiscoveryUtility.MakeSafeVariableName("1stProperty"));
    }

    [Fact]
    public void GetUIHints_BooleanProperty_ReturnsCheckBox()
    {
        var prop = CreatePropertyInfo("IsEnabled", "bool");
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("CheckBox", hints.DefaultControlType);
        Assert.True(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_EnumProperty_ReturnsComboBox()
    {
        var prop = CreatePropertyInfo("Status", "System.DayOfWeek");
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("ComboBox", hints.DefaultControlType);
        Assert.True(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_CollectionProperty_ReturnsTreeViewAndHidesInEditor()
    {
        var prop = CreatePropertyInfo("Items", "List<string>");
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("TreeView", hints.DefaultControlType);
        Assert.False(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_ReadOnlyProperty_RecommendedReadOnly()
    {
        var prop = CreatePropertyInfo("ReadOnlyValue", "string", true);
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.True(hints.IsReadOnlyRecommended);
    }

    [Fact]
    public void PropertyAnalysis_Metadata_CachesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("TestProp", "string")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        // Get metadata twice - should return same cached instance
        var metadata1 = analysis.GetMetadata(props[0]);
        var metadata2 = analysis.GetMetadata(props[0]);
        
        Assert.Same(metadata1, metadata2);
        Assert.Equal("TestProp", metadata1.DisplayName);
    }

    [Fact]
    public void PropertyAnalysis_SetMetadata_AllowsCustomMetadata()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("TestProp", "string")
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        var customMetadata = new PropertyMetadata
        {
            DisplayName = "Custom Display Name",
            CountProperty = "CustomCount"
        };
        
        analysis.SetMetadata(props[0], customMetadata);
        var retrievedMetadata = analysis.GetMetadata(props[0]);
        
        Assert.Equal("Custom Display Name", retrievedMetadata.DisplayName);
        Assert.Equal("CustomCount", retrievedMetadata.CountProperty);
    }
}