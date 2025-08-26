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

public class ServerGeneratorBugTests
{
    static string GenerateServer()
    {
        return ServerGenerator.Generate(
            "SampleViewModel",
            "Generated.Protos",
            "SampleViewModelService",
            new List<PropertyInfo>(),
            new List<CommandInfo>(),
            "Generated.ViewModels");
    }

    [Fact]
    public void ConvertAnyToTargetType_ShouldHandle_Double()
    {
        var server = GenerateServer();
        Assert.Contains("anyValue.Is(DoubleValue.Descriptor)", server);
    }

    [Fact]
    public void ConvertAnyToTargetType_ShouldHandle_Float()
    {
        var server = GenerateServer();
        Assert.Contains("anyValue.Is(FloatValue.Descriptor)", server);
    }

    [Fact]
    public void ConvertAnyToTargetType_ShouldHandle_Long()
    {
        var server = GenerateServer();
        Assert.Contains("anyValue.Is(Int64Value.Descriptor)", server);
    }

    [Fact]
    public void PackToAny_ShouldHandle_UInt()
    {
        var server = GenerateServer();
        Assert.Contains("case uint", server);
    }

    [Fact]
    public void ToValue_ShouldHandle_UInt()
    {
        var server = GenerateServer();
        Assert.Contains("case uint", server);
    }

    [Fact]
    public void CommandParameter_DateTime_Should_Use_ToDateTime()
    {
        var compilation = CSharpCompilation.Create("test",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var dtSymbol = compilation.GetSpecialType(SpecialType.System_DateTime);
        var cmd = new CommandInfo("SetTime", "SetTimeCommand",
            new List<ParameterInfo> { new("time", "System.DateTime", dtSymbol) }, false);
        var server = ServerGenerator.Generate(
            "SampleViewModel",
            "Generated.Protos",
            "SampleViewModelService",
            new List<PropertyInfo>(),
            new List<CommandInfo> { cmd },
            "Generated.ViewModels");
        Assert.Contains("var time = request.Time.ToDateTime()", server);
    }

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
    public void CommandWithGuidParameter_ParsesGuid()
    {
        var compilation = CreateCompilation();
        var guidType = compilation.GetTypeByMetadataName("System.Guid")!;
        var parameters = new List<ParameterInfo>
        {
            new("id", guidType.ToDisplayString(), guidType)
        };
        var cmd = new CommandInfo("DoThing", "DoThingCommand", parameters, false);
        var code = ServerGenerator.Generate("Vm", "Test.Proto", "VmService", new List<PropertyInfo>(), new List<CommandInfo> { cmd }, "Generated.ViewModels");
        Assert.Contains("var id = Guid.Parse(request.Id);", code);
    }
}

