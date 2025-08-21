using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using Xunit;

namespace ToolExecution;

public class ClientGeneratorBugTests
{
    static Compilation CreateCompilation()
    {
        var refs = LoadDefaultRefs().Select(r => MetadataReference.CreateFromFile(r));
        return CSharpCompilation.Create("TestCompilation", references: refs);
    }

    static List<string> LoadDefaultRefs()
    {
        var list = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
        }
        return list;
    }

    [Fact]
    public void ProtoNamespaceWithoutDotCausesException()
    {
        var compilation = CreateCompilation();
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var props = new List<PropertyInfo> { new("Name", "string", stringType) };
        var ex = Record.Exception(() => ClientGenerator.Generate("Vm", "ProtoNs", "VmService", props, new List<CommandInfo>()));
        Assert.Null(ex);
    }

    [Fact]
    public void MultipleParametersCommandNotHandled()
    {
        var compilation = CreateCompilation();
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var parameters = new List<ParameterInfo>
        {
            new("First", "int", intType),
            new("Second", "string", stringType)
        };
        var cmd = new CommandInfo("DoThing", "DoThingCommand", parameters, false);
        var code = ClientGenerator.Generate("Vm", "Test.Proto", "VmService", new List<PropertyInfo>(), new List<CommandInfo>{cmd});
        Assert.Contains("RelayCommand<(int, string)>", code);
    }

    [Fact]
    public void MemoryPropertyAssignmentUsesArray()
    {
        var compilation = CreateCompilation();
        var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
        var memType = compilation.GetTypeByMetadataName("System.Memory`1")!.Construct(byteType);
        var props = new List<PropertyInfo> { new("Data", "System.Memory<byte>", memType) };
        var code = ClientGenerator.Generate("Vm", "Test.Proto", "VmService", props, new List<CommandInfo>());
        Assert.DoesNotContain("ToArray()", code);
    }

    [Fact]
    public void DoublePropertyLacksUpdateCase()
    {
        var compilation = CreateCompilation();
        var doubleType = compilation.GetSpecialType(SpecialType.System_Double);
        var props = new List<PropertyInfo> { new("Value", "double", doubleType) };
        var code = ClientGenerator.Generate("Vm", "Test.Proto", "VmService", props, new List<CommandInfo>());
        Assert.Contains("DoubleValue.Descriptor", code);
    }
}
