using System;
using System.Collections.Generic;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// Unit tests for PropertyDiscoveryUtility structured data analysis
/// </summary>
public class PropertyDiscoveryUtilityTests
{
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
        var prop = new PropertyInfo("ImageData", "byte[]", null);
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Length", metadata.CountProperty);
        Assert.True(metadata.IsArrayType);
        Assert.Equal("imagedata", metadata.SafeVariableName);
        Assert.Equal("ImageData", metadata.SafePropertyAccess);
    }

    [Fact]
    public void AnalyzePropertyMetadata_SystemByteArray_UsesLengthProperty()
    {
        var prop = new PropertyInfo("Data", "System.Byte[]", null);
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Length", metadata.CountProperty);
        Assert.True(metadata.IsArrayType);
    }

    [Fact]
    public void AnalyzePropertyMetadata_List_UsesCountProperty()
    {
        var prop = new PropertyInfo("Items", "List<string>", null);
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Count", metadata.CountProperty);
        Assert.False(metadata.IsArrayType);
    }

    [Fact]
    public void AnalyzePropertyMetadata_ObservableCollection_UsesCountProperty()
    {
        var prop = new PropertyInfo("Items", "ObservableCollection<int>", null);
        var metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
        
        Assert.Equal("Count", metadata.CountProperty);
        Assert.False(metadata.IsArrayType);
    }

    [Fact]
    public void AnalyzeProperties_BooleanProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            new("IsEnabled", "bool", null),
            new("IsVisible", "bool?", null)
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(2, analysis.BooleanProperties.Count);
        Assert.Empty(analysis.SimpleProperties);
        
        var enabledMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Boolean", enabledMetadata.TypeCategory);
        Assert.Equal("CheckBox", enabledMetadata.UIHints.DefaultControlType);
        Assert.True(enabledMetadata.IsNonNullableValueType);
    }

    [Fact]
    public void AnalyzeProperties_EnumProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            new("Status", "Status", null),
            new("GameMode", "GameMode", null)
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(2, analysis.EnumProperties.Count);
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
            new("Items", "List<string>", null),
            new("Data", "byte[]", null),
            new("Numbers", "ObservableCollection<int>", null)
        };
        
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        Assert.Equal(3, analysis.CollectionProperties.Count);
        Assert.Empty(analysis.SimpleProperties);
        
        // Test array metadata
        var dataMetadata = analysis.GetMetadata(props[1]);
        Assert.Equal("Collection", dataMetadata.TypeCategory);
        Assert.Equal("Length", dataMetadata.CountProperty);
        Assert.Equal("TreeView", dataMetadata.UIHints.DefaultControlType);
        Assert.False(dataMetadata.UIHints.ShowInPropertyEditor);
        
        // Test list metadata
        var itemsMetadata = analysis.GetMetadata(props[0]);
        Assert.Equal("Count", itemsMetadata.CountProperty);
    }

    [Fact]
    public void AnalyzeProperties_SimpleProperty_CategorizesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            new("Name", "string", null),
            new("Age", "int", null),
            new("Score", "double", null)
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
            new("Buffer", "Memory<byte>", null),
            new("ReadOnlyBuffer", "ReadOnlyMemory<byte>", null)
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
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("int"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("double"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("bool"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("DateTime"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("Guid"));
    }

    [Fact]
    public void IsNonNullableValueType_ReferenceTypes_ReturnsFalse()
    {
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType("string"));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType("object"));
        Assert.False(PropertyDiscoveryUtility.IsNonNullableValueType("List<int>"));
    }

    [Fact]
    public void IsNonNullableValueType_MemoryTypes_ReturnsTrue()
    {
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("Memory<byte>"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("ReadOnlyMemory<int>"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("Span<char>"));
        Assert.True(PropertyDiscoveryUtility.IsNonNullableValueType("ReadOnlySpan<byte>"));
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
        var prop = new PropertyInfo("IsEnabled", "bool", null);
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("CheckBox", hints.DefaultControlType);
        Assert.True(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_EnumProperty_ReturnsComboBox()
    {
        var prop = new PropertyInfo("Status", "Status", null);
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("ComboBox", hints.DefaultControlType);
        Assert.True(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_CollectionProperty_ReturnsTreeViewAndHidesInEditor()
    {
        var prop = new PropertyInfo("Items", "List<string>", null);
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.Equal("TreeView", hints.DefaultControlType);
        Assert.False(hints.ShowInPropertyEditor);
    }

    [Fact]
    public void GetUIHints_ReadOnlyProperty_RecommendedReadOnly()
    {
        var prop = new PropertyInfo("ReadOnlyValue", "string", null, true);
        var hints = PropertyDiscoveryUtility.GetUIHints(prop);
        
        Assert.True(hints.IsReadOnlyRecommended);
    }

    [Fact]
    public void PropertyAnalysis_Metadata_CachesCorrectly()
    {
        var props = new List<PropertyInfo>
        {
            new("TestProp", "string", null)
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
            new("TestProp", "string", null)
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