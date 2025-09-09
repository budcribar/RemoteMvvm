using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Utility class for discovering properties at generation time and generating appropriate UI code
/// based on the actual properties found in the view model
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
            
            if (IsMemoryType(prop.TypeString))
            {
                // Memory<T> and similar types should be treated as simple properties for UI purposes
                analysis.SimpleProperties.Add(prop);
                metadata.TypeCategory = "Simple";
            }
            else if (IsCollectionType(prop.TypeString))
            {
                analysis.CollectionProperties.Add(prop);
                metadata.TypeCategory = "Collection";
            }
            else if (IsBooleanType(prop.TypeString))
            {
                analysis.BooleanProperties.Add(prop);
                metadata.TypeCategory = "Boolean";
            }
            else if (IsEnumType(prop.TypeString))
            {
                analysis.EnumProperties.Add(prop);
                metadata.TypeCategory = "Enum";
            }
            else if (IsComplexType(prop.TypeString))
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
    /// Analyzes metadata for a single property
    /// </summary>
    public static PropertyMetadata AnalyzePropertyMetadata(PropertyInfo prop)
    {
        var metadata = new PropertyMetadata
        {
            SafeVariableName = MakeSafeVariableName(prop.Name.ToLower()),
            SafePropertyAccess = MakeSafePropertyAccess(prop.Name),
            DisplayName = prop.Name
        };

        // Determine if this is a non-nullable value type (including enums)
        metadata.IsNonNullableValueType = IsNonNullableValueType(prop.TypeString) || IsEnumType(prop.TypeString);
        metadata.RequiresNullCheck = !metadata.IsNonNullableValueType;

        // Determine count property for collections
        metadata.IsArrayType = prop.TypeString.EndsWith("[]") || 
                              prop.TypeString.EndsWith("Byte[]") || 
                              prop.TypeString.Contains("[]");
        metadata.CountProperty = metadata.IsArrayType ? "Length" : "Count";

        // Set UI hints
        metadata.UIHints = GetUIHints(prop);

        return metadata;
    }

    /// <summary>
    /// Gets UI hints for a property
    /// </summary>
    public static UIHints GetUIHints(PropertyInfo prop)
    {
        var hints = new UIHints
        {
            IsReadOnlyRecommended = prop.IsReadOnly
        };

        if (IsBooleanType(prop.TypeString))
        {
            hints.DefaultControlType = "CheckBox";
        }
        else if (IsEnumType(prop.TypeString))
        {
            hints.DefaultControlType = "ComboBox";
        }
        else if (IsCollectionType(prop.TypeString))
        {
            hints.DefaultControlType = "TreeView";
            hints.ShowInPropertyEditor = false; // Collections better shown in tree
        }
        else if (IsComplexType(prop.TypeString))
        {
            hints.DefaultControlType = "TreeView";
            hints.ShowInPropertyEditor = false; // Complex objects better shown in tree
        }
        else
        {
            hints.DefaultControlType = "TextBox";
        }

        // Memory types should be read-only in UI
        if (IsMemoryType(prop.TypeString))
        {
            hints.IsReadOnlyRecommended = true;
            hints.DisplayFormat = "Hex";
        }

        return hints;
    }

    /// <summary>
    /// Checks if a type string represents a non-nullable value type
    /// </summary>
    public static bool IsNonNullableValueType(string typeString)
    {
        // Handle basic value types that cannot use null-conditional operator
        if (typeString == "int" || typeString == "double" || typeString == "float" ||
            typeString == "decimal" || typeString == "long" || typeString == "short" ||
            typeString == "byte" || typeString == "sbyte" || typeString == "uint" ||
            typeString == "ulong" || typeString == "ushort" || typeString == "nuint" ||
            typeString == "nint" || typeString == "char" || typeString == "bool" ||
            typeString == "DateTime" || typeString == "DateOnly" || typeString == "TimeOnly" ||
            typeString == "Guid" || typeString == "TimeSpan" || typeString == "Half")
        {
            return true;
        }
        
        // Handle Memory<T>, Span<T> and all their variants (these are value types/structs)
        if (typeString.StartsWith("Memory<") || typeString.StartsWith("ReadOnlyMemory<") ||
            typeString.StartsWith("Span<") || typeString.StartsWith("ReadOnlySpan<") ||
            typeString.Contains("Memory<") || typeString.Contains("Span<"))
        {
            return true;
        }
        
        return false;
    }

    private static bool IsCollectionType(string typeString)
    {
        return typeString.Contains("ObservableCollection") || 
               typeString.Contains("List<") || 
               typeString.Contains("IEnumerable<") ||
               typeString.Contains("ICollection<") ||
               typeString.Contains("Dictionary<") ||
               typeString.Contains("IDictionary<") ||
               typeString.Contains("IList<") ||
               typeString.Contains("HashSet<") ||
               typeString.Contains("ISet<") ||
               typeString.Contains("Collection<") ||
               typeString.EndsWith("[]"); // Arrays
    }

    private static bool IsBooleanType(string typeString)
    {
        return typeString == "bool" || typeString == "bool?";
    }

    private static bool IsEnumType(string typeString)
    {
        // Enhanced enum detection
        // Check for common enum patterns and known enum types
        if (typeString.Contains("Enum") && typeString.Contains("."))
            return true;
            
        // Check for nullable enum
        if (typeString.EndsWith("?") && !typeString.StartsWith("bool") && 
            !typeString.StartsWith("int") && !typeString.StartsWith("string") &&
            !typeString.StartsWith("double") && !typeString.StartsWith("float") &&
            !typeString.StartsWith("decimal") && !typeString.StartsWith("DateTime"))
        {
            var baseType = typeString.TrimEnd('?');
            // Could be an enum if it's not a known primitive
            return !IsPrimitiveTypeName(baseType);
        }
        
        // Check for common enum naming patterns
        if (typeString.EndsWith("Type") || typeString.EndsWith("Kind") || 
            typeString.EndsWith("Status") || typeString.EndsWith("State") ||
            typeString.EndsWith("Mode") || typeString.EndsWith("Option"))
            return true;
            
        return false;
    }

    private static bool IsPrimitiveTypeName(string typeName)
    {
        return typeName == "string" || typeName == "int" || typeName == "long" ||
               typeName == "double" || typeName == "float" || typeName == "decimal" ||
               typeName == "bool" || typeName == "char" || typeName == "byte" ||
               typeName == "sbyte" || typeName == "short" || typeName == "ushort" ||
               typeName == "uint" || typeName == "ulong" || typeName == "nuint" || 
               typeName == "nint" || typeName == "DateTime" || typeName == "DateOnly" || 
               typeName == "TimeOnly" || typeName == "Guid" || typeName == "TimeSpan" ||
               typeName == "Half" || // .NET 5+ half-precision float
               typeName.EndsWith("[]") || // Arrays are primitive-like for our purposes
               IsMemoryType(typeName) || // Memory<T> types are primitive-like for our purposes
               (typeName.EndsWith("?") && IsPrimitiveTypeName(typeName.TrimEnd('?'))); // Nullable primitives
    }

    private static bool IsComplexType(string typeString)
    {
        // A type is complex if it's not primitive, not collection, not enum, and not boolean
        return !IsPrimitiveTypeName(typeString) && 
               !IsCollectionType(typeString) && 
               !IsBooleanType(typeString) && 
               !IsEnumType(typeString) &&
               (!typeString.EndsWith("?") || // Handle nullable complex types
               (typeString.EndsWith("?") && !IsPrimitiveTypeName(typeString.TrimEnd('?'))));
    }

    private static bool IsMemoryType(string typeString)
    {
        return typeString.Contains("Memory<") || 
               typeString.Contains("ReadOnlyMemory<") ||
               typeString.Contains("Span<") ||
               typeString.Contains("ReadOnlySpan<");
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