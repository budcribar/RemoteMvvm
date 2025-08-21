using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.GeneratorHelpersTests;

public class EnumerableElementTypeTests
{
    [Fact]
    public void TryGetEnumerableElementType_Array_ReturnsElementType()
    {
        var code = "class C { int[] Numbers; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Numbers").Single();
        Assert.True(GeneratorHelpers.TryGetEnumerableElementType(field.Type, out var elem));
        Assert.Equal("int", elem!.ToDisplayString());
    }

    [Fact]
    public void TryGetEnumerableElementType_String_ReturnsFalse()
    {
        var code = "class C { string S; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("S").Single();
        Assert.False(GeneratorHelpers.TryGetEnumerableElementType(field.Type, out _));
    }

    [Fact]
    public void TryGetEnumerableElementType_NullableArray_ReturnsConstructedElement()
    {
        var code = "class C { int?[] Values; }";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Values").Single();
        GeneratorHelpers.TryGetEnumerableElementType(field.Type, out var elemType);
        Assert.Equal("int?", elemType?.ToDisplayString());
    }

    [Fact]
    public void TryGetMemoryElementType_ReadOnlySpan_ReturnsTrue()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var spanType = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        Assert.True(GeneratorHelpers.TryGetMemoryElementType(spanType, out _));
    }
}
