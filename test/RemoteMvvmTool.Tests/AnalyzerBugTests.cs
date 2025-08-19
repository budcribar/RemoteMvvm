using System;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AnalyzerBugTests
{
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
public class Foo : IFoo { }
";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var classSymbol = compilation.GetTypeByMetadataName("Foo");
        var members = Helpers.GetAllMembers(classSymbol!).ToList();
        Assert.Contains(members.OfType<IMethodSymbol>(), m => m.Name == "Bar");
    }

    [Fact]
    public void GetRelayCommands_ValueTaskDetectedAsAsync()
    {
        var code = @"
using CommunityToolkit.Mvvm.Input;
public partial class MyViewModel {
    [RelayCommand]
    public System.Threading.Tasks.ValueTask DoWorkAsync() => default;
}
namespace CommunityToolkit.Mvvm.Input {
    public class RelayCommandAttribute : System.Attribute {}
}
";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, references);
        var classSymbol = compilation.GetTypeByMetadataName("MyViewModel");
        var cmds = ViewModelAnalyzer.GetRelayCommands(classSymbol!, "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", compilation);
        Assert.True(cmds[0].IsAsync);
    }
}
