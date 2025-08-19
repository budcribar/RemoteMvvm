using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bugs;

public class ExposedBugTests
{
    [Fact]
    public void GetWrapperType_Float_NotHandled()
    {
        Assert.Equal("FloatValue", GeneratorHelpers.GetWrapperType("float"));
    }

    [Fact]
    public void GetWrapperType_Double_NotHandled()
    {
        Assert.Equal("DoubleValue", GeneratorHelpers.GetWrapperType("double"));
    }

    [Fact]
    public void InheritsFrom_WithGlobalPrefix_NotRecognized()
    {
        var code = @"public class BaseClass {} public class Derived : BaseClass {}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var derivedSymbol = compilation.GetTypeByMetadataName("Derived");
        Assert.True(Helpers.InheritsFrom(derivedSymbol, "global::BaseClass"));
    }

    [Fact]
    public async Task AnalyzeAsync_MissingType_WarnsButContinues()
    {
        var code = @"using CommunityToolkit.Mvvm.ComponentModel;\npublic partial class Vm : ObservableObject {\n    [ObservableProperty]\n    private MissingType _value;\n}";
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        await File.WriteAllTextAsync(tempFile, code);
        var referencePaths = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);
        var result = await ViewModelAnalyzer.AnalyzeAsync(new[] { tempFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            referencePaths);
        Assert.Equal("Vm", result.ViewModelName);
    }
}
