using Microsoft.CodeAnalysis;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace RemoteMvvmTool.Generators;

public static class GeneratorHelpers
{
    public static string ToSnake(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var sb = new StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            bool addUnderscore = i > 0 && s[i - 1] != '_' && char.IsUpper(c) &&
                                 (char.IsLower(s[i - 1]) ||
                                  (i + 1 < s.Length && char.IsLower(s[i + 1])));

            if (addUnderscore)
                sb.Append('_');

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
    public static string ToCamel(string s) => string.IsNullOrEmpty(s)?s:char.ToLowerInvariant(s[0])+s.Substring(1);
    public static string ToCamelCase(string s) => string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    public static string ToPascalCase(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    public static string? GetWrapperType(string typeName) => typeName switch
    {
        "string" => "StringValue",
        "int" => "Int32Value",
        "System.Int32" => "Int32Value",
        "long" => "Int64Value",
        "System.Int64" => "Int64Value",
        "uint" => "UInt32Value",
        "System.UInt32" => "UInt32Value",
        "ulong" => "UInt64Value",
        "System.UInt64" => "UInt64Value",
        "short" => "Int32Value",
        "System.Int16" => "Int32Value",
        "ushort" => "UInt32Value",
        "System.UInt16" => "UInt32Value",
        "byte" => "UInt32Value",
        "System.Byte" => "UInt32Value",
        "sbyte" => "Int32Value",
        "System.SByte" => "Int32Value",
        "nint" => "Int64Value",
        "System.IntPtr" => "Int64Value",
        "nuint" => "UInt64Value",
        "System.UIntPtr" => "UInt64Value",
        "char" => "StringValue",
        "System.Char" => "StringValue",
        "decimal" => "StringValue",
        "System.Decimal" => "StringValue",
        "bool" => "BoolValue",
        "System.Boolean" => "BoolValue",
        "float" => "FloatValue",
        "System.Single" => "FloatValue",
        "double" => "DoubleValue",
        "System.Double" => "DoubleValue",
        "half" => "FloatValue",
        "Half" => "FloatValue",
        "System.Half" => "FloatValue",
        "System.DateTime" => "Timestamp",
        _ => null
    };
    public static string LowercaseFirst(string str) => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str[1..];
    public static string GetProtoWellKnownTypeFor(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is null) return "Any";
        if (typeSymbol is INamedTypeSymbol namedTypeSymbolNullable &&
            namedTypeSymbolNullable.IsGenericType &&
            namedTypeSymbolNullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
        {
            typeSymbol = namedTypeSymbolNullable.TypeArguments[0];
        }

        if (typeSymbol.TypeKind == TypeKind.Enum) return "Int32Value";

        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_String: return "StringValue";
            case SpecialType.System_Boolean: return "BoolValue";
            case SpecialType.System_Single: return "FloatValue";
            case SpecialType.System_Double: return "DoubleValue";
            case SpecialType.System_Int32: return "Int32Value";
            case SpecialType.System_Int64: return "Int64Value";
            case SpecialType.System_UInt32: return "UInt32Value";
            case SpecialType.System_UInt64: return "UInt64Value";
            case SpecialType.System_SByte: return "Int32Value";
            case SpecialType.System_Byte: return "UInt32Value";
            case SpecialType.System_Int16: return "Int32Value";
            case SpecialType.System_UInt16: return "UInt32Value";
            case SpecialType.System_IntPtr: return "Int64Value";
            case SpecialType.System_UIntPtr: return "UInt64Value";
            case SpecialType.System_Char: return "StringValue";
            case SpecialType.System_DateTime: return "Timestamp";
            case SpecialType.System_Decimal: return "StringValue";
            case SpecialType.System_Object: return "Any";
        }

        string fullTypeName = typeSymbol.OriginalDefinition.ToDisplayString();
        if (fullTypeName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal))
            return "Any";

        switch (fullTypeName)
        {
            case "System.TimeSpan": return "Duration";
            case "System.Guid": return "StringValue";
            case "System.DateTimeOffset": return "Timestamp";
            case "System.DateOnly":
            case "System.TimeOnly":
                return "StringValue";
            case "System.Half": return "FloatValue";
            case "System.Uri": return "StringValue";
            case "System.Version": return "StringValue";
            case "System.Numerics.BigInteger": return "StringValue";
        }

        if (typeSymbol.TypeKind == TypeKind.Array && typeSymbol is IArrayTypeSymbol arraySymbol)
        {
            if (arraySymbol.ElementType.SpecialType == SpecialType.System_Byte && arraySymbol.Rank == 1)
                return "BytesValue";
        }

        if (TryGetMemoryElementType(typeSymbol, out var elementType))
        {
            if (elementType is INamedTypeSymbol namedNullable &&
                namedNullable.IsGenericType &&
                namedNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                elementType = namedNullable.TypeArguments[0];
            }
            if (elementType?.SpecialType == SpecialType.System_Byte)
                return "BytesValue";
        }

        if (TryGetEnumerableElementType(typeSymbol, out var enumElem))
        {
            if (enumElem is INamedTypeSymbol enumNullable &&
                enumNullable.IsGenericType &&
                enumNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                enumElem = enumNullable.TypeArguments[0];
            }
            if (enumElem?.SpecialType == SpecialType.System_Byte)
                return "BytesValue";
        }

        return "Any";
    }

    public static bool TryGetDictionaryTypeArgs(ITypeSymbol typeSymbol, out ITypeSymbol? keyType, out ITypeSymbol? valueType)
    {
        keyType = null;
        valueType = null;
        if (typeSymbol is INamedTypeSymbol named)
        {
            if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                return true;
            }
            var iface = named.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");
            if (iface != null)
            {
                keyType = iface.TypeArguments[0];
                valueType = iface.TypeArguments[1];
                return true;
            }
        }
        return false;
    }

    public static bool TryGetEnumerableElementType(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (typeSymbol is IArrayTypeSymbol arraySymbol)
        {
            elementType = arraySymbol.ElementType;
            return true;
        }

        if (typeSymbol is INamedTypeSymbol named)
        {
            if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                elementType = named.TypeArguments[0];
                return true;
            }
            var iface = named.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
            if (iface != null)
            {
                elementType = iface.TypeArguments[0];
                return true;
            }
        }
        return false;
    }

    public static bool TryGetMemoryElementType(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (typeSymbol is INamedTypeSymbol named)
        {
            var def = named.OriginalDefinition.ToDisplayString();
            if (def == "System.Memory<T>" || def == "System.ReadOnlyMemory<T>" ||
                def == "System.Span<T>" || def == "System.ReadOnlySpan<T>")
            {
                elementType = named.TypeArguments[0];
                return true;
            }
        }
        return false;
    }

    public static bool IsWellKnownType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
            return true;
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);
    }

    static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        if (sb.Length > 0 && char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    public static string GetDictionaryEntryName(ITypeSymbol keyType, ITypeSymbol valueType)
        => SanitizeIdentifier(keyType.Name + "_" + valueType.Name + "_Entry");

    public static bool CanUseProtoMap(ITypeSymbol keyType, ITypeSymbol valueType)
    {
        var allowedKeys = new HashSet<string>
        {
            "int32","int64","uint32","uint64","sint32","sint64","fixed32","fixed64","sfixed32","sfixed64","bool","string"
        };
        string keyProto = GetProtoWellKnownTypeFor(keyType) switch
        {
            "StringValue" => "string",
            "BoolValue" => "bool",
            "Int32Value" => "int32",
            "Int64Value" => "int64",
            "UInt32Value" => "uint32",
            "UInt64Value" => "uint64",
            "FloatValue" => "float",
            "DoubleValue" => "double",
            _ => keyType.TypeKind == TypeKind.Enum ? "int32" : string.Empty
        };
        if (!allowedKeys.Contains(keyProto))
            return false;
        if (TryGetDictionaryTypeArgs(valueType, out _, out _))
            return false;
        return true;
    }

    public static void AppendAutoGeneratedHeader(StringBuilder sb, string prefix = "// ", string suffix = "")
    {
        sb.AppendLine($"{prefix}<auto-generated>{suffix}");
        sb.AppendLine($"{prefix}Generated by RemoteMvvmTool.{suffix}");
        sb.AppendLine($"{prefix}</auto-generated>{suffix}");
        sb.AppendLine();
    }
}
