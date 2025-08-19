using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using GrpcRemoteMvvmModelUtil;
using Xunit;

namespace SampleViewModel;

public class HelpersTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var references = new List<MetadataReference>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    references.Add(MetadataReference.CreateFromFile(p));
                }
            }
        }
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private const string TestCode = @"namespace TestNs {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class FooAttribute : System.Attribute { }
    public class Base { public int BaseField; }
    [Foo]
    public class Derived : Base { public void Method() { } }
    public class Unrelated { }
}";

    [Fact]
    public void AttributeMatches_SupportsVariousNames()
    {
        var compilation = CreateCompilation(TestCode);
        var derived = compilation.GetTypeByMetadataName("TestNs.Derived")!;
        var attr = derived.GetAttributes().Single();

        Assert.True(Helpers.AttributeMatches(attr, "TestNs.FooAttribute"));
        Assert.True(Helpers.AttributeMatches(attr, "TestNs.Foo"));
        Assert.True(Helpers.AttributeMatches(attr, "FooAttribute"));
        Assert.True(Helpers.AttributeMatches(attr, "Foo"));
        Assert.False(Helpers.AttributeMatches(attr, "Bar"));
    }

    [Fact]
    public void InheritsFrom_HandlesBaseTypes()
    {
        var compilation = CreateCompilation(TestCode);
        var derived = compilation.GetTypeByMetadataName("TestNs.Derived")!;

        Assert.True(Helpers.InheritsFrom(derived, "TestNs.Base"));
        Assert.False(Helpers.InheritsFrom(derived, "TestNs.Unrelated"));
    }

    [Fact]
    public void GetAllMembers_IncludesBaseMembers()
    {
        var compilation = CreateCompilation(TestCode);
        var derived = compilation.GetTypeByMetadataName("TestNs.Derived")!;

        var memberNames = Helpers.GetAllMembers(derived).Select(m => m.Name).ToList();

        Assert.Contains("BaseField", memberNames);
        Assert.Contains("Method", memberNames);
    }
}
