using Microsoft.CodeAnalysis;
using System.Linq;

namespace RemoteMvvmTool.Generators;

public static class GeneratorHelpers
{
    public static string ToSnake(string s) => string.Concat(s.Select((c,i) => i>0 && char.IsUpper(c)?"_"+char.ToLower(c).ToString():char.ToLower(c).ToString()));
    public static string ToCamel(string s) => string.IsNullOrEmpty(s)?s:char.ToLowerInvariant(s[0])+s.Substring(1);
    public static string ToCamelCase(string s) => string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    public static string ToPascalCase(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    public static string? GetWrapperType(string typeName) => typeName switch
    {
        "string" => "StringValue",
        "int" => "Int32Value",
        "System.Int32" => "Int32Value",
        "bool" => "BoolValue",
        "System.Boolean" => "BoolValue",
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
            case SpecialType.System_Char: return "StringValue";
            case SpecialType.System_DateTime: return "Timestamp";
            case SpecialType.System_Decimal: return "StringValue";
            case SpecialType.System_Object: return "Any";
        }

        string fullTypeName = typeSymbol.OriginalDefinition.ToDisplayString();
        switch (fullTypeName)
        {
            case "System.TimeSpan": return "Duration";
            case "System.Guid": return "StringValue";
            case "System.DateTimeOffset": return "Timestamp";
            case "System.Uri": return "StringValue";
            case "System.Version": return "StringValue";
            case "System.Numerics.BigInteger": return "StringValue";
        }

        if (typeSymbol.TypeKind == TypeKind.Array && typeSymbol is IArrayTypeSymbol arraySymbol)
        {
            if (arraySymbol.ElementType.SpecialType == SpecialType.System_Byte && arraySymbol.Rank == 1)
                return "BytesValue";
        }
        return "Any";
    }
}
