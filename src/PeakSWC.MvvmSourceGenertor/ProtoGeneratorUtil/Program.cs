using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection; // For Assembly.Location

namespace ProtoGeneratorUtil
{
    // Define command-line options
    public class Options
    {
        [Option('v', "viewModelFiles", Required = true, HelpText = "Paths to the C# ViewModel files to process (comma-separated).", Separator = ',')]
        public IEnumerable<string> ViewModelFiles { get; set; } = Enumerable.Empty<string>();

        [Option('o', "output", Required = false, HelpText = "Output path for the generated .proto file. Default: Protos/{ServiceName}.proto")]
        public string? OutputPath { get; set; }

        [Option('p', "protoNamespace", Required = false, HelpText = "The C# namespace for the generated proto types. Default: {ViewModelNamespace}.Protos")]
        public string? ProtoNamespace { get; set; }

        [Option('s', "serviceName", Required = false, HelpText = "The gRPC service name. Default: {ViewModelName}Service")]
        public string? GrpcServiceName { get; set; }

        [Option('a', "attributeFullName", Required = false, Default = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute", HelpText = "The full name of the GenerateGrpcRemoteAttribute.")]
        public string GenerateGrpcRemoteAttributeFullName { get; set; } = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";

        [Option("observablePropertyAttribute", Required = false, Default = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", HelpText = "Full name of the ObservableProperty attribute.")]
        public string ObservablePropertyAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";

        [Option("relayCommandAttribute", Required = false, Default = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", HelpText = "Full name of the RelayCommand attribute.")]
        public string RelayCommandAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        public Options() { }
    }

    // Helper records
    internal record PropertyInfo { public string Name; public string TypeString; public ITypeSymbol FullTypeSymbol; }
    internal record CommandInfo { public string MethodName; public string CommandPropertyName; public List<ParameterInfoForProto> Parameters; public bool IsAsync; }
    internal record ParameterInfoForProto { public string Name; public string TypeString; public ITypeSymbol FullTypeSymbol; }


    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                   .WithParsedAsync(RunOptionsAndReturnExitCode);
        }

        static async Task RunOptionsAndReturnExitCode(Options opts)
        {
            Console.WriteLine($"Starting .proto file generation with initial options...");
            Console.WriteLine($"  ViewModel files: {string.Join(", ", opts.ViewModelFiles)}");
            Console.WriteLine($"  Output .proto (initial): {opts.OutputPath ?? "Not set, will derive"}");
            Console.WriteLine($"  Proto Namespace (initial): {opts.ProtoNamespace ?? "Not set, will derive"}");
            Console.WriteLine($"  gRPC Service Name (initial): {opts.GrpcServiceName ?? "Not set, will derive"}");
            Console.WriteLine($"  GenerateAttribute: {opts.GenerateGrpcRemoteAttributeFullName}");
            Console.WriteLine($"  ObservablePropertyAttribute: {opts.ObservablePropertyAttributeFullName}");
            Console.WriteLine($"  RelayCommandAttribute: {opts.RelayCommandAttributeFullName}");

            if (!opts.ViewModelFiles.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No ViewModel files specified.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in opts.ViewModelFiles)
            {
                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: ViewModel file not found: {filePath}");
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }
                var fileContent = await File.ReadAllTextAsync(filePath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, path: filePath));
            }

            var references = new List<MetadataReference>();

            string? trustedAssembliesPaths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(trustedAssembliesPaths))
            {
                var paths = trustedAssembliesPaths.Split(Path.PathSeparator);
                foreach (var path in paths)
                {
                    if (File.Exists(path) && !references.Any(r => r.Display?.Equals(path, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        try
                        {
                            references.Add(MetadataReference.CreateFromFile(path));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not load TPA '{path}': {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"Loaded {references.Count} references from TRUSTED_PLATFORM_ASSEMBLIES.");
            }
            else
            {
                Console.WriteLine("Warning: TRUSTED_PLATFORM_ASSEMBLIES not available. Falling back to manual reference loading.");
                references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                try { references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)); } catch { Console.WriteLine("Warning: Could not load System.Runtime by Assembly.Load"); }
                try { references.Add(MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location)); } catch { /* ... */ }
            }

            TryAddAssemblyReference(references, "CommunityToolkit.Mvvm.dll", opts.ViewModelFiles.FirstOrDefault());
            string remoteAttributeDllName = "RemoteAttribute.dll";
            TryAddAssemblyReference(references, remoteAttributeDllName, opts.ViewModelFiles.FirstOrDefault(), false);

            Compilation compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var allDiagnostics = compilation.GetDiagnostics();
            bool hasErrors = false;
            var relevantDiagnostics = allDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error || (d.Severity == DiagnosticSeverity.Warning && d.Id != "CS0169" && d.Id != "CS0414"))
                .ToList();

            if (relevantDiagnostics.Any())
            {
                Console.WriteLine("--- Compilation Diagnostics (Errors/Relevant Warnings) ---");
                foreach (var diag in relevantDiagnostics.OrderBy(d => d.Location.SourceTree?.FilePath ?? string.Empty).ThenBy(d => d.Location.SourceSpan.Start))
                {
                    if (diag.Severity == DiagnosticSeverity.Error)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        hasErrors = true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    Console.WriteLine($"{diag.Id}: {diag.GetMessage()} (Location: {diag.Location})");
                    Console.ResetColor();
                }
                Console.WriteLine("---------------------------------------------------------");
                if (hasErrors)
                {
                    Console.WriteLine("Compilation has errors. Proto generation might be incomplete or incorrect.");
                }
            }


            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";

            foreach (var tree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                if (semanticModel == null)
                {
                    Console.WriteLine($"Warning: Could not get semantic model for {tree.FilePath}. Skipping.");
                    continue;
                }
                var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classSyntax in classDeclarations)
                {
                    if (semanticModel.GetDeclaredSymbol(classSyntax) is INamedTypeSymbol classSymbol)
                    {
                        Console.WriteLine($"Checking class: {classSymbol.ToDisplayString()} for GenerateGrpcRemoteAttribute.");
                        foreach (var attr in classSymbol.GetAttributes())
                        {
                            Console.WriteLine($"  Class {classSymbol.Name} has attribute: {attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))} (Short: {attr.AttributeClass?.Name})");
                        }

                        var generateAttributeData = classSymbol.GetAttributes().FirstOrDefault(ad =>
                            ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.GenerateGrpcRemoteAttributeFullName ||
                            ad.AttributeClass?.Name == opts.GenerateGrpcRemoteAttributeFullName
                            );

                        if (generateAttributeData != null)
                        {
                            mainViewModelSymbol = classSymbol;
                            originalVmName = classSymbol.Name;
                            Console.WriteLine($"Found ViewModel for .proto generation: {originalVmName} using attribute '{generateAttributeData.AttributeClass?.ToDisplayString()}'");
                            break;
                        }
                    }
                }
                if (mainViewModelSymbol != null) break;
            }

            if (mainViewModelSymbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: No ViewModel class found with the attribute matching '{opts.GenerateGrpcRemoteAttributeFullName}'.");
                Console.WriteLine($"Ensure the attribute name is correct and the defining assembly ('{remoteAttributeDllName}') is referenced and found.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace))
            {
                opts.ProtoNamespace = $"{mainViewModelSymbol.ContainingNamespace.ToDisplayString()}.Protos";
                Console.WriteLine($"Derived Proto Namespace: {opts.ProtoNamespace}");
            }

            if (string.IsNullOrWhiteSpace(opts.GrpcServiceName))
            {
                opts.GrpcServiceName = $"{originalVmName}Service";
                Console.WriteLine($"Derived gRPC Service Name: {opts.GrpcServiceName}");
            }

            if (string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                var baseDir = Path.GetDirectoryName(opts.ViewModelFiles.First());
                if (string.IsNullOrEmpty(baseDir)) baseDir = ".";
                var protosDir = Path.Combine(baseDir, "Protos");
                opts.OutputPath = Path.Combine(protosDir, $"{opts.GrpcServiceName}.proto");
                Console.WriteLine($"Derived Output Path: {opts.OutputPath}");
            }

            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace) || string.IsNullOrWhiteSpace(opts.GrpcServiceName) || string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Could not determine essential options (ProtoNamespace, GrpcServiceName, OutputPath) even after attempting to derive them.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"--- Final effective options ---");
            Console.WriteLine($"  Output .proto: {opts.OutputPath}");
            Console.WriteLine($"  Proto Namespace: {opts.ProtoNamespace}");
            Console.WriteLine($"  gRPC Service Name: {opts.GrpcServiceName}");
            Console.WriteLine($"-------------------------------");

            List<PropertyInfo> properties = GetObservableProperties(mainViewModelSymbol, opts.ObservablePropertyAttributeFullName);
            List<CommandInfo> commands = GetRelayCommands(mainViewModelSymbol, opts.RelayCommandAttributeFullName);

            if (!properties.Any() && !commands.Any() && mainViewModelSymbol.GetMembers().OfType<IFieldSymbol>().Any(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "ObservablePropertyAttribute" || a.AttributeClass?.ToDisplayString().Contains("ObservablePropertyAttribute") == true)))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: No observable properties or relay commands were extracted from ViewModel '{originalVmName}', but fields/methods with corresponding attributes might exist.");
                Console.WriteLine("This often indicates an issue with resolving attribute symbols from referenced assemblies (e.g., CommunityToolkit.Mvvm.dll or " + remoteAttributeDllName + "). Check compilation diagnostics above.");
                Console.ResetColor();
            }


            string protoFileContent = GenerateProtoFileContent(
                opts.ProtoNamespace!,
                opts.GrpcServiceName!,
                originalVmName,
                properties,
                commands,
                compilation);

            try
            {
                var outputDir = Path.GetDirectoryName(opts.OutputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"Created output directory: {outputDir}");
                }
                await File.WriteAllTextAsync(opts.OutputPath, protoFileContent, Encoding.UTF8);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully generated .proto file at: {opts.OutputPath}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error writing .proto file: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }

        private static void TryAddAssemblyReference(List<MetadataReference> references, string dllName, string? firstViewModelFilePath, bool isOptional = false, bool isWpfFramework = false)
        {
            string? foundPath = null;

            if (Directory.Exists(AppContext.BaseDirectory))
            {
                foundPath = Directory.GetFiles(AppContext.BaseDirectory, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            }

            if (foundPath == null && isWpfFramework)
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string[] dotnetSdkPackRoots = { Path.Combine(programFiles, "dotnet", "packs"), Path.Combine(programFilesX86, "dotnet", "packs") };

                foreach (var root in dotnetSdkPackRoots)
                {
                    if (Directory.Exists(root))
                    {
                        try
                        {
                            foundPath = Directory.GetFiles(root, dllName, SearchOption.AllDirectories)
                                               .Where(p => p.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar) && (p.Contains("net8.0") || p.Contains("net7.0") || p.Contains("net6.0")))
                                               .OrderByDescending(p => p)
                                               .FirstOrDefault();
                            if (foundPath != null) break;
                        }
                        catch (Exception ex) { Console.WriteLine($"  Minor error searching SDK packs for {dllName}: {ex.Message.Split('\n')[0]}"); }
                    }
                }
            }

            if (foundPath == null && !string.IsNullOrEmpty(firstViewModelFilePath))
            {
                var viewModelDir = Path.GetDirectoryName(firstViewModelFilePath);
                if (string.IsNullOrEmpty(viewModelDir))
                {
                    viewModelDir = Directory.GetCurrentDirectory();
                }
                string? currentSearchDir = viewModelDir;
                for (int i = 0; i < 4 && !string.IsNullOrEmpty(currentSearchDir); i++)
                {
                    string[] commonBuildDirs = {
                        Path.Combine(currentSearchDir, "bin", "Debug"),
                        Path.Combine(currentSearchDir, "bin", "Release"),
                    };
                    foreach (var buildDir in commonBuildDirs)
                    {
                        if (Directory.Exists(buildDir))
                        {
                            foundPath = Directory.GetFiles(buildDir, dllName, SearchOption.AllDirectories)
                                              .OrderBy(f => f.Length)
                                              .FirstOrDefault();
                            if (foundPath != null) break;
                        }
                    }
                    if (foundPath != null) break;

                    if (Directory.Exists(currentSearchDir))
                    {
                        foundPath = Directory.GetFiles(currentSearchDir, dllName, SearchOption.AllDirectories)
                                            .OrderBy(f => f.Length)
                                            .FirstOrDefault();
                        if (foundPath != null) break;
                    }
                    currentSearchDir = Path.GetDirectoryName(currentSearchDir);
                }
            }

            if (foundPath != null && File.Exists(foundPath))
            {
                if (references.Any(r => r.Display?.Equals(foundPath, StringComparison.OrdinalIgnoreCase) == true))
                {
                    Console.WriteLine($"Reference for '{dllName}' already added: {foundPath}");
                }
                else
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(foundPath));
                        Console.WriteLine($"Added reference (heuristic): {foundPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not create MetadataReference for '{foundPath}': {ex.Message.Split('\n')[0]}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"{(isOptional ? "Info: Optional assembly" : "Warning: Could not find")} '{dllName}'. Attribute resolution and type analysis might be affected.");
            }
        }

        private static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName)
        {
            var props = new List<PropertyInfo>();
            Console.WriteLine($"Scanning for ObservableProperties in {classSymbol.Name} using attribute name '{observablePropertyAttributeFullName}'...");
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    foreach (var attr in fieldSymbol.GetAttributes())
                    {
                        string attrClassFQN = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "null_FQN";
                        string attrClassName = attr.AttributeClass?.Name ?? "null_Name";

                        if (attrClassFQN == observablePropertyAttributeFullName || attrClassName == observablePropertyAttributeFullName)
                        {
                            Console.WriteLine($"      MATCHED ObservablePropertyAttribute for field '{fieldSymbol.Name}' (FQN='{attrClassFQN}', Short='{attrClassName}')!");
                            string propertyName = fieldSymbol.Name.TrimStart('_');
                            if (propertyName.Length > 0)
                            {
                                propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                            }
                            else
                            {
                                Console.WriteLine($"        Skipping field '{fieldSymbol.Name}' due to invalid derived property name.");
                                continue;
                            }
                            props.Add(new PropertyInfo { Name = propertyName, TypeString = fieldSymbol.Type.ToDisplayString(), FullTypeSymbol = fieldSymbol.Type });
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"Extracted {props.Count} observable properties from '{classSymbol.Name}'.");
            return props;
        }

        private static List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol, string relayCommandAttributeFullName)
        {
            var cmds = new List<CommandInfo>();
            Console.WriteLine($"Scanning for RelayCommands in {classSymbol.Name} using attribute name '{relayCommandAttributeFullName}'...");
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol)
                {
                    foreach (var attr in methodSymbol.GetAttributes())
                    {
                        string attrClassFQN = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "null_FQN";
                        string attrClassName = attr.AttributeClass?.Name ?? "null_Name";

                        if (attrClassFQN == relayCommandAttributeFullName || attrClassName == relayCommandAttributeFullName)
                        {
                            Console.WriteLine($"      MATCHED RelayCommandAttribute for method '{methodSymbol.Name}' (FQN='{attrClassFQN}', Short='{attrClassName}')!");
                            string commandPropertyName = methodSymbol.Name + "Command";
                            cmds.Add(new CommandInfo
                            {
                                MethodName = methodSymbol.Name,
                                CommandPropertyName = commandPropertyName,
                                Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoForProto { Name = p.Name, TypeString = p.Type.ToDisplayString(), FullTypeSymbol = p.Type }).ToList(),
                                IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")))
                            });
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"Extracted {cmds.Count} relay commands from '{classSymbol.Name}'.");
            return cmds;
        }

        private static string ToSnakeCase(string pascalCaseName)
        {
            if (string.IsNullOrEmpty(pascalCaseName)) return pascalCaseName;
            return Regex.Replace(pascalCaseName, "(?<=[a-z0-9])[A-Z]|(?<=[A-Z])[A-Z](?=[a-z])", "_$0").ToLower();
        }

        private static bool IsProtoMapKeyType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedTypeSymbolNullable &&
                namedTypeSymbolNullable.IsGenericType &&
                namedTypeSymbolNullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            {
                typeSymbol = namedTypeSymbolNullable.TypeArguments[0];
            }
            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String:
                case SpecialType.System_Boolean:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                    return true;
            }
            return typeSymbol.TypeKind == TypeKind.Enum;
        }

        private static string GetProtoFieldType(ITypeSymbol typeSymbol, Compilation compilation, HashSet<string> requiredImports)
        {
            if (typeSymbol is INamedTypeSymbol namedTypeSymbolNullable &&
                namedTypeSymbolNullable.IsGenericType &&
                namedTypeSymbolNullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            {
                typeSymbol = namedTypeSymbolNullable.TypeArguments[0];
            }

            if (typeSymbol.TypeKind == TypeKind.Enum) return "int32";

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String: return "string";
                case SpecialType.System_Boolean: return "bool";
                case SpecialType.System_Single: return "float";
                case SpecialType.System_Double: return "double";
                case SpecialType.System_Int32: return "int32";
                case SpecialType.System_Int64: return "int64";
                case SpecialType.System_UInt32: return "uint32";
                case SpecialType.System_UInt64: return "uint64";
                case SpecialType.System_SByte: return "int32";
                case SpecialType.System_Byte: return "uint32";
                case SpecialType.System_Int16: return "int32";
                case SpecialType.System_UInt16: return "uint32";
                case SpecialType.System_Char: return "string";
                case SpecialType.System_DateTime:
                    requiredImports.Add("google/protobuf/timestamp.proto");
                    return "google.protobuf.Timestamp";
                case SpecialType.System_Decimal: return "string";
                case SpecialType.System_Object:
                    requiredImports.Add("google/protobuf/any.proto");
                    return "google.protobuf.Any";
            }

            string fullTypeName = typeSymbol.OriginalDefinition.ToDisplayString();
            switch (fullTypeName)
            {
                case "System.TimeSpan": requiredImports.Add("google/protobuf/duration.proto"); return "google.protobuf.Duration";
                case "System.Guid": return "string";
                case "System.DateTimeOffset": requiredImports.Add("google/protobuf/timestamp.proto"); return "google.protobuf.Timestamp";
                case "System.Uri": return "string";
                case "System.Version": return "string";
                case "System.Numerics.BigInteger": return "string";
                case "System.Windows.Threading.DispatcherTimer":
                    Console.WriteLine($"Warning: DispatcherTimer type '{typeSymbol.ToDisplayString()}' cannot be directly mapped to a proto field. Mapping to Any.");
                    requiredImports.Add("google/protobuf/any.proto");
                    return "google.protobuf.Any";
            }

            if (typeSymbol.TypeKind == TypeKind.Array && typeSymbol is IArrayTypeSymbol arraySymbol)
            {
                if (arraySymbol.ElementType.SpecialType == SpecialType.System_Byte && arraySymbol.Rank == 1) return "bytes";
                return $"repeated {GetProtoFieldType(arraySymbol.ElementType, compilation, requiredImports)}";
            }

            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var originalDef = namedType.OriginalDefinition.ToDisplayString();
                if (originalDef == "System.Collections.Generic.List<T>" ||
                    originalDef == "System.Collections.Generic.IList<T>" ||
                    originalDef == "System.Collections.Generic.IReadOnlyList<T>" ||
                    originalDef == "System.Collections.Generic.IEnumerable<T>" ||
                    originalDef == "System.Collections.ObjectModel.ObservableCollection<T>")
                {
                    return $"repeated {GetProtoFieldType(namedType.TypeArguments[0], compilation, requiredImports)}";
                }
                if (originalDef == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
                    originalDef == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                    originalDef == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
                {
                    if (IsProtoMapKeyType(namedType.TypeArguments[0]))
                    {
                        var keyType = GetProtoFieldType(namedType.TypeArguments[0], compilation, requiredImports);
                        var valueType = GetProtoFieldType(namedType.TypeArguments[1], compilation, requiredImports);
                        return $"map<{keyType}, {valueType}>";
                    }
                    else
                    {
                        requiredImports.Add("google/protobuf/any.proto"); return "google.protobuf.Any";
                    }
                }
            }
            Console.WriteLine($"Warning: Unhandled type '{typeSymbol.ToDisplayString()}' encountered. Mapping to 'google.protobuf.Any'. Consider adding explicit mapping for this type.");
            requiredImports.Add("google/protobuf/any.proto");
            return "google.protobuf.Any";
        }

        private static string GenerateProtoFileContent(
            string protoNamespaceOption, string grpcServiceName, string originalVmName,
            List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
        {
            // --- MODIFICATION START: Ensure correct import order ---
            var bodySb = new StringBuilder(); // For messages and service
            var requiredImports = new HashSet<string> { "google/protobuf/empty.proto" };

            // Messages for Properties and State
            string vmStateMessageName = $"{originalVmName}State";
            bodySb.AppendLine($"// Message representing the full state of the {originalVmName}");
            bodySb.AppendLine($"message {vmStateMessageName} {{");
            int fieldNumber = 1;
            if (!props.Any())
            {
                bodySb.AppendLine("  // No observable properties found or mapped.");
            }
            foreach (var prop in props)
            {
                string protoFieldType = GetProtoFieldType(prop.FullTypeSymbol, compilation, requiredImports);
                string protoFieldName = ToSnakeCase(prop.Name);
                bodySb.AppendLine($"  {protoFieldType} {protoFieldName} = {fieldNumber++};");
            }
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            // Common messages for property updates
            // Ensure Any is imported if used (GetProtoFieldType handles adding to requiredImports)
            bodySb.AppendLine("// Message for property change notifications");
            bodySb.AppendLine("message PropertyChangeNotification {");
            bodySb.AppendLine("  string property_name = 1;");
            bodySb.AppendLine("  google.protobuf.Any new_value = 2;"); // This will add "google/protobuf/any.proto" to requiredImports
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            bodySb.AppendLine("// Request to update a property's value");
            bodySb.AppendLine("message UpdatePropertyValueRequest {");
            bodySb.AppendLine("  string property_name = 1;");
            bodySb.AppendLine("  google.protobuf.Any new_value = 2;"); // This will add "google/protobuf/any.proto"
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            // Messages for Commands
            foreach (var cmd in cmds)
            {
                string requestMessageName = $"{cmd.MethodName}Request";
                string responseMessageName = $"{cmd.MethodName}Response";

                bodySb.AppendLine($"// Request message for {cmd.MethodName} command");
                bodySb.AppendLine($"message {requestMessageName} {{");
                fieldNumber = 1;
                foreach (var param in cmd.Parameters)
                {
                    string paramProtoFieldType = GetProtoFieldType(param.FullTypeSymbol, compilation, requiredImports);
                    string paramProtoFieldName = ToSnakeCase(param.Name);
                    bodySb.AppendLine($"  {paramProtoFieldType} {paramProtoFieldName} = {fieldNumber++};");
                }
                bodySb.AppendLine("}");
                bodySb.AppendLine();

                bodySb.AppendLine($"// Response message for {cmd.MethodName} command");
                bodySb.AppendLine($"message {responseMessageName} {{");
                bodySb.AppendLine("}");
                bodySb.AppendLine();
            }

            // Service Definition
            bodySb.AppendLine($"service {grpcServiceName} {{");
            bodySb.AppendLine($"  // RPC for getting the initial state");
            bodySb.AppendLine($"  rpc GetState (google.protobuf.Empty) returns ({vmStateMessageName});");
            bodySb.AppendLine();
            bodySb.AppendLine($"  // RPC for subscribing to property changes");
            bodySb.AppendLine($"  rpc SubscribeToPropertyChanges (google.protobuf.Empty) returns (stream PropertyChangeNotification);");
            bodySb.AppendLine();
            bodySb.AppendLine($"  // RPC for a client to update a property value on the server");
            bodySb.AppendLine($"  rpc UpdatePropertyValue (UpdatePropertyValueRequest) returns (google.protobuf.Empty);");
            bodySb.AppendLine();

            foreach (var cmd in cmds)
            {
                bodySb.AppendLine($"  // RPC for {cmd.MethodName} command"); // This was a typo, should be bodySb
                bodySb.AppendLine($"  rpc {cmd.MethodName} ({cmd.MethodName}Request) returns ({cmd.MethodName}Response);");
            }
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            // --- Build the final .proto content in the correct order ---
            var finalProtoSb = new StringBuilder();
            finalProtoSb.AppendLine("syntax = \"proto3\";");
            finalProtoSb.AppendLine();
            finalProtoSb.AppendLine($"package {protoNamespaceOption.ToLowerInvariant().Replace(".", "_")};");
            finalProtoSb.AppendLine();
            finalProtoSb.AppendLine($"option csharp_namespace = \"{protoNamespaceOption}\";");
            finalProtoSb.AppendLine();

            // Add imports AFTER syntax, package, and options
            // Ensure "google/protobuf/any.proto" is in requiredImports if it was used by GetProtoFieldType
            if (bodySb.ToString().Contains("google.protobuf.Any"))
            { // Simple check
                requiredImports.Add("google/protobuf/any.proto");
            }
            foreach (var importPath in requiredImports.OrderBy(x => x))
            {
                finalProtoSb.AppendLine($"import \"{importPath}\";");
            }
            finalProtoSb.AppendLine();

            finalProtoSb.Append(bodySb.ToString()); // Append messages and service

            return finalProtoSb.ToString();
            // --- MODIFICATION END ---
        }
    }
}
