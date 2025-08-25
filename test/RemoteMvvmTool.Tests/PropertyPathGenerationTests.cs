using System.Collections.Generic;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests;

public class PropertyPathGenerationTests
{
    static string GenerateServer() => ServerGenerator.Generate(
        "SampleViewModel",
        "Generated.Protos",
        "SampleViewModelService",
        new List<PropertyInfo>(),
        new List<CommandInfo>(),
        "Generated.ViewModels");

    static string GenerateClient() => TypeScriptClientGenerator.Generate(
        "SampleViewModel",
        "Generated.Protos",
        "SampleViewModelService",
        new List<PropertyInfo>(),
        new List<CommandInfo>());

    [Fact]
    public void ServerGenerator_Emits_PropertyPath()
    {
        var server = GenerateServer();
        Assert.Contains("PropertyPath = fullPath", server);
        Assert.Contains("var topLevel = fullPath.Split", server);
        Assert.Contains("GetValueByPath", server);
    }

    [Fact]
    public void TypeScriptClientGenerator_Handles_PropertyPath()
    {
        var client = GenerateClient();
        Assert.Contains("update.getPropertyPath()", client);
        Assert.Contains("setByPath(this, path", client);
        Assert.Contains("private setByPath", client);
    }
}
