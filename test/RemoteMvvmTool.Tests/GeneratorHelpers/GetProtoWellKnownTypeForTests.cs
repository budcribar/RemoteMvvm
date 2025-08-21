using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.GeneratorHelpersTests;

public class GetProtoWellKnownTypeForTests
{
    [Fact]
    public void GetProtoWellKnownTypeFor_ReadOnlySpanByte_ReturnsBytesValue()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var spanType = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Byte));
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(spanType));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_NullableByteEnumerable_ReturnsBytesValue()
    {
        var code = "using System.Collections.Generic; public class C { public IEnumerable<byte?> Data => null!; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var typeSymbol = ((IPropertySymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Data").Single()).Type;
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(typeSymbol));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_ReadOnlyMemoryByte_ReturnsBytesValue()
    {
        var code = "using System; class C { System.ReadOnlyMemory<byte> F; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ReadOnlyMemory<byte>).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("F").Single();
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_MemoryByte_ReturnsBytesValue()
    {
        var code = "using System; class C { System.Memory<byte> F; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Memory<byte>).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("F").Single();
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_Half_ReturnsFloatValue()
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
    public void GetProtoWellKnownTypeFor_DateOnly_ReturnsStringValue()
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

    [Fact]
    public void GetProtoWellKnownTypeFor_ListOfBytes_ReturnsBytesValue()
    {
        var code = "using System.Collections.Generic; class C { List<byte> Bytes; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<byte>).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Bytes").Single();
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }
}
