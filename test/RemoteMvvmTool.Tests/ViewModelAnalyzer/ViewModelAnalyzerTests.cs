using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace RemoteMvvmTool.Tests.ViewModelAnalyzerTests;

public class ViewModelAnalyzerTests
{
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
}";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, references);
        var classSymbol = compilation.GetTypeByMetadataName("MyViewModel");
        var cmds = ViewModelAnalyzer.GetRelayCommands(classSymbol!, "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", compilation);
        Assert.True(cmds[0].IsAsync);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidSyntax_ShouldStillFindViewModel()
    {
        var code = "using CommunityToolkit.Mvvm.ComponentModel;\npublic partial class Vm : ObservableObject {\n[ObservableProperty]\nprivate int value;";
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

    [Fact]
    public async Task AnalyzeAsync_CompletelyInvalidCode_StillReturnsResult()
    {
        var code = "using CommunityToolkit.Mvvm.ComponentModel; public partial class Vm : ObservableObject { [ObservableProperty] private int value";
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
    }

    [Fact]
    public async Task AnalyzeAsync_MissingType_WarnsButContinues()
    {
        var code = @"using CommunityToolkit.Mvvm.ComponentModel;
public partial class Vm : ObservableObject {
    [ObservableProperty]
    private MissingType _value;
}";
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

    [Fact]
    public void GetObservableProperties_StaticField_IsIgnored()
    {
        var code = @"namespace Test {
using System;
public class ObservableObject {}
[AttributeUsage(AttributeTargets.Field)] public class ObservablePropertyAttribute : Attribute {}
public partial class Vm : ObservableObject {
    [ObservableProperty]
    private static int value;
}}";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbol = compilation.GetTypeByMetadataName("Test.Vm")!;
        var props = ViewModelAnalyzer.GetObservableProperties(classSymbol, "Test.ObservablePropertyAttribute", compilation);
        Assert.Empty(props);
    }

    [Fact]
    public void GetRelayCommands_StaticMethod_IsIgnored()
    {
        var code = @"namespace Test {
using System;
public class ObservableObject {}
[AttributeUsage(AttributeTargets.Method)] public class RelayCommandAttribute : Attribute {}
public partial class Vm : ObservableObject {
    [RelayCommand]
    public static void DoIt() {}
}}";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbol = compilation.GetTypeByMetadataName("Test.Vm")!;
        var cmds = ViewModelAnalyzer.GetRelayCommands(classSymbol, "Test.RelayCommandAttribute", compilation);
        Assert.Empty(cmds);
    }
}
