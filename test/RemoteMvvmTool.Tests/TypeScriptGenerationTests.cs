using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using RemoteMvvmTool.Generators;

namespace Bugs;

public class TypeScriptGenerationTests
{
    [Fact]
    public async Task GeneratesInterfacesForDependentTypes()
    {
        var code = @"\
namespace CommunityToolkit.Mvvm.ComponentModel
{
    public class ObservableObject {}
    public class ObservablePropertyAttribute : System.Attribute {}
}
namespace Test;
public class Child
{
    public int Value { get; set; }
}
public partial class ParentViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial Child ChildProp { get; set; }
}
";
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, code);
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { tmp },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs,
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject");
        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        Assert.Contains("export interface ChildState", ts);
    }
}
