using System;
using System.Globalization;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace Bugs;

public class NewBugTests
{
    [Fact]
    public void ToSnake_ConsecutiveCaps()
    {
        Assert.Equal("http_server", GeneratorHelpers.ToSnake("HTTPServer"));
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
    public void ToSnake_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("indigo", GeneratorHelpers.ToSnake("Indigo"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
