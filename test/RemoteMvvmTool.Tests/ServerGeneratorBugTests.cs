using System;
using System.Collections.Generic;
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
}

