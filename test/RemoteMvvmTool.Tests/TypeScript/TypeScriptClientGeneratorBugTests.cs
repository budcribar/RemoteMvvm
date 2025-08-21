using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.TypeScript;

public class TypeScriptClientGeneratorBugTests
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

    static async Task<string> GenerateTsAsync(string code)
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, code);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { tmp },
            "ObservablePropertyAttribute",
            "RelayCommandAttribute",
            refs,
            "ObservableObject");
        return TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
    }

    [Fact(Skip="Bug: Nullable properties should generate optional types")]
    public async Task Nullable_property_should_generate_optional_type()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class NullableViewModel : ObservableObject
{
    [ObservableProperty]
    public int? Count { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("count: number | undefined;", ts);
    }

    [Fact(Skip="Bug: Array properties should use getXList in initializeRemote")]
    public async Task Array_property_should_use_get_list_method()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class ArrayViewModel : ObservableObject
{
    [ObservableProperty]
    public int[] Numbers { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("getNumbersList()", ts);
    }

    [Fact(Skip="Bug: Dictionary properties should use getXMap in initializeRemote")]
    public async Task Dictionary_property_should_use_get_map_method()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class DictViewModel : ObservableObject
{
    [ObservableProperty]
    public System.Collections.Generic.Dictionary<string, int> Values { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("getValuesMap()", ts);
    }

    [Fact(Skip="Bug: Long properties require Int64Value wrapper")]
    public async Task Long_property_should_import_int64_wrapper()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class LongViewModel : ObservableObject
{
    [ObservableProperty]
    public long Total { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("Int64Value", ts);
    }

    [Fact(Skip="Bug: Float properties are missing change notification handling")]
    public async Task Float_property_change_should_be_handled()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class FloatViewModel : ObservableObject
{
    [ObservableProperty]
    public float Level { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("FloatValue.deserializeBinary", ts);
    }
}
