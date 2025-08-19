using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace Bugs;

public class BugExposureTests
{
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
    public void GetWrapperType_UInt_NotHandled()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("uint"));
    }

    [Fact]
    public void GetWrapperType_ULong_NotHandled()
    {
        Assert.Equal("UInt64Value", GeneratorHelpers.GetWrapperType("ulong"));
    }

    [Fact]
    public void GetWrapperType_Short_NotHandled()
    {
        Assert.Equal("Int32Value", GeneratorHelpers.GetWrapperType("short"));
    }

    [Fact]
    public void GetWrapperType_Decimal_NotHandled()
    {
        Assert.Equal("StringValue", GeneratorHelpers.GetWrapperType("decimal"));
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidSyntax_ShouldStillFindViewModel()
    {
        var code = "using CommunityToolkit.Mvvm.ComponentModel;\npublic partial class Vm : ObservableObject {\n[ObservableProperty]\nprivate int value;"; // missing closing braces
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        await File.WriteAllTextAsync(tempFile, code);
        var referencePaths = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);
        var result = await ViewModelAnalyzer.AnalyzeAsync(new[] { tempFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            referencePaths);
        Assert.NotNull(result.ViewModelSymbol);
        Assert.NotEmpty(result.Properties);
    }
}
