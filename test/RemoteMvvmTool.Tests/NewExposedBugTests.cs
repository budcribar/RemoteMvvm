using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace Bugs;

public class NewExposedBugTests
{
    [Fact]
    public void GetWrapperType_UShort_NotHandled()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("ushort"));
    }

    [Fact]
    public void GetWrapperType_NInt_NotHandled()
    {
        Assert.Equal("Int64Value", GeneratorHelpers.GetWrapperType("nint"));
    }

    [Fact]
    public void GetWrapperType_NUInt_NotHandled()
    {
        Assert.Equal("UInt64Value", GeneratorHelpers.GetWrapperType("nuint"));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_Half_NotHandled()
    {
        var code = "using System; class C { System.Half F; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Half).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("F").Single();
        Assert.Equal("FloatValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }

    [Fact]
    public void TryGetEnumerableElementType_Array_NotHandled()
    {
        var code = "class C { int[] Numbers; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Numbers").Single();
        Assert.True(GeneratorHelpers.TryGetEnumerableElementType(field.Type, out var elem));
        Assert.Equal("int", elem!.ToDisplayString());
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_DateOnly_NotHandled()
    {
        var code = "using System; class C { DateOnly D; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DateOnly).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("D").Single();
        Assert.Equal("StringValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }
}

