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

namespace ProtoGeneratorUtil
{
    // Define command-line options
    public class Options
    {
        [Option('v', "viewModelFiles", Required = true, HelpText = "Paths to the C# ViewModel files to process (comma-separated).", Separator = ',')]
        public IEnumerable<string> ViewModelFiles { get; set; } = Enumerable.Empty<string>();

        [Option('o', "output", Required = false, HelpText = "Output path for the generated .proto file. Default: Protos/{ServiceName}.proto")]
        public string? OutputPath { get; set; } // Made optional, will be derived if not set

        [Option('p', "protoNamespace", Required = false, HelpText = "The C# namespace for the generated proto types. Default: {ViewModelNamespace}.Protos")]
        public string? ProtoNamespace { get; set; } // Made optional, will be derived

        [Option('s', "serviceName", Required = false, HelpText = "The gRPC service name. Default: {ViewModelName}Service")]
        public string? GrpcServiceName { get; set; } // Made optional, will be derived

        [Option('a', "attributeFullName", Required = false, Default = "GenerateGrpcRemoteAttribute", HelpText = "The full name of the GenerateGrpcRemoteAttribute.")]
        public string GenerateGrpcRemoteAttributeFullName { get; set; } = "GenerateGrpcRemoteAttribute";// "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";

        [Option("observablePropertyAttribute", Required = false, Default = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", HelpText = "Full name of the ObservableProperty attribute.")]
        public string ObservablePropertyAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";

        [Option("relayCommandAttribute", Required = false, Default = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", HelpText = "Full name of the RelayCommand attribute.")]
        public string RelayCommandAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        public Options()
        {
            // Default constructor for CommandLineParser
        }
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
            // Initial console output before defaults are applied
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

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.ObjectModel.ObservableCollection<>).Assembly.Location),
            };

            string? toolkitPath = Directory.GetFiles(AppContext.BaseDirectory, "CommunityToolkit.Mvvm.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (toolkitPath != null)
            {
                references.Add(MetadataReference.CreateFromFile(toolkitPath));
                Console.WriteLine($"Added reference: {toolkitPath}");
            }
            else
            {
                // Also try to load from a path relative to the ViewModel file, common in project structures
                // This is a heuristic. For more complex scenarios, MSBuildWorkspace is better.
                var firstViewModelDir = Path.GetDirectoryName(opts.ViewModelFiles.First());
                if (firstViewModelDir.Length == 0)
                    firstViewModelDir = Directory.GetCurrentDirectory();

                if (firstViewModelDir != null)
                {
                    toolkitPath = Directory.GetFiles(firstViewModelDir, "CommunityToolkit.Mvvm.dll", SearchOption.AllDirectories)
                                           .OrderBy(f => f.Length) // Prefer shorter paths (closer to root)
                                           .FirstOrDefault();
                    if (toolkitPath != null)
                    {
                        references.Add(MetadataReference.CreateFromFile(toolkitPath));
                        Console.WriteLine($"Added reference (heuristic): {toolkitPath}");
                    }
                    else
                    {
                        Console.WriteLine("Warning: CommunityToolkit.Mvvm.dll not found. Attribute resolution might fail if types are not in basic references.");
                    }
                }
            }

            Compilation compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";

            // Find the primary ViewModel based on the attribute
            foreach (var tree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classSyntax in classDeclarations)
                {
                    if (semanticModel.GetDeclaredSymbol(classSyntax) is INamedTypeSymbol classSymbol)
                    {
                        var generateAttributeData = classSymbol.GetAttributes().FirstOrDefault(ad =>
                            ad.AttributeClass?.ToDisplayString() == opts.GenerateGrpcRemoteAttributeFullName);

                        if (generateAttributeData != null)
                        {
                            mainViewModelSymbol = classSymbol;
                            originalVmName = classSymbol.Name;
                            Console.WriteLine($"Found ViewModel for .proto generation: {originalVmName}");

                            // If attribute arguments provide defaults, use them (optional)
                            // This assumes your GenerateGrpcRemoteAttribute might have constructor args for these.
                            // For this example, we'll prioritize command-line args or derive.
                            // If you want attribute to override, logic would go here.
                            // Example: opts.ProtoNamespace ??= generateAttributeData.ConstructorArguments[0].Value?.ToString();

                            break;
                        }
                    }
                }
                if (mainViewModelSymbol != null) break;
            }

            if (mainViewModelSymbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: No ViewModel class found with the attribute [{opts.GenerateGrpcRemoteAttributeFullName}].");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            // --- Apply derived defaults if options were not provided ---
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
                // Default to a "Protos" subdirectory relative to the first ViewModel file's directory
                var baseDir = Path.GetDirectoryName(opts.ViewModelFiles.First());
                var protosDir = Path.Combine(baseDir ?? ".", "Protos");
                opts.OutputPath = Path.Combine(protosDir, $"{opts.GrpcServiceName}.proto");
                Console.WriteLine($"Derived Output Path: {opts.OutputPath}");
            }

            // Final check for essential derived values
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

            string protoFileContent = GenerateProtoFileContent(
                opts.ProtoNamespace!, // Now non-null due to derivation or initial value
                opts.GrpcServiceName!, // Now non-null
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

        // --- Helper methods for extracting properties and commands (adapted from source generator) ---
        private static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName)
        {
            var props = new List<PropertyInfo>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    var obsPropAttribute = fieldSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == observablePropertyAttributeFullName);
                    if (obsPropAttribute != null)
                    {
                        string propertyName = fieldSymbol.Name.TrimStart('_');
                        if (propertyName.Length > 0)
                        {
                            propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                        }
                        else continue;

                        var actualPropertySymbol = classSymbol.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
                        if (actualPropertySymbol != null)
                        {
                            props.Add(new PropertyInfo { Name = actualPropertySymbol.Name, TypeString = actualPropertySymbol.Type.ToDisplayString(), FullTypeSymbol = actualPropertySymbol.Type });
                        }
                    }
                }
            }
            Console.WriteLine($"Extracted {props.Count} observable properties.");
            return props;
        }

        private static List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol, string relayCommandAttributeFullName)
        {
            var cmds = new List<CommandInfo>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol)
                {
                    var relayCmdAttribute = methodSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == relayCommandAttributeFullName);
                    if (relayCmdAttribute != null)
                    {
                        string commandPropertyName = methodSymbol.Name + "Command";
                        var actualCommandPropertySymbol = classSymbol.GetMembers(commandPropertyName).OfType<IPropertySymbol>().FirstOrDefault();
                        if (actualCommandPropertySymbol != null)
                        {
                            cmds.Add(new CommandInfo
                            {
                                MethodName = methodSymbol.Name,
                                CommandPropertyName = commandPropertyName,
                                Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoForProto { Name = p.Name, TypeString = p.Type.ToDisplayString(), FullTypeSymbol = p.Type }).ToList(),
                                IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")))
                            });
                        }
                    }
                }
            }
            Console.WriteLine($"Extracted {cmds.Count} relay commands.");
            return cmds;
        }

        // --- .proto generation logic (adapted from source generator) ---
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
            requiredImports.Add("google/protobuf/any.proto"); return "google.protobuf.Any";
        }

        private static string GenerateProtoFileContent(
            string protoNamespaceOption, string grpcServiceName, string originalVmName,
            List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
        {
            var sb = new StringBuilder();
            var requiredImports = new HashSet<string> { "google/protobuf/empty.proto" };

            sb.AppendLine("syntax = \"proto3\";");
            sb.AppendLine();
            sb.AppendLine($"package {protoNamespaceOption.ToLowerInvariant().Replace(".", "_")};");
            sb.AppendLine();
            sb.AppendLine($"option csharp_namespace = \"{protoNamespaceOption}\";");
            sb.AppendLine();

            string vmStateMessageName = $"{originalVmName}State";
            sb.AppendLine($"message {vmStateMessageName} {{");
            int fieldNumber = 1;
            foreach (var prop in props)
            {
                string protoFieldType = GetProtoFieldType(prop.FullTypeSymbol, compilation, requiredImports);
                string protoFieldName = ToSnakeCase(prop.Name);
                sb.AppendLine($"  {protoFieldType} {protoFieldName} = {fieldNumber++};");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            requiredImports.Add("google/protobuf/any.proto");
            sb.AppendLine("message PropertyChangeNotification {");
            sb.AppendLine("  string property_name = 1;");
            sb.AppendLine("  google.protobuf.Any new_value = 2;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("message UpdatePropertyValueRequest {");
            sb.AppendLine("  string property_name = 1;");
            sb.AppendLine("  google.protobuf.Any new_value = 2;");
            sb.AppendLine("}");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                string requestMessageName = $"{cmd.MethodName}Request";
                string responseMessageName = $"{cmd.MethodName}Response";
                sb.AppendLine($"message {requestMessageName} {{");
                fieldNumber = 1;
                foreach (var param in cmd.Parameters)
                {
                    string paramProtoFieldType = GetProtoFieldType(param.FullTypeSymbol, compilation, requiredImports);
                    string paramProtoFieldName = ToSnakeCase(param.Name);
                    sb.AppendLine($"  {paramProtoFieldType} {paramProtoFieldName} = {fieldNumber++};");
                }
                sb.AppendLine("}");
                sb.AppendLine();
                sb.AppendLine($"message {responseMessageName} {{");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine($"service {grpcServiceName} {{");
            sb.AppendLine($"  rpc GetState (google.protobuf.Empty) returns ({vmStateMessageName});");
            sb.AppendLine($"  rpc SubscribeToPropertyChanges (google.protobuf.Empty) returns (stream PropertyChangeNotification);");
            sb.AppendLine($"  rpc UpdatePropertyValue (UpdatePropertyValueRequest) returns (google.protobuf.Empty);");
            foreach (var cmd in cmds)
            {
                sb.AppendLine($"  rpc {cmd.MethodName} ({cmd.MethodName}Request) returns ({cmd.MethodName}Response);");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            var importsBuilder = new StringBuilder();
            foreach (var importPath in requiredImports.OrderBy(x => x))
            {
                importsBuilder.AppendLine($"import \"{importPath}\";");
            }
            importsBuilder.AppendLine();
            return importsBuilder.ToString() + sb.ToString();
        }
    }
}
