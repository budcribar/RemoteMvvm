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
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, code);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { tmp },
            "ObservablePropertyAttribute",
            "RelayCommandAttribute",
            refs,
            "ObservableObject");
        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        Assert.Contains("export interface ChildState", ts);
    }
}
