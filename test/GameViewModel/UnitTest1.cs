using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Xunit;
using System.Diagnostics;

public class GameViewModelGenerationTests
{
    static void AssertEqualWithDiff(string expectedPath, string actualText)
    {
        var expected = File.ReadAllText(expectedPath);
        var normExpected = expected.Replace("\r\n", "\n");
        var normActual = actualText.Replace("\r\n", "\n");
        if(normExpected != normActual)
        {
            var actualDir = Path.Combine(Path.GetDirectoryName(expectedPath)!, "..", "actual");
            Directory.CreateDirectory(actualDir);
            var actualPath = Path.Combine(actualDir, Path.GetFileName(expectedPath));
            File.WriteAllText(actualPath, actualText);
            try
            {
                var psi = new ProcessStartInfo("git", $"--no-pager diff --no-index \"{expectedPath}\" \"{actualPath}\"")
                {
                    UseShellExecute = false
                };
                var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
            Assert.Equal(normExpected, normActual);
        }
    }
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
        AssertEqualWithDiff(Path.Combine(root,"test","GameViewModel","expected","GameViewModelService.proto"), proto);
    }

    [Fact]
    public async Task ServerMatchesExpected()
    {
        var (_,server,_,_) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        AssertEqualWithDiff(Path.Combine(root,"test","GameViewModel","expected","GameViewModelGrpcServiceImpl.cs"), server);
    }

    [Fact]
    public async Task ClientMatchesExpected()
    {
        var (_,_,client,_) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        AssertEqualWithDiff(Path.Combine(root,"test","GameViewModel","expected","GameViewModelRemoteClient.cs"), client);
    }

    [Fact]
    public async Task TypeScriptMatchesExpected()
    {
        var (_,_,_,ts) = await GenerateAsync();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        AssertEqualWithDiff(Path.Combine(root,"test","GameViewModel","expected","GameViewModelRemoteClient.ts"), ts);
    }
}
