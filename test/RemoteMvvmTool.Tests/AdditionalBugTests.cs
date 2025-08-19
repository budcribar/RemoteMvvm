using System;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace Bugs;

public class AdditionalBugTests
{
    [Fact]
    public void AttributeMatches_NestedAttribute_DoesNotMatch()
    {
        var code = @"
namespace NamespaceA {
    public class Outer {
        public class FooAttribute : System.Attribute {}
    }
    [Outer.Foo]
    public class TestClass {}
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("NamespaceA.TestClass");
        var attribute = classSymbol!.GetAttributes().Single();
        Assert.False(Helpers.AttributeMatches(attribute, "NamespaceA.FooAttribute"));
    }

    [Fact]
    public void InheritsFrom_InterfacesShouldMatch()
    {
        var code = @"
public interface IFoo {}
public class FooImpl : IFoo {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("FooImpl");
        Assert.True(Helpers.InheritsFrom(classSymbol, "IFoo"));
    }

    [Fact]
    public void GetWrapperType_HandlesLong()
    {
        Assert.Equal("Int64Value", GeneratorHelpers.GetWrapperType("long"));
    }
}

