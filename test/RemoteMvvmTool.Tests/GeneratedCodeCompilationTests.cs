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

    static async Task GenerateAndCompileAsync(string keyType, string valueType, string? customModelCode = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmDir = tempDir;
        var vmFile = Path.Combine(vmDir, "TestViewModel.cs");
        
        string viewModelCode;
        if (!string.IsNullOrEmpty(customModelCode))
        {
            viewModelCode = customModelCode;
        }
        else
        {
            viewModelCode = @$"using System;\nusing System.Collections.Generic;\nusing CommunityToolkit.Mvvm.ComponentModel;\nusing CommunityToolkit.Mvvm.Input;\n\nnamespace GeneratedTests;\n\npublic partial class TestViewModel : ObservableObject\n{{\n    [ObservableProperty]\n    private Dictionary<{keyType}, {valueType}> map;\n\n    [RelayCommand]\n    void DoThing() {{ }}\n\n    public enum SampleEnum {{ A, B, C }}\n    public class NestedType {{ public int Value {{ get; set; }} }}\n}}".Replace("\\n", "\n");
        }
        
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

        string stubCode;
        if (!string.IsNullOrEmpty(customModelCode))
        {
            // For custom model code, create a minimal stub that implements the interface
            stubCode = """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;
                using CommunityToolkit.Mvvm.Input;

                namespace GeneratedTests;

                // Stub implementation to satisfy compilation - the actual implementation comes from customModelCode
                public partial class TestViewModel : ObservableObject
                {
                    // Add any missing command implementations
                    public IRelayCommand? StartGameCommand { get; }
                }
                """;
        }
        else
        {
            stubCode = @$"using System;\nusing System.Collections.Generic;\nusing CommunityToolkit.Mvvm.ComponentModel;\nusing CommunityToolkit.Mvvm.Input;\n\nnamespace GeneratedTests;\n\npublic partial class TestViewModel : ObservableObject\n{{\n    public Dictionary<{keyType}, {valueType}> Map {{ get; set; }} = new();\n    public IRelayCommand DoThingCommand {{ get; }} = new RelayCommand(() => {{ }});\n    public enum SampleEnum {{ A, B, C }}\n    public class NestedType {{ public int Value {{ get; set; }} }}\n}}".Replace("\\n", "\n");
        }
        
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

    [Theory]
    [InlineData("ListOfDictionaries")]
    [InlineData("DictionaryOfLists")]  
    [InlineData("EdgeCasePrimitives")]
    [InlineData("NestedCustomObjects")]
    [InlineData("EmptyCollections")]
    [InlineData("MemoryTypes")]
    [InlineData("LargeCollections")]
    [InlineData("MixedComplexTypes")]
    public async Task Generated_Code_Compiles_For_EdgeCase_Types(string testCaseType)
    {
        var modelCode = testCaseType switch
        {
            "ListOfDictionaries" => """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private ObservableCollection<Dictionary<string, int>> _metricsByRegion = new();
                    
                    [ObservableProperty]
                    private int _totalRegions = 0;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "DictionaryOfLists" => """
                using System;
                using System.Collections.Generic;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private Dictionary<string, List<double>> _scoresByCategory = new();
                    
                    [ObservableProperty]
                    private int _categoryCount = 0;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "EdgeCasePrimitives" => """
                using System;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private decimal _preciseValue;

                    [ObservableProperty]
                    private nuint _tinyValue;

                    [ObservableProperty]
                    private ulong _bigValue;

                    [ObservableProperty]
                    private DateOnly _birthDate;

                    [ObservableProperty]
                    private TimeOnly _startTime;

                    [ObservableProperty]
                    private short _negativeShort;

                    [ObservableProperty]
                    private byte _positiveByte;

                    [ObservableProperty]
                    private char _unicodeChar;

                    [ObservableProperty]
                    private Guid _emptyGuid = Guid.Empty;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "NestedCustomObjects" => """
                using System;
                using System.Collections.Generic;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private CompanyInfo _company = new();
                    
                    [ObservableProperty]
                    private bool _isActiveCompany = false;

                    [ObservableProperty]
                    private DateTime _lastUpdate = DateTime.MinValue;

                    public class CompanyInfo
                    {
                        public string Name { get; set; } = "";
                        public int EmployeeCount { get; set; }
                        public List<Department> Departments { get; set; } = new();
                    }

                    public class Department
                    {
                        public string Name { get; set; } = "";
                        public int HeadCount { get; set; }
                        public double Budget { get; set; }
                    }

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "EmptyCollections" => """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private ObservableCollection<string> _emptyList = new();

                    [ObservableProperty]
                    private Dictionary<int, string> _emptyDict = new();

                    [ObservableProperty]
                    private int? _nullableInt;

                    [ObservableProperty]
                    private double? _nullableDouble;

                    [ObservableProperty]
                    private List<int> _singleItemList = new();

                    [ObservableProperty]
                    private Dictionary<string, bool> _singleItemDict = new();

                    [ObservableProperty]
                    private List<int> _zeroValues = new();

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "MemoryTypes" => """
                using System;
                using System.Collections.Generic;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private byte[] _imageData = Array.Empty<byte>();

                    [ObservableProperty]
                    private Memory<byte> _bufferData;

                    [ObservableProperty]
                    private ReadOnlyMemory<byte> _readOnlyBuffer;

                    [ObservableProperty]
                    private List<byte> _bytesList = new();

                    [ObservableProperty]
                    private int _dataLength = 0;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "LargeCollections" => """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private ObservableCollection<int> _largeNumberList = new();

                    [ObservableProperty]
                    private Dictionary<string, int> _largeStringDict = new();

                    [ObservableProperty]
                    private List<Dictionary<int, string>> _nestedLargeDicts = new();

                    [ObservableProperty]
                    private Dictionary<int, List<double>> _dictOfLargeLists = new();

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "MixedComplexTypes" => """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;
                using CommunityToolkit.Mvvm.Input;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private GameMode _gameState = GameMode.Inactive;

                    [ObservableProperty]
                    private ObservableCollection<Player> _players = new();

                    [ObservableProperty]
                    private Dictionary<StatType, List<double>> _statistics = new();

                    [ObservableProperty]
                    private Guid _sessionId;

                    [ObservableProperty]
                    private DateTime _startTime;

                    [ObservableProperty]
                    private int _totalSessions = 0;

                    [RelayCommand]
                    private void StartGame() => GameState = GameMode.Active;

                    public enum GameMode { Inactive = 0, Active = 1, Paused = 2 }
                    public enum StatType { DamageDealt = 10, HealingDone = 20 }
                    
                    public class Player
                    {
                        public string Name { get; set; } = "";
                        public float Score { get; set; }
                        public int Level { get; set; }
                        public bool IsActive { get; set; }
                    }

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            _ => throw new ArgumentException($"Unknown test case: {testCaseType}")
        };

        await GenerateAndCompileAsync("mixed", "complex", modelCode);
    }

    [Theory]
    [InlineData("SimpleMemory")]
    [InlineData("BasicPrimitives")] 
    [InlineData("SimpleCollections")]
    public async Task EdgeCase_Primitive_Types_Compile_Successfully(string testCaseType)
    {
        var modelCode = testCaseType switch
        {
            "SimpleMemory" => """
                using System;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private byte[] _imageData = Array.Empty<byte>();

                    [ObservableProperty]
                    private int _dataLength = 0;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "BasicPrimitives" => """
                using System;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private decimal _preciseValue;

                    [ObservableProperty]
                    private char _unicodeChar;

                    [ObservableProperty]
                    private Guid _emptyGuid = Guid.Empty;

                    [ObservableProperty]
                    private Half _halfValue;

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            "SimpleCollections" => """
                using System;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;
                using CommunityToolkit.Mvvm.ComponentModel;

                namespace GeneratedTests;

                public partial class TestViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private ObservableCollection<string> _stringList = new();

                    [ObservableProperty]
                    private List<int> _numberList = new();

                    [ObservableProperty]
                    private int[] _numberArray = Array.Empty<int>();

                    public enum SampleEnum { A, B, C }
                    public class NestedType { public int Value { get; set; } }
                }
                """,

            _ => throw new ArgumentException($"Unknown test case: {testCaseType}")
        };

        await GenerateAndCompileAsync("simple", "test", modelCode);
    }
}
