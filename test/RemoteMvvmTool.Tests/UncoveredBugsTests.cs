using System;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;

namespace Bugs;

public class UncoveredBugsTests
{
    [Fact]
    public void GetWrapperType_Half_NotHandled()
    {
        Assert.Equal("FloatValue", GeneratorHelpers.GetWrapperType("half"));
    }

    [Fact]
    public void GetWrapperType_DateTime_NotHandled()
    {
        Assert.Equal("Timestamp", GeneratorHelpers.GetWrapperType("System.DateTime"));
    }

    [Fact]
    public void InheritsFrom_IgnoresCase()
    {
        var tree = CSharpSyntaxTree.ParseText("class Base{} class Derived: Base {}");
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var derived = compilation.GetTypeByMetadataName("Derived");
        Assert.True(Helpers.InheritsFrom(derived, "base"));
    }

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
    public void TryGetMemoryElementType_ReadOnlySpan_ReturnsTrue()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var spanType = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        Assert.True(GeneratorHelpers.TryGetMemoryElementType(spanType, out var _));
    }
}
