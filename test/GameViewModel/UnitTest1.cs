using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Xunit;

public class GameViewModelGenerationTests
{
    private static async Task<(string Proto,string Server,string Client,string Ts)> GenerateAsync()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string vmFile = Path.Combine(root, "src","demo","MonsterClicker","ViewModels","GameViewModel.cs");
        string attrSource = await File.ReadAllTextAsync(Path.Combine(root, "src","GrpcRemoteMvvmGenerator","attributes","GenerateGrpcRemoteAttribute.cs"));
        var references = new[]{typeof(object).GetTypeInfo().Assembly.Location, typeof(Console).GetTypeInfo().Assembly.Location};
        var (sym,name,props,cmds,comp) = await ViewModelAnalyzer.AnalyzeAsync(new[]{vmFile},
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute",
            references,
            attrSource,
            "GenerateGrpcRemoteAttribute.cs");
        Assert.NotNull(sym);
        const string protoNs = "MonsterClicker.ViewModels.Protos";
        const string serviceName = "GameViewModelService";
        var proto = Generators.GenerateProto(protoNs, serviceName, name, props, cmds, comp);
        var server = Generators.GenerateServer(name, protoNs, serviceName, props, cmds);
        var client = Generators.GenerateClient(name, protoNs, serviceName, props, cmds);
        var ts = Generators.GenerateTypeScriptClient(name, protoNs, serviceName, props, cmds);
        return (proto, server, client, ts);
    }

    [Fact]
    public async Task ProtoMatchesExpected()
    {
        var (proto,_,_,_) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var expected = await File.ReadAllTextAsync(Path.Combine(root,"test","GameViewModel","expected","GameViewModelService.proto"));
        Assert.Equal(expected, proto);
    }

    [Fact]
    public async Task ServerMatchesExpected()
    {
        var (_,server,_,_) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var expected = await File.ReadAllTextAsync(Path.Combine(root,"test","GameViewModel","expected","GameViewModelGrpcServiceImpl.cs"));
        Assert.Equal(expected, server);
    }

    [Fact]
    public async Task ClientMatchesExpected()
    {
        var (_,_,client,_) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var expected = await File.ReadAllTextAsync(Path.Combine(root,"test","GameViewModel","expected","GameViewModelRemoteClient.cs"));
        Assert.Equal(expected, client);
    }

    [Fact]
    public async Task TypeScriptMatchesExpected()
    {
        var (_,_,_,ts) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var expected = await File.ReadAllTextAsync(Path.Combine(root,"test","GameViewModel","expected","GameViewModelRemoteClient.ts"));
        Assert.Equal(expected, ts);
    }
}
