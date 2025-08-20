using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace Bugs;

public class MoreFailingBugTests
{
    [Fact]
    public void ToSnake_ExistingUnderscore_ShouldNotDuplicate()
    {
        Assert.Equal("already_snake", GeneratorHelpers.ToSnake("Already_Snake"));
    }

    [Fact]
    public void TryGetEnumerableElementType_String_ShouldReturnFalse()
    {
        var code = "class C { string S; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("S").Single();
        Assert.False(GeneratorHelpers.TryGetEnumerableElementType(field.Type, out _));
    }

    [Fact]
    public void TryGetEnumerableElementType_NullableArray_ShouldReturnConstructedElement()
    {
        var code = "class C { int?[] Values; }";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Values").Single();
        GeneratorHelpers.TryGetEnumerableElementType(field.Type, out var elemType);
        Assert.Equal("int?", elemType?.ToDisplayString());
    }

    [Fact]
    public void GetObservableProperties_StaticField_ShouldBeIgnored()
    {
        var code = @"namespace Test {
using System;
public class ObservableObject {}
[AttributeUsage(AttributeTargets.Field)] public class ObservablePropertyAttribute : Attribute {}
public partial class Vm : ObservableObject {
    [ObservableProperty]
    private static int value;
}}
";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbol = compilation.GetTypeByMetadataName("Test.Vm")!;
        var props = ViewModelAnalyzer.GetObservableProperties(classSymbol, "Test.ObservablePropertyAttribute", compilation);
        Assert.Empty(props);
    }

    [Fact]
    public void GetRelayCommands_StaticMethod_ShouldBeIgnored()
    {
        var code = @"namespace Test {
using System;
public class ObservableObject {}
[AttributeUsage(AttributeTargets.Method)] public class RelayCommandAttribute : Attribute {}
public partial class Vm : ObservableObject {
    [RelayCommand]
    public static void DoIt() {}
}}
";
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classSymbol = compilation.GetTypeByMetadataName("Test.Vm")!;
        var cmds = ViewModelAnalyzer.GetRelayCommands(classSymbol, "Test.RelayCommandAttribute", compilation);
        Assert.Empty(cmds);
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_ListOfBytes_ShouldReturnBytesValue()
    {
        var code = "using System.Collections.Generic; class C { List<byte> Bytes; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<byte>).Assembly.Location)
        });
        var field = (IFieldSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("Bytes").Single();
        Assert.Equal("BytesValue", GeneratorHelpers.GetProtoWellKnownTypeFor(field.Type));
    }
}
