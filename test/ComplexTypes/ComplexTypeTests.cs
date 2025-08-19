using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using ComplexTypes.ViewModels;
using ComplexTypes.ViewModels.RemoteClients;
using PeakSWC.Mvvm.Remote;
using Xunit;

namespace ComplexTypes.Tests;

public class ComplexTypeTests
{
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

    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    static Dictionary<int, SecondLevel> CreateSampleData()
    {
        var fourthA = new FourthLevel { Measurement = 1.23 };
        var fourthB = new FourthLevel { Measurement = 4.56 };
        var thirdA = new ThirdLevel
        {
            Series = new[] { fourthA },
            Items = new List<FourthLevel> { new FourthLevel { Measurement = 7.89 } }
        };
        var thirdB = new ThirdLevel
        {
            Series = new[] { fourthB },
            Items = new List<FourthLevel> { new FourthLevel { Measurement = 0.12 } }
        };
        var second = new SecondLevel
        {
            ModeMap = new Dictionary<Mode, ThirdLevel[]> { { Mode.On, new[] { thirdA } }, { Mode.Off, new[] { thirdB } } },
            NamedGroups = new Dictionary<string, List<ThirdLevel>>
            {
                { "groupA", new List<ThirdLevel> { thirdA } },
                { "groupB", new List<ThirdLevel> { thirdB } }
            }
        };
        return new Dictionary<int, SecondLevel> { { 1, second } };
    }

    static void AssertFourthLevel(FourthLevel expected, FourthLevel actual)
        => Assert.Equal(expected.Measurement, actual.Measurement);

    static void AssertThirdLevel(ThirdLevel expected, ThirdLevel actual)
    {
        Assert.Equal(expected.Series.Length, actual.Series.Length);
        for (int i = 0; i < expected.Series.Length; i++)
            AssertFourthLevel(expected.Series[i], actual.Series[i]);
        Assert.Equal(expected.Items.Count, actual.Items.Count);
        for (int i = 0; i < expected.Items.Count; i++)
            AssertFourthLevel(expected.Items[i], actual.Items[i]);
    }

    static void AssertSecondLevel(SecondLevel expected, SecondLevel actual)
    {
        Assert.Equal(expected.ModeMap.Count, actual.ModeMap.Count);
        foreach (var kv in expected.ModeMap)
        {
            Assert.True(actual.ModeMap.ContainsKey(kv.Key));
            var expArr = kv.Value;
            var actArr = actual.ModeMap[kv.Key];
            Assert.Equal(expArr.Length, actArr.Length);
            for (int i = 0; i < expArr.Length; i++)
                AssertThirdLevel(expArr[i], actArr[i]);
        }
        Assert.Equal(expected.NamedGroups.Count, actual.NamedGroups.Count);
        foreach (var kv in expected.NamedGroups)
        {
            Assert.True(actual.NamedGroups.ContainsKey(kv.Key));
            var expList = kv.Value;
            var actList = actual.NamedGroups[kv.Key];
            Assert.Equal(expList.Count, actList.Count);
            for (int i = 0; i < expList.Count; i++)
                AssertThirdLevel(expList[i], actList[i]);
        }
    }

    [Fact]
    public async Task SupportedTypes_GenerateProto()
    {
        string vmFile = Path.Combine(Root, "test", "ComplexTypes", "ViewModels", "SupportedComplexViewModel.cs");
        string nestedFile = Path.Combine(Root, "test", "ComplexTypes", "ViewModels", "NestedModels.cs");
        var refs = LoadDefaultRefs();
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile, nestedFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs,
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
        var proto = ProtoGenerator.Generate("ComplexTypes.Protos", name + "Service", name, props, cmds, comp);
        Assert.Contains("SupportedComplexViewModelState", proto);
    }

    [Fact]
    public async Task SupportedCollectionTypes_GenerateProto()
    {
        string vmFile = Path.Combine(Root, "test", "ComplexTypes", "ViewModels", "SupportedCollectionsViewModel.cs");
        var refs = LoadDefaultRefs();
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs,
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
        var proto = ProtoGenerator.Generate("ComplexTypes.Protos", name + "Service", name, props, cmds, comp);
        Assert.Contains("SupportedCollectionsViewModelState", proto);
    }

    [Fact]
    public async Task UnsupportedTypes_Throw()
    {
        string vmFile = Path.Combine(Root, "test", "ComplexTypes", "ViewModels", "UnsupportedViewModels.cs");
        var refs = LoadDefaultRefs();
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs,
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
        Assert.Throws<NotSupportedException>(() =>
            ProtoGenerator.Generate("ComplexTypes.Protos", name + "Service", name, props, cmds, comp));
    }

    [Fact]
    public async Task SupportedTypes_TransferDataThroughRemoteClient()
    {
        var serverOptions = new ServerOptions { Port = NetworkConfig.Port };
        using var serverVm = new SupportedComplexViewModel(serverOptions) { Layers = CreateSampleData() };

        var clientOptions = new ClientOptions { Address = NetworkConfig.ServerAddress };
        using var clientVm = new SupportedComplexViewModel(clientOptions);
        using var remoteClient = await clientVm.GetRemoteModel();

        AssertSecondLevel(serverVm.Layers[1], remoteClient.Layers[1]);
    }
}
