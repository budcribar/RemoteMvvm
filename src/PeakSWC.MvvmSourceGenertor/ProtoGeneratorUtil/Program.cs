
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

            [Option('o', "output", Required = true, HelpText = "Output path for the generated .proto file.")]
            public string OutputPath { get; set; } = string.Empty;

            // These would be specific to one ViewModel if multiple are processed into one proto,
            // or you'd generate multiple proto files. For simplicity, assuming one primary ViewModel for the proto.
            [Option('p', "protoNamespace", Required = true, HelpText = "The C# namespace for the generated proto types.")]
            public string ProtoNamespace { get; set; } = string.Empty;

            [Option('s', "serviceName", Required = true, HelpText = "The gRPC service name.")]
            public string GrpcServiceName { get; set; } = string.Empty;

            [Option('a', "attributeFullName", Required = false, Default = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute", HelpText = "The full name of the GenerateGrpcRemoteAttribute.")]
            public string GenerateGrpcRemoteAttributeFullName { get; set; } = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";

            [Option("observablePropertyAttribute", Required = false, Default = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute", HelpText = "Full name of the ObservableProperty attribute.")]
            public string ObservablePropertyAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";

            [Option("relayCommandAttribute", Required = false, Default = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute", HelpText = "Full name of the RelayCommand attribute.")]
            public string RelayCommandAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        }

        // Helper records (mirroring those from the source generator context)
        // These should ideally be in a shared library if used by both the SG and this util.
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
                Console.WriteLine($"Starting .proto file generation...");
                Console.WriteLine($"Processing ViewModel files: {string.Join(", ", opts.ViewModelFiles)}");
                Console.WriteLine($"Output .proto file: {opts.OutputPath}");
                Console.WriteLine($"Proto Namespace: {opts.ProtoNamespace}");
                Console.WriteLine($"gRPC Service Name: {opts.GrpcServiceName}");

                if (!opts.ViewModelFiles.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: No ViewModel files specified.");
                    Console.ResetColor();
                    return; // Or throw an exception
                }

                var syntaxTrees = new List<SyntaxTree>();
                var sourceTexts = new List<string>();

                foreach (var filePath in opts.ViewModelFiles)
                {
                    if (!File.Exists(filePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: ViewModel file not found: {filePath}");
                        Console.ResetColor();
                        return;
                    }
                    var fileContent = await File.ReadAllTextAsync(filePath);
                    sourceTexts.Add(fileContent);
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, path: filePath));
                }

                // Basic Roslyn setup: Create a compilation
                // Add necessary references (e.g., for CommunityToolkit.Mvvm, System.ObjectModel for ObservableCollection)
                // This part might need adjustment based on the actual dependencies of your ViewModels.
                // For simplicity, we'll assume basic types and common MVVM toolkit types.
                // A more robust solution might involve loading project references or using MSBuildWorkspace.
                var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location), // System.ComponentModel
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location), // System.Linq
                MetadataReference.CreateFromFile(typeof(System.Collections.ObjectModel.ObservableCollection<>).Assembly.Location), // System.ObjectModel
                // Attempt to locate CommunityToolkit.Mvvm.dll if needed for attribute symbols.
                // This is a simplification. A robust way is to get exact paths from the build environment.
                // MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "CommunityToolkit.Mvvm.dll")) // Example
            };

                // Try to find CommunityToolkit.Mvvm if it's in the same directory or a known relative path
                // This is a heuristic and might not always work.
                string? toolkitPath = Directory.GetFiles(AppContext.BaseDirectory, "CommunityToolkit.Mvvm.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (toolkitPath != null)
                {
                    references.Add(MetadataReference.CreateFromFile(toolkitPath));
                    Console.WriteLine($"Added reference: {toolkitPath}");
                }
                else
                {
                    Console.WriteLine("Warning: CommunityToolkit.Mvvm.dll not found in AppContext.BaseDirectory. Attribute resolution might fail.");
                }


                Compilation compilation = CSharpCompilation.Create("ViewModelAssembly",
                    syntaxTrees: syntaxTrees,
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                INamedTypeSymbol? mainViewModelSymbol = null;
                string originalVmName = "";

                foreach (var tree in syntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(tree);
                    var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

                    foreach (var classSyntax in classDeclarations)
                    {
                        var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax);
                        if (classSymbol != null)
                        {
                            var generateAttribute = classSymbol.GetAttributes().FirstOrDefault(ad =>
                                ad.AttributeClass?.ToDisplayString() == opts.GenerateGrpcRemoteAttributeFullName);

                            if (generateAttribute != null)
                            {
                                // For simplicity, we assume the first ViewModel with the attribute is the primary one for this .proto file.
                                // A more complex scenario might involve multiple .proto files or merging.
                                mainViewModelSymbol = classSymbol;
                                originalVmName = classSymbol.Name;
                                Console.WriteLine($"Found ViewModel for .proto generation: {originalVmName}");
                                break; // Found the target ViewModel
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
                    return;
                }

                List<PropertyInfo> properties = GetObservableProperties(mainViewModelSymbol, opts.ObservablePropertyAttributeFullName);
                List<CommandInfo> commands = GetRelayCommands(mainViewModelSymbol, opts.RelayCommandAttributeFullName);

                string protoFileContent = GenerateProtoFileContent(
                    opts.ProtoNamespace,
                    opts.GrpcServiceName,
                    originalVmName,
                    properties,
                    commands,
                    compilation);

                try
                {
                    var outputDir = Path.GetDirectoryName(opts.OutputPath);
                    if (outputDir != null && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
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
                            // Ensure the generated command property exists (CommunityToolkit.Mvvm does this)
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


