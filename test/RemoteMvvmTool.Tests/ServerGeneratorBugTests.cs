using System.Collections.Generic;
using GrpcRemoteMvvmModelUtil;
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
    public void UpdatePropertyValue_ShouldHandle_Double()
    {
        var server = GenerateServer();
        Assert.Contains("request.NewValue.Is(DoubleValue.Descriptor)", server);
    }

    [Fact]
    public void UpdatePropertyValue_ShouldHandle_Float()
    {
        var server = GenerateServer();
        Assert.Contains("request.NewValue.Is(FloatValue.Descriptor)", server);
    }

    [Fact]
    public void UpdatePropertyValue_ShouldHandle_Long()
    {
        var server = GenerateServer();
        Assert.Contains("request.NewValue.Is(Int64Value.Descriptor)", server);
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
}

