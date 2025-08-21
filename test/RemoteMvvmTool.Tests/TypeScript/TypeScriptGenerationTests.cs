using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.TypeScript;

public class TypeScriptGenerationTests
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

    [Fact]
    public async Task GeneratesInterfacesForDependentTypes()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public class Child
{
    public int Value { get; set; }
}
public partial class ParentViewModel : ObservableObject
{
    [ObservableProperty]
    public Child ChildProp { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("export interface ChildState", ts);
    }

    [Fact]
    public async Task Maps_Primitive_Types_To_TS_Primitives()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class PrimitiveViewModel : ObservableObject
{
    [ObservableProperty]
    public string Name { get; set; }
    [ObservableProperty]
    public bool IsActive { get; set; }
    [ObservableProperty]
    public int Age { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("name: string;", ts);
        Assert.Contains("isActive: boolean;", ts);
        Assert.Contains("age: number;", ts);
    }

    [Fact]
    public async Task Maps_Array_Types_To_TS_Arrays()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class ArrayViewModel : ObservableObject
{
    [ObservableProperty]
    public int[] Numbers { get; set; }
    [ObservableProperty]
    public System.Collections.Generic.List<string> Names { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("numbers: number[];", ts);
        Assert.Contains("names: string[];", ts);
    }

    [Fact]
    public async Task Maps_Dictionary_To_Record()
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
        Assert.Contains("values: Record<string, number>;", ts);
    }

    [Fact]
    public async Task Maps_Enum_To_Number()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public enum Status { Active, Inactive }
public partial class EnumViewModel : ObservableObject
{
    [ObservableProperty]
    public Status CurrentStatus { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("currentStatus: number;", ts);
    }

    [Fact]
    public async Task Generates_Command_Methods()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public class RelayCommandAttribute : System.Attribute {}
public partial class CommandViewModel : ObservableObject
{
    [ObservableProperty]
    public int Count { get; set; }

    [RelayCommand]
    public void DoStuff(int value) {}
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("DoStuffRequest", ts);
        Assert.Contains("async doStuff(value: any): Promise<void>", ts);
    }

    [Fact]
    public async Task Maps_DateTime_To_Date()
    {
        var code = @"\
public class ObservablePropertyAttribute : System.Attribute {}
public partial class DateViewModel : ObservableObject
{
    [ObservableProperty]
    public System.DateTime When { get; set; }
}
public class ObservableObject {}
";
        var ts = await GenerateTsAsync(code);
        Assert.Contains("when: Date;", ts);
    }
}
