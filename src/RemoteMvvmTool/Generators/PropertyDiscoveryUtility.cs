using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Utility class for discovering properties at generation time and generating appropriate UI code
/// based on the actual properties found in the view model using Roslyn compiler analysis
/// </summary>
public static class PropertyDiscoveryUtility
{
    /// <summary>
    /// Analyzes a list of properties and categorizes them for UI generation
    /// </summary>
    public static PropertyAnalysis AnalyzeProperties(List<PropertyInfo> props)
    {
        var analysis = new PropertyAnalysis();
        
        foreach (var prop in props)
        {
            // Generate metadata for each property
            var metadata = AnalyzePropertyMetadata(prop);
            analysis.SetMetadata(prop, metadata);
            
            if (IsMemoryType(prop))
            {
                // Memory<T> and similar types should be treated as simple properties for UI purposes
                analysis.SimpleProperties.Add(prop);
                metadata.TypeCategory = "Simple";
            }
            else if (IsCollectionType(prop))
            {
                analysis.CollectionProperties.Add(prop);
                metadata.TypeCategory = "Collection";
            }
            else if (IsBooleanType(prop))
            {
                analysis.BooleanProperties.Add(prop);
                metadata.TypeCategory = "Boolean";
            }
            else if (IsEnumType(prop))
            {
                analysis.EnumProperties.Add(prop);
                metadata.TypeCategory = "Enum";
            }
            else if (IsComplexType(prop))
            {
                analysis.ComplexProperties.Add(prop);
                metadata.TypeCategory = "Complex";
            }
            else
            {
                analysis.SimpleProperties.Add(prop);
                metadata.TypeCategory = "Simple";
            }
        }
        
        return analysis;
    }

    /// <summary>
    /// Analyzes metadata for a single property using Roslyn type information
    /// </summary>
    public static PropertyMetadata AnalyzePropertyMetadata(PropertyInfo prop)
    {
        var metadata = new PropertyMetadata
        {
            SafeVariableName = MakeSafeVariableName(prop.Name.ToLower()),
            SafePropertyAccess = MakeSafePropertyAccess(prop.Name),
            DisplayName = prop.Name
        };

        // Determine if this is a non-nullable value type using Roslyn analysis
        metadata.IsNonNullableValueType = IsNonNullableValueType(prop);
        metadata.RequiresNullCheck = !metadata.IsNonNullableValueType;

        // Determine count property for collections using Roslyn analysis
        metadata.IsArrayType = IsArrayType(prop);
        metadata.CountProperty = metadata.IsArrayType ? "Length" : "Count";

        // Set UI hints
        metadata.UIHints = GetUIHints(prop);

        return metadata;
    }

    /// <summary>
    /// Gets UI hints for a property using Roslyn type information
    /// </summary>
    public static UIHints GetUIHints(PropertyInfo prop)
    {
        var hints = new UIHints
        {
            IsReadOnlyRecommended = prop.IsReadOnly
        };

        if (IsBooleanType(prop))
        {
            hints.DefaultControlType = "CheckBox";
        }
        else if (IsEnumType(prop))
        {
            hints.DefaultControlType = "ComboBox";
        }
        else if (IsCollectionType(prop))
        {
            hints.DefaultControlType = "TreeView";
            hints.ShowInPropertyEditor = false; // Collections better shown in tree
        }
        else if (IsComplexType(prop))
        {
            hints.DefaultControlType = "TreeView";
            hints.ShowInPropertyEditor = false; // Complex objects better shown in tree
        }
        else
        {
            hints.DefaultControlType = "TextBox";
        }

        // Memory types should be read-only in UI
        if (IsMemoryType(prop))
        {
            hints.IsReadOnlyRecommended = true;
            hints.DisplayFormat = "Hex";
        }

        return hints;
    }

    /// <summary>
    /// Checks if a property represents a non-nullable value type using Roslyn analysis
    /// </summary>
    public static bool IsNonNullableValueType(PropertyInfo prop)
    {
        // Use Roslyn's definitive type analysis instead of string guessing
        if (!prop.FullTypeSymbol.IsValueType)
        {
            return false; // Reference types are never non-nullable value types
        }
        
        // For value types, check if it's not a nullable value type (e.g., int? is nullable)
        if (prop.FullTypeSymbol is INamedTypeSymbol namedType && 
            namedType.IsGenericType && 
            namedType.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
        {
            return false; // This is a nullable value type like int?, bool?, etc.
        }
        
        // It's a non-nullable value type (int, bool, DateTime, Guid, Memory<T>, etc.)
        return true;
    }

    /// <summary>
    /// Checks if a property represents an array type using Roslyn analysis
    /// </summary>
    public static bool IsArrayType(PropertyInfo prop)
    {
        // Use Roslyn's definitive type analysis
        return prop.FullTypeSymbol.TypeKind == TypeKind.Array;
    }

    /// <summary>
    /// Checks if a property represents a collection type using Roslyn analysis
    /// </summary>
    private static bool IsCollectionType(PropertyInfo prop)
    {
        // String is technically an IEnumerable, but we don't treat it as a collection for UI purposes
        if (prop.FullTypeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        // Check for arrays first
        if (prop.FullTypeSymbol.TypeKind == TypeKind.Array)
        {
            return true;
        }

        // Check if the type implements IEnumerable (covers lists, collections, etc.)
        // Note: We need to check both the non-generic and generic versions
        var allInterfaces = prop.FullTypeSymbol.AllInterfaces;
        
        if (allInterfaces.Any(i => 
            i.MetadataName == "IEnumerable" ||     // System.Collections.IEnumerable
            i.MetadataName == "IEnumerable`1" ||   // System.Collections.Generic.IEnumerable<T>
            i.MetadataName == "ICollection`1" ||   // System.Collections.Generic.ICollection<T>
            i.MetadataName == "IList`1"))          // System.Collections.Generic.IList<T>
        {
            return true;
        }

        // Fallback: Check for well-known collection types by name
        // This helps when interface resolution fails in test environments
        var typeName = prop.FullTypeSymbol.MetadataName;
        var namespaceName = prop.FullTypeSymbol.ContainingNamespace?.ToDisplayString();
        
        if (namespaceName == "System.Collections.Generic")
        {
            return typeName.StartsWith("List`1") ||
                   typeName.StartsWith("IList`1") ||
                   typeName.StartsWith("ICollection`1") ||
                   typeName.StartsWith("IEnumerable`1") ||
                   typeName.StartsWith("Dictionary`2") ||
                   typeName.StartsWith("IDictionary`2") ||
                   typeName.StartsWith("HashSet`1") ||
                   typeName.StartsWith("ISet`1");
        }
        
        if (namespaceName == "System.Collections.ObjectModel")
        {
            return typeName.StartsWith("ObservableCollection`1") ||
                   typeName.StartsWith("Collection`1") ||
                   typeName.StartsWith("ReadOnlyCollection`1");
        }
        
        return false;
    }

    /// <summary>
    /// Checks if a property represents a boolean type using Roslyn analysis
    /// </summary>
    private static bool IsBooleanType(PropertyInfo prop)
    {
        // Use Roslyn's definitive type analysis
        return prop.FullTypeSymbol.SpecialType == SpecialType.System_Boolean ||
               (prop.FullTypeSymbol is INamedTypeSymbol namedType && 
                namedType.IsGenericType && 
                namedType.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T &&
                namedType.TypeArguments.FirstOrDefault()?.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>
    /// Checks if a property represents an enum type using Roslyn analysis
    /// </summary>
    private static bool IsEnumType(PropertyInfo prop)
    {
        // Use Roslyn's definitive type analysis - much more reliable than string patterns
        if (prop.FullTypeSymbol.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // Check for nullable enum (Enum?)
        if (prop.FullTypeSymbol is INamedTypeSymbol namedType && 
            namedType.IsGenericType && 
            namedType.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = namedType.TypeArguments.FirstOrDefault();
            return underlyingType?.TypeKind == TypeKind.Enum;
        }

        return false;
    }

    /// <summary>
    /// Checks if a property represents a primitive type using Roslyn analysis
    /// </summary>
    private static bool IsPrimitiveType(PropertyInfo prop)
    {
        // Use Roslyn's SpecialType enumeration for definitive primitive type detection
        var specialType = prop.FullTypeSymbol.SpecialType;
        
        return specialType == SpecialType.System_Boolean ||
               specialType == SpecialType.System_Char ||
               specialType == SpecialType.System_SByte ||
               specialType == SpecialType.System_Byte ||
               specialType == SpecialType.System_Int16 ||
               specialType == SpecialType.System_UInt16 ||
               specialType == SpecialType.System_Int32 ||
               specialType == SpecialType.System_UInt32 ||
               specialType == SpecialType.System_Int64 ||
               specialType == SpecialType.System_UInt64 ||
               specialType == SpecialType.System_Decimal ||
               specialType == SpecialType.System_Single ||
               specialType == SpecialType.System_Double ||
               specialType == SpecialType.System_String ||
               specialType == SpecialType.System_DateTime ||
               IsWellKnownValueType(prop) ||
               IsMemoryType(prop);
    }

    /// <summary>
    /// Checks for well-known value types that don't have SpecialType entries
    /// </summary>
    private static bool IsWellKnownValueType(PropertyInfo prop)
    {
        var typeName = prop.FullTypeSymbol.MetadataName;
        var namespaceName = prop.FullTypeSymbol.ContainingNamespace?.ToDisplayString();
        
        return namespaceName == "System" && (
            typeName == "Guid" ||
            typeName == "TimeSpan" ||
            typeName == "DateOnly" ||
            typeName == "TimeOnly" ||
            typeName == "Half" ||
            typeName == "IntPtr" ||
            typeName == "UIntPtr" ||
            typeName.StartsWith("Memory`1") ||
            typeName.StartsWith("ReadOnlyMemory`1") ||
            typeName.StartsWith("Span`1") ||
            typeName.StartsWith("ReadOnlySpan`1"));
    }

    /// <summary>
    /// Checks if a property represents a complex type using Roslyn analysis
    /// </summary>
    private static bool IsComplexType(PropertyInfo prop)
    {
        // A type is complex if it's not primitive, not collection, not enum, and not boolean
        return !IsPrimitiveType(prop) && 
               !IsCollectionType(prop) && 
               !IsBooleanType(prop) && 
               !IsEnumType(prop) &&
               prop.FullTypeSymbol.TypeKind == TypeKind.Class;
    }

    /// <summary>
    /// Checks if a property represents a Memory or Span type using Roslyn analysis
    /// </summary>
    private static bool IsMemoryType(PropertyInfo prop)
    {
        if (prop.FullTypeSymbol is not INamedTypeSymbol namedType)
            return false;
            
        var typeName = namedType.MetadataName;
        var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
        
        return namespaceName == "System" && (
            typeName.StartsWith("Memory`1") ||
            typeName.StartsWith("ReadOnlyMemory`1") ||
            typeName.StartsWith("Span`1") ||
            typeName.StartsWith("ReadOnlySpan`1"));
    }

    /// <summary>
    /// Makes a variable name safe by avoiding C# keywords and ensuring valid identifier format
    /// </summary>
    public static string MakeSafeVariableName(string name)
    {
        // Handle C# keywords
        var keywords = new HashSet<string>
        {
            "char", "int", "long", "bool", "byte", "short", "float", "double", "decimal",
            "string", "object", "class", "struct", "enum", "interface", "namespace",
            "using", "public", "private", "protected", "internal", "static", "readonly",
            "const", "void", "var", "new", "this", "base", "typeof", "sizeof", "null",
            "true", "false", "if", "else", "for", "while", "do", "switch", "case",
            "default", "break", "continue", "return", "throw", "try", "catch", "finally"
        };

        if (keywords.Contains(name.ToLower()))
        {
            return "@" + name; // Use verbatim identifier
        }

        // Ensure it starts with a letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            return "_" + name;
        }

        return name;
    }

    /// <summary>
    /// Makes a property access safe by ensuring valid identifier format
    /// </summary>
    private static string MakeSafePropertyAccess(string propertyName)
    {
        // For property access, we don't need the @ prefix, just ensure it's a valid identifier
        if (string.IsNullOrWhiteSpace(propertyName))
            return "UnknownProperty";
            
        // Remove any invalid characters and ensure it starts with a letter or underscore
        var cleaned = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (i == 0)
            {
                if (char.IsLetter(c) || c == '_')
                    cleaned.Append(c);
                else
                    cleaned.Append('_');
            }
            else
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    cleaned.Append(c);
            }
        }
        
        var result = cleaned.ToString();
        if (string.IsNullOrEmpty(result))
            return "UnknownProperty";
            
        return result;
    }
}

/// <summary>
/// Contains the analysis results of discovered properties
/// </summary>
public class PropertyAnalysis
{
    public List<PropertyInfo> SimpleProperties { get; } = new();
    public List<PropertyInfo> BooleanProperties { get; } = new();
    public List<PropertyInfo> EnumProperties { get; } = new();
    public List<PropertyInfo> CollectionProperties { get; } = new();
    public List<PropertyInfo> ComplexProperties { get; } = new(); // Classes with properties
    
    // Add metadata for better code generation
    private readonly Dictionary<string, PropertyMetadata> _metadata = new();
    
    public PropertyMetadata GetMetadata(PropertyInfo prop)
    {
        if (!_metadata.TryGetValue(prop.Name, out var metadata))
        {
            metadata = PropertyDiscoveryUtility.AnalyzePropertyMetadata(prop);
            _metadata[prop.Name] = metadata;
        }
        return metadata;
    }
    
    public void SetMetadata(PropertyInfo prop, PropertyMetadata metadata)
    {
        _metadata[prop.Name] = metadata;
    }
}

/// <summary>
/// Contains metadata about a property for code generation
/// </summary>
public class PropertyMetadata
{
    public string SafeVariableName { get; set; } = string.Empty;
    public string SafePropertyAccess { get; set; } = string.Empty;
    public string CountProperty { get; set; } = "Count"; // "Length" or "Count"
    public bool IsNonNullableValueType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool RequiresNullCheck { get; set; } = true;
    public UIHints UIHints { get; set; } = new();
    public bool IsArrayType { get; set; }
    public string TypeCategory { get; set; } = "Simple"; // Simple, Boolean, Enum, Collection, Complex
}

/// <summary>
/// UI generation hints for properties
/// </summary>
public class UIHints
{
    public bool SupportsDataBinding { get; set; } = true;
    public string DefaultControlType { get; set; } = "TextBox"; // "TextBox", "CheckBox", "ComboBox"
    public bool IsReadOnlyRecommended { get; set; }
    public string DisplayFormat { get; set; } = string.Empty;
    public bool ShowInTreeView { get; set; } = true;
    public bool ShowInPropertyEditor { get; set; } = true;
}