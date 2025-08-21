using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Microsoft.CodeAnalysis;
using Xunit;

public class ProtoGeneratorBugTests
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

    static async Task<(string Name, List<PropertyInfo> Props, List<CommandInfo> Cmds, Compilation Comp)> AnalyzeAsync(string source)
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".cs");
        await File.WriteAllTextAsync(tmp, source);
        try
        {
            var refs = LoadDefaultRefs();
            var (_, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { tmp },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                refs,
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
            return (name, props, cmds, comp);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task ByteArrayProperty_ShouldUseBytes()
    {
        const string source = @"
using CommunityToolkit.Mvvm.ComponentModel;
public partial class ByteArrayViewModel : ObservableObject
{
    [ObservableProperty]
    private byte[] data;
}";
        var (name, props, cmds, comp) = await AnalyzeAsync(source);
        var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, comp);
        Assert.Contains("bytes data = 1;", proto);
    }

    [Fact]
    public async Task NullablePrimitive_ShouldUseWrapper()
    {
        const string source = @"
using CommunityToolkit.Mvvm.ComponentModel;
public partial class NullableIntViewModel : ObservableObject
{
    [ObservableProperty]
    private int? count;
}";
        var (name, props, cmds, comp) = await AnalyzeAsync(source);
        var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, comp);
        Assert.Contains("google.protobuf.Int32Value count = 1;", proto);
    }

    [Fact]
    public async Task MultiDimArray_ShouldThrow()
    {
        const string source = @"
using CommunityToolkit.Mvvm.ComponentModel;
public partial class MultiDimArrayViewModel : ObservableObject
{
    [ObservableProperty]
    private int[,] grid;
}";
        var (name, props, cmds, comp) = await AnalyzeAsync(source);
        Assert.Throws<NotSupportedException>(() =>
            ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, comp));
    }

    [Fact]
    public async Task CommandWithCustomParam_ShouldGenerateMessage()
    {
        const string source = @"
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
public class CustomType { public int X { get; set; } }
public partial class CommandWithCustomParamViewModel : ObservableObject
{
    [ObservableProperty]
    private int counter;
    [RelayCommand]
    private void Save(CustomType item) { }
}";
        var (name, props, cmds, comp) = await AnalyzeAsync(source);
        var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, comp);
        Assert.Contains("message CustomTypeState", proto);
    }

    [Fact]
    public async Task NoProperties_ShouldNotThrow()
    {
        const string source = @"
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
public partial class CommandOnlyViewModel : ObservableObject
{
    [RelayCommand]
    private void Do() { }
}";
        var (name, props, cmds, comp) = await AnalyzeAsync(source);
        ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, comp);
    }
}
