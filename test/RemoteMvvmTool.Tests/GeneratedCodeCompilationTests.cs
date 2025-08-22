using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RemoteMvvmTool;
using Xunit;

namespace ToolExecution;
[CollectionDefinition("Compile", DisableParallelization = true)]
public class CompileCollectionDefinition { }

[Collection("Compile")]
public class GeneratedCodeCompilationTests
{
    [Theory]
    [InlineData("string", "int")]
    [InlineData("int", "string")]
    [InlineData("double", "float")]
    [InlineData("float", "double")]
    [InlineData("bool", "string")]
    [InlineData("SampleEnum", "int")]
    [InlineData("Guid", "SampleEnum")]
    [InlineData("short", "long")]
    [InlineData("byte", "bool")]
    [InlineData("string", "NestedType")]
    [InlineData("SampleEnum", "Dictionary<int, string>")]
    [InlineData("long", "DateTime")]
    public async Task Generated_Code_Compiles_For_Dictionary_Types(string keyType, string valueType)
    {
        await GenerateAndCompileAsync(keyType, valueType);
    }

    static async Task GenerateAndCompileAsync(string keyType, string valueType)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmDir = tempDir;
        var vmFile = Path.Combine(vmDir, "TestViewModel.cs");
        var viewModelCode = @$"using System;\nusing System.Collections.Generic;\nusing CommunityToolkit.Mvvm.ComponentModel;\nusing CommunityToolkit.Mvvm.Input;\n\nnamespace GeneratedTests;\n\npublic partial class TestViewModel : ObservableObject\n{{\n    [ObservableProperty]\n    private Dictionary<{keyType}, {valueType}> map;\n\n    [RelayCommand]\n    void DoThing() {{ }}\n\n    public enum SampleEnum {{ A, B, C }}\n    public class NestedType {{ public int Value {{ get; set; }} }}\n}}".Replace("\\n", "\n");
        File.WriteAllText(vmFile, viewModelCode);

        var generatedDir = Path.Combine(vmDir, "generated");
        var protoDir = Path.Combine(vmDir, "protos");
        var args = new[]
        {
            "--output", generatedDir,
            "--protoOutput", protoDir,
            vmFile
        };
        var exitCode = await Program.Main(args);
        Assert.Equal(0, exitCode);

        var protoFile = Path.Combine(protoDir, "TestViewModelService.proto");
        var grpcOut = Path.Combine(generatedDir, "grpc");
        Directory.CreateDirectory(grpcOut);
        RunProtoc(protoDir, protoFile, grpcOut);

        var sourceFiles = Directory.GetFiles(generatedDir, "*.cs")
            .Concat(Directory.GetFiles(grpcOut, "*.cs"))
            .ToList();

        var stubCode = @$"using System;\nusing System.Collections.Generic;\nusing CommunityToolkit.Mvvm.ComponentModel;\nusing CommunityToolkit.Mvvm.Input;\n\nnamespace GeneratedTests;\n\npublic partial class TestViewModel : ObservableObject\n{{\n    public Dictionary<{keyType}, {valueType}> Map {{ get; set; }} = new();\n    public IRelayCommand DoThingCommand {{ get; }} = new RelayCommand(() => {{ }});\n    public enum SampleEnum {{ A, B, C }}\n    public class NestedType {{ public int Value {{ get; set; }} }}\n}}".Replace("\\n", "\n");
        var stubFile = Path.Combine(vmDir, "TestViewModelStub.cs");
        File.WriteAllText(stubFile, stubCode);
        sourceFiles.Add(stubFile);

        var dispatcherStub = "namespace System.Windows.Threading { public class Dispatcher { public void Invoke(System.Action a) => a(); public System.Threading.Tasks.Task InvokeAsync(System.Action a) { a(); return System.Threading.Tasks.Task.CompletedTask; } public static Dispatcher CurrentDispatcher { get; } = new Dispatcher(); } }";
        var serviceCtorStub = "public partial class TestViewModelGrpcServiceImpl { public TestViewModelGrpcServiceImpl(GeneratedTests.TestViewModel vm) : this(vm, System.Windows.Threading.Dispatcher.CurrentDispatcher, null) {} }";

        var trees = sourceFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();
        trees.Add(CSharpSyntaxTree.ParseText(dispatcherStub));
        trees.Add(CSharpSyntaxTree.ParseText(serviceCtorStub));

        var refs = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    refs.Add(MetadataReference.CreateFromFile(p));
        }
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
        {
            if (!refs.OfType<PortableExecutableReference>().Any(r => string.Equals(r.FilePath, dll, StringComparison.OrdinalIgnoreCase)))
                refs.Add(MetadataReference.CreateFromFile(dll));
        }

        var compilation = CSharpCompilation.Create(
            "GeneratedDictTest",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emitResult = compilation.Emit(Stream.Null);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));
    }

    static void RunProtoc(string protoDir, string protoFile, string outDir)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolsRoot = Path.Combine(home, ".nuget", "packages", "grpc.tools");
        var versionDir = Directory.GetDirectories(toolsRoot).OrderBy(p => p).Last();
        string osPart = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macosx" : "linux";
        string archPart = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
        bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var protoc = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "protoc.exe" : "protoc");
        var plugin = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "grpc_csharp_plugin.exe" : "grpc_csharp_plugin");
        var includeDir = Path.Combine(versionDir, "build", "native", "include");

        var psi = new ProcessStartInfo
        {
            FileName = protoc,
            Arguments = $"--csharp_out \"{outDir}\" --grpc_out \"{outDir}\" --plugin=protoc-gen-grpc=\"{plugin}\" -I\"{protoDir}\" -I\"{includeDir}\" \"{protoFile}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var msg = proc.StandardError.ReadToEnd();
            throw new Exception($"protoc failed: {msg}");
        }
    }
}
