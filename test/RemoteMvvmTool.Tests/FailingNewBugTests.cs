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

public class FailingNewBugTests
{
    [Fact]
    public void GetWrapperType_Byte_NotHandled()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("byte"));
    }

    [Fact]
    public void GetWrapperType_SByte_NotHandled()
    {
        Assert.Equal("Int32Value", GeneratorHelpers.GetWrapperType("sbyte"));
    }

    [Fact]
    public void GetWrapperType_Char_NotHandled()
    {
        Assert.Equal("StringValue", GeneratorHelpers.GetWrapperType("char"));
    }

    [Fact]
    public void GetProtoWellKnownTypeFor_ReadOnlyMemoryByte_NotHandled()
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
    public void GetProtoWellKnownTypeFor_MemoryByte_NotHandled()
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
    public async Task AnalyzeAsync_CompletelyInvalidCode()
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
}

