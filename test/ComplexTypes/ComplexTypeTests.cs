using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
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
}
