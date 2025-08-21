using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool.Generators;
using System.Linq;
using Xunit;

namespace RemoteMvvmTool.Tests;

public class ConversionGeneratorBugTests
{
    static CSharpCompilation CreateCompilation(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };
        return CSharpCompilation.Create("TestAssembly", new[] { tree }, references);
    }

    [Fact]
    public void FromProto_Should_Use_ToArray_For_Array_Properties()
    {
        const string code = @"namespace Test{public class Model{public int[] Numbers {get;set;} }}";
        var compilation = CreateCompilation(code);
        var model = compilation.GetTypeByMetadataName("Test.Model");
        var result = ConversionGenerator.Generate("Proto", "Test", new[] { model! }, compilation);
        Assert.Contains("model.Numbers = state.Numbers.ToArray();", result);
    }

    [Fact]
    public void FromProto_Should_Skip_Private_Setters()
    {
        const string code = @"namespace Test{public class Model{public int Value {get;private set;} }}";
        var compilation = CreateCompilation(code);
        var model = compilation.GetTypeByMetadataName("Test.Model");
        var result = ConversionGenerator.Generate("Proto", "Test", new[] { model! }, compilation);
        Assert.DoesNotContain("model.Value =", result);
    }

    [Fact]
    public void Dictionary_Values_Should_Be_Converted()
    {
        const string code = @"using System.Collections.Generic; namespace Test{public class Nested{public int N {get;set;}} public class Model{public Dictionary<string, Nested> Map {get;set;} }}";
        var compilation = CreateCompilation(code);
        var model = compilation.GetTypeByMetadataName("Test.Model");
        var result = ConversionGenerator.Generate("Proto", "Test", new[] { model! }, compilation);
        Assert.Contains("NestedState", result);
    }

    [Fact]
    public void FromProto_Should_Not_Instantiate_Interface_Types()
    {
        const string code = @"namespace Test{public interface IFoo{int A{get;set;}} public class Model{public IFoo Foo {get;set;}} }";
        var compilation = CreateCompilation(code);
        var model = compilation.GetTypeByMetadataName("Test.Model");
        var result = ConversionGenerator.Generate("Proto", "Test", new[] { model! }, compilation);
        Assert.DoesNotContain("new Test.IFoo()", result);
    }

    [Fact]
    public void Nullable_DateTime_Collections_Should_Access_Value()
    {
        const string code = @"using System; using System.Collections.Generic; namespace Test{public class Model{public List<DateTime?> Dates {get;set;} }}";
        var compilation = CreateCompilation(code);
        var model = compilation.GetTypeByMetadataName("Test.Model");
        var result = ConversionGenerator.Generate("Proto", "Test", new[] { model! }, compilation);
        Assert.Contains("e.Value.ToUniversalTime()", result);
    }
}
