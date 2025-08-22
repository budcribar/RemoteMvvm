using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.InteropServices;
namespace ToolExecution;

public class DefaultNamespaceGenerationTests
{
    [Fact]
    public async Task RemoteMvvmTool_Generates_And_Compiles_Simple_ViewModel()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var generatedDir = Path.Combine(vmDir, "generated");
        if (Directory.Exists(generatedDir))
            Directory.Delete(generatedDir, true);

        var args = new[]
        {
            "--output", generatedDir,
            "--protoOutput", Path.Combine(vmDir, "protos"),
            Path.Combine(vmDir, "MainViewModel.cs"),
            Path.Combine(vmDir, "DeviceInfo.cs"),
            Path.Combine(vmDir, "DeviceStatus.cs"),
            Path.Combine(vmDir, "NetworkConfig.cs")
        };
        var exitCode = await Program.Main(args);
        Assert.Equal(0, exitCode);
        // generate gRPC sources from proto
        var protoDir = Path.Combine(vmDir, "protos");
        var protoFile = Path.Combine(protoDir, "MainViewModelService.proto");
        var grpcOut = Path.Combine(generatedDir, "grpc");
        Directory.CreateDirectory(grpcOut);
        RunProtoc(protoDir, protoFile, grpcOut);

        // collect source files
        var sourceFiles = Directory.GetFiles(generatedDir, "*.cs")
            .Concat(new[]
            {
                Path.Combine(vmDir, "DeviceInfo.cs"),
                Path.Combine(vmDir, "DeviceStatus.cs"),
                Path.Combine(vmDir, "NetworkConfig.cs")
            })
            .Concat(Directory.GetFiles(grpcOut, "*.cs"))
            .ToList();

        // add a dispatcher stub so we don't need the WPF assemblies on non-Windows platforms
        var dispatcherStub = @"namespace System.Windows.Threading { public class Dispatcher { public void Invoke(System.Action a) => a(); public System.Threading.Tasks.Task InvokeAsync(System.Action a) { a(); return System.Threading.Tasks.Task.CompletedTask; } public static Dispatcher CurrentDispatcher { get; } = new Dispatcher(); } }";
        var vmStub = @"namespace SimpleViewModelTest.ViewModels { public partial class MainViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { public MainViewModel() { } public System.Collections.Generic.List<DeviceInfo> Devices { get; set; } = new(); public CommunityToolkit.Mvvm.Input.IRelayCommand<DeviceStatus> UpdateStatusCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand<DeviceStatus>(_ => { }); } }";
        var serviceCtorStub = @"public partial class MainViewModelGrpcServiceImpl { public MainViewModelGrpcServiceImpl(SimpleViewModelTest.ViewModels.MainViewModel vm) : this(vm, System.Windows.Threading.Dispatcher.CurrentDispatcher, null) {} }";
        var trees = sourceFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();
        trees.Add(CSharpSyntaxTree.ParseText(dispatcherStub));
        trees.Add(CSharpSyntaxTree.ParseText(vmStub));
        trees.Add(CSharpSyntaxTree.ParseText(serviceCtorStub));

        // gather references from runtime (TPA) and test run directory
        var refs = new System.Collections.Generic.List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa2)
        {
            foreach (var p in tpa2.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    refs.Add(MetadataReference.CreateFromFile(p));
        }
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
        {
            if (!refs.OfType<PortableExecutableReference>().Any(r => string.Equals(r.FilePath, dll, StringComparison.OrdinalIgnoreCase)))
                refs.Add(MetadataReference.CreateFromFile(dll));
        }

        var compilation = CSharpCompilation.Create(
            "GeneratedServerClient",
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
