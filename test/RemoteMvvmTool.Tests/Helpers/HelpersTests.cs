using System;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace RemoteMvvmTool.Tests.HelperFunctions;

public class HelpersTests
{
    [Fact]
    public void AttributeMatches_NestedAttribute_DoesNotMatch()
    {
        var code = @"
namespace NamespaceA {
    public class Outer { public class FooAttribute : System.Attribute {} }
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
    public void AttributeMatches_DifferentNamespace_ReturnsFalse()
    {
        var code = @"
namespace NamespaceA {
    public class FooAttribute : System.Attribute {}
    [Foo]
    public class TestClass {}
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("NamespaceA.TestClass");
        var attribute = classSymbol!.GetAttributes().Single();
        Assert.False(Helpers.AttributeMatches(attribute, "NamespaceB.FooAttribute"));
    }

    [Fact]
    public void GetAllMembers_IncludesDefaultInterfaceMembers()
    {
        var code = @"
public interface IFoo {
    void Bar() {}
}
public class Foo : IFoo { }";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("Foo");
        var members = Helpers.GetAllMembers(classSymbol!).ToList();
        Assert.Contains(members.OfType<IMethodSymbol>(), m => m.Name == "Bar");
    }

    [Fact]
    public void AttributeMatches_NestedAttribute_ShouldMatch()
    {
        var code = @"namespace N { public class Outer { public class InnerAttribute : System.Attribute {} } [Outer.Inner] public class C {} }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("N.C");
        var attribute = classSymbol!.GetAttributes().Single();
        Assert.True(Helpers.AttributeMatches(attribute, "N.Outer.InnerAttribute"));
    }

    [Fact]
    public void AttributeMatches_IgnoresCase()
    {
        var codeCase = "[System.Obsolete] public class TestClass {}";
        var treeCase = CSharpSyntaxTree.ParseText(codeCase, new CSharpParseOptions(LanguageVersion.Latest));
        var compilationCase = CSharpCompilation.Create("Test", new[] { treeCase }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbolCase = compilationCase.GetTypeByMetadataName("TestClass");
        var attr = classSymbolCase!.GetAttributes().Single();
        Assert.True(Helpers.AttributeMatches(attr, "system.obsoleteattribute"));
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
    public void AttributeMatches_WithGlobalPrefix()
    {
        var code = "[System.Obsolete] public class TestClass {}";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbol = compilation.GetTypeByMetadataName("TestClass");
        var attribute = classSymbol!.GetAttributes().Single();
        Assert.True(Helpers.AttributeMatches(attribute, "global::System.ObsoleteAttribute"));
    }

    [Fact]
    public void InheritsFrom_WithGlobalPrefix()
    {
        var code = @"public class BaseClass {} public class Derived : BaseClass {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var derivedSymbol = compilation.GetTypeByMetadataName("Derived");
        Assert.True(Helpers.InheritsFrom(derivedSymbol, "global::BaseClass"));
    }
}
