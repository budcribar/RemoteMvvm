using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
// Assuming Helpers class is in this namespace or accessible
// using static YourProject.Helpers; // If GetAllMembers is in a static class

namespace ProtoGeneratorUtil
{
    public class Options
    {
        [Option('v', "viewModelFiles", Required = true, HelpText = "Paths to the C# ViewModel files to process (comma-separated).", Separator = ',')]
        public IEnumerable<string> ViewModelFiles { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output path for the generated .proto file. Default: Protos/{ServiceName}.proto")]
        public string? OutputPath { get; set; }

        [Option('p', "protoNamespace", Required = false, HelpText = "The C# namespace for the generated proto types. Default: {ViewModelNamespace}.Protos")]
        public string? ProtoNamespace { get; set; }

        [Option('s', "serviceName", Required = false, HelpText = "The gRPC service name. Default: {ViewModelName}Service")]
        public string? GrpcServiceName { get; set; }

        [Option('a', "attributeFullName", Required = false, HelpText = "The full name of the GenerateGrpcRemoteAttribute (must match definition in the embedded attribute source).")]
        public string GenerateGrpcRemoteAttributeFullName { get; set; } = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";

        [Option("observablePropertyAttribute", Required = false, HelpText = "Full name of the ObservableProperty attribute.")]
        public string ObservablePropertyAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";

        [Option("relayCommandAttribute", Required = false, HelpText = "Full name of the RelayCommand attribute.")]
        public string RelayCommandAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        // Changed to string to accept semicolon-delimited list from MSBuild
        [Option("referencePaths", Required = false, HelpText = "Semicolon-separated paths to additional reference assemblies (DLLs).")]
        public string ReferencePathsRaw { get; set; }

        // Helper to get the split paths
        public IEnumerable<string> GetReferencePaths() =>
            ReferencePathsRaw?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Enumerable.Empty<string>();


        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ProtoGeneratorUtil.Options))]
        public Options()
        {
            ViewModelFiles = new List<string>();
            ReferencePathsRaw = string.Empty;
        }
    }

    internal record PropertyInfo { public string Name = ""; public string TypeString = ""; public required ITypeSymbol FullTypeSymbol; }
    internal record CommandInfo { public string MethodName = ""; public string CommandPropertyName = ""; public List<ParameterInfoForProto> Parameters = []; public bool IsAsync; }
    internal record ParameterInfoForProto { public string Name = ""; public string TypeString = ""; public required ITypeSymbol FullTypeSymbol; }

    // Assume your Helpers class with GetAllMembers is defined elsewhere and accessible
    // For example, if it's in the same namespace:
    // internal static class Helpers { /* ... */ }


    class Program
    {
        const string AttributeDefinitionResourceName = "ProtoGeneratorUtil.Resources.GenerateGrpcRemoteAttribute.cs";
        const string AttributeDefinitionPlaceholderPath = "embedded://PeakSWC/Mvvm/Remote/GenerateGrpcRemoteAttribute.cs";

        static async Task Main(string[] args)
        {
            Console.WriteLine($"ProtoGeneratorUtil: Executing from AppContext.BaseDirectory: {AppContext.BaseDirectory}");
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (string.IsNullOrEmpty(tpa))
            {
                Console.WriteLine("ProtoGeneratorUtil: Warning: TRUSTED_PLATFORM_ASSEMBLIES is null or empty. Assembly resolution will rely heavily on fallbacks and heuristics.");
            }
            try
            {
                var coreLibPath = typeof(object).Assembly.Location;
                Console.WriteLine($"ProtoGeneratorUtil: Core assembly (System.Object) location: {coreLibPath} (File Exists: {File.Exists(coreLibPath)})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProtoGeneratorUtil: Error resolving core assembly location: {ex.Message}");
            }

            await Parser.Default.ParseArguments<Options>(args)
                           .WithParsedAsync(RunOptionsAndReturnExitCode);
        }

        static async Task RunOptionsAndReturnExitCode(Options opts)
        {
            Console.WriteLine($"ProtoGeneratorUtil: Starting .proto file generation...");
            Console.WriteLine($"  ViewModel files: {string.Join(", ", opts.ViewModelFiles)}");
            Console.WriteLine($"  Raw --referencePaths string: '{opts.ReferencePathsRaw ?? "null"}'"); // Log the raw string
            var explicitRefs = opts.GetReferencePaths().ToList();
            Console.WriteLine($"  Parsed Explicit Reference DLLs ({explicitRefs.Count}): {(explicitRefs.Any() ? string.Join(", ", explicitRefs) : "None")}");
            Console.WriteLine($"  Output .proto (initial): {opts.OutputPath ?? "Not set, will derive"}");
            Console.WriteLine($"  Proto Namespace (initial): {opts.ProtoNamespace ?? "Not set, will derive"}");
            Console.WriteLine($"  gRPC Service Name (initial): {opts.GrpcServiceName ?? "Not set, will derive"}");
            Console.WriteLine($"  GenerateAttribute FQN: {opts.GenerateGrpcRemoteAttributeFullName}");
            Console.WriteLine($"  ObservablePropertyAttribute FQN: {opts.ObservablePropertyAttributeFullName}");
            Console.WriteLine($"  RelayCommandAttribute FQN: {opts.RelayCommandAttributeFullName}");


            if (!opts.ViewModelFiles.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ProtoGeneratorUtil: Error: No ViewModel files specified via --viewModelFiles.");
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
                    Console.WriteLine($"ProtoGeneratorUtil: Error: ViewModel file not found: {filePath}");
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }
                var fileContent = await File.ReadAllTextAsync(filePath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: filePath));
                Console.WriteLine($"ProtoGeneratorUtil: Added syntax tree for ViewModel: {filePath}");
            }

            string? attributeSourceContent = null;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                Console.WriteLine($"ProtoGeneratorUtil: Attempting to load embedded resource: {AttributeDefinitionResourceName}");
                using (Stream? stream = assembly.GetManifestResourceStream(AttributeDefinitionResourceName))
                {
                    if (stream == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"ProtoGeneratorUtil: Error: Embedded resource '{AttributeDefinitionResourceName}' not found.");
                        Console.WriteLine("  Ensure the .cs file is marked as EmbeddedResource in ProtoGeneratorUtil.csproj and the name matches.");
                        Console.WriteLine("  Available manifest resource names in this assembly (ProtoGeneratorUtil.exe):");
                        foreach (var resName in assembly.GetManifestResourceNames())
                        {
                            Console.WriteLine($"    - {resName}");
                        }
                        Console.ResetColor();
                        Environment.ExitCode = 1;
                        return;
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        attributeSourceContent = await reader.ReadToEndAsync();
                    }
                }

                if (!string.IsNullOrEmpty(attributeSourceContent))
                {
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(attributeSourceContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: AttributeDefinitionPlaceholderPath));
                    Console.WriteLine($"ProtoGeneratorUtil: Added syntax tree for embedded attribute definition ('{AttributeDefinitionResourceName}' identified by path '{AttributeDefinitionPlaceholderPath}').");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ProtoGeneratorUtil: Error: Embedded resource '{AttributeDefinitionResourceName}' was found but is empty.");
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ProtoGeneratorUtil: Error loading embedded attribute definition resource '{AttributeDefinitionResourceName}': {ex.GetType().FullName} - {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            var references = new List<MetadataReference>();
            var loadedReferencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<string, string> addRef = (path, source) =>
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && loadedReferencePaths.Add(path))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                        // Console.WriteLine($"ProtoGeneratorUtil: Loaded reference from {source}: {path}"); // Can be very verbose
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not load reference '{path}' (from {source}): {ex.GetType().Name} - {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
                    }
                }
                else if (!File.Exists(path) && !string.IsNullOrEmpty(path))
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Warning: Reference path from {source} does not exist: {path}");
                }
            };

            var msbuildProvidedRefs = opts.GetReferencePaths().ToList();
            if (msbuildProvidedRefs.Any())
            {
                Console.WriteLine($"ProtoGeneratorUtil: Loading {msbuildProvidedRefs.Count} references from --referencePaths (MSBuild)...");
                foreach (var refPath in msbuildProvidedRefs)
                {
                    addRef(refPath, "MSBuild --referencePaths");
                }
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: Warning: No explicit --referencePaths provided by MSBuild. This is unusual and may lead to resolution issues.");
            }

            string? trustedAssembliesPaths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(trustedAssembliesPaths))
            {
                Console.WriteLine("ProtoGeneratorUtil: Attempting to load references from TRUSTED_PLATFORM_ASSEMBLIES...");
                var tpaPaths = trustedAssembliesPaths.Split(Path.PathSeparator);
                int tpaLoadedCount = 0;
                foreach (var path in tpaPaths)
                {
                    if (loadedReferencePaths.Contains(path)) continue; // Already loaded via --referencePaths
                    addRef(path, "TPA");
                    if (loadedReferencePaths.Contains(path)) tpaLoadedCount++;
                }
                Console.WriteLine($"ProtoGeneratorUtil: Loaded {tpaLoadedCount} additional distinct references from TPA.");
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: TRUSTED_PLATFORM_ASSEMBLIES not available or empty.");
            }

            // Fallback for truly essential assemblies if still not found
            // (Should be covered by MSBuild's reference paths normally)
            var essentialTypes = new[] {
                typeof(object), typeof(System.Attribute), typeof(System.String), typeof(System.Int32),
                typeof(System.Collections.Generic.List<>), typeof(System.Linq.Enumerable),
                typeof(System.ComponentModel.INotifyPropertyChanged),
                typeof(System.Threading.Tasks.Task), typeof(System.AttributeUsageAttribute)
            };
            Console.WriteLine("ProtoGeneratorUtil: Fallback check for essential core assembly references via typeof().Assembly.Location...");
            foreach (var type in essentialTypes)
            {
                try
                {
                    if (type.Assembly?.Location != null && !loadedReferencePaths.Contains(type.Assembly.Location))
                    {
                        addRef(type.Assembly.Location, $"Fallback typeof({type.Name})");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not get location for {type.FullName} during fallback: {ex.Message}"); }
            }

            if (!references.Any(r => r.Display?.Contains("CommunityToolkit.Mvvm.dll", StringComparison.OrdinalIgnoreCase) == true))
            {
                Console.WriteLine("ProtoGeneratorUtil: CommunityToolkit.Mvvm.dll not found in explicit or TPA paths. Attempting heuristic load...");
                TryAddAssemblyReference(references, "CommunityToolkit.Mvvm.dll", opts.ViewModelFiles.FirstOrDefault(), isOptional: false, loadedReferencePaths);
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: CommunityToolkit.Mvvm.dll already referenced.");
            }

            Console.WriteLine($"ProtoGeneratorUtil: Total {references.Count} metadata references collected for compilation.");
            if (references.Count < 50 && msbuildProvidedRefs.Any()) // Expect many from MSBuild
            {
                Console.WriteLine("ProtoGeneratorUtil: Warning: Significantly fewer references loaded than provided by MSBuild. Check parsing of --referencePaths and file existence.");
            }
            else if (references.Count < 20) // General low count warning
            {
                Console.WriteLine("ProtoGeneratorUtil: Warning: Very few references loaded. Expect compilation issues.");
            }


            Compilation compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
                    .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                    {
                        { "CS0169", ReportDiagnostic.Suppress },
                        { "CS0414", ReportDiagnostic.Suppress },
                        { "CS8019", ReportDiagnostic.Suppress }
                    }));

            var allDiagnostics = compilation.GetDiagnostics();
            bool hasErrors = false;
            var relevantDiagnostics = allDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error ||
                            (d.Severity == DiagnosticSeverity.Warning && d.Id != "CS0169" && d.Id != "CS0414" && d.Id != "CS8019"))
                .ToList();

            if (relevantDiagnostics.Any())
            {
                Console.WriteLine("--- ProtoGeneratorUtil: Compilation Diagnostics (Errors/Relevant Warnings) ---");
                foreach (var diag in relevantDiagnostics.OrderBy(d => d.Location.SourceTree?.FilePath ?? string.Empty).ThenBy(d => d.Location.SourceSpan.Start))
                {
                    ConsoleColor originalColor = Console.ForegroundColor;
                    var lineSpan = diag.Location.GetLineSpan();
                    string locationString = lineSpan.IsValid ? $"{Path.GetFileName(lineSpan.Path)}({lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1})" : "(No location)";

                    if (diag.Severity == DiagnosticSeverity.Error)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        hasErrors = true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    Console.WriteLine($"{diag.Id}: {diag.GetMessage()} {locationString}");
                    Console.ForegroundColor = originalColor;
                }
                Console.WriteLine("--------------------------------------------------------------------------");
                if (hasErrors)
                {
                    Console.WriteLine("ProtoGeneratorUtil: Compilation has errors. Proto generation might be incomplete or incorrect. Please resolve errors above.");
                    // Don't necessarily exit here if you want to see if *any* symbols can be found for partial generation.
                    // However, for command parameters, type resolution is critical.
                }
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: Compilation completed with no relevant errors or warnings reported by Roslyn.");
            }

            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";

            foreach (var tree in syntaxTrees.Where(st => st.FilePath != AttributeDefinitionPlaceholderPath))
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                if (semanticModel == null)
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not get semantic model for syntax tree '{tree.FilePath}'. Skipping class scan in this tree.");
                    continue;
                }
                var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classSyntax in classDeclarations)
                {
                    if (semanticModel.GetDeclaredSymbol(classSyntax) is INamedTypeSymbol classSymbol)
                    {
                        bool foundGenerateAttribute = false;
                        foreach (var attr in classSymbol.GetAttributes())
                        {
                            string? attrFqn = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                            string? attrShortName = attr.AttributeClass?.Name;

                            if (attrFqn == opts.GenerateGrpcRemoteAttributeFullName ||
                                (attr.AttributeClass != null && attr.AttributeClass.IsUnboundGenericType == false && attrFqn == null && attrShortName == Path.GetFileNameWithoutExtension(opts.GenerateGrpcRemoteAttributeFullName)))
                            {
                                mainViewModelSymbol = classSymbol;
                                originalVmName = classSymbol.Name;
                                Console.WriteLine($"ProtoGeneratorUtil: Found target ViewModel for .proto generation: {originalVmName} (Attribute matched: '{attr.AttributeClass?.ToDisplayString() ?? opts.GenerateGrpcRemoteAttributeFullName}')");
                                foundGenerateAttribute = true;
                                break;
                            }
                        }
                        if (foundGenerateAttribute) break;
                    }
                }
                if (mainViewModelSymbol != null) break;
            }

            if (mainViewModelSymbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ProtoGeneratorUtil: Error: No ViewModel class found with an attribute matching '{opts.GenerateGrpcRemoteAttributeFullName}'.");
                Console.WriteLine($"  Ensure that:");
                Console.WriteLine($"  1. The ViewModel class is present in one of the '--viewModelFiles'.");
                Console.WriteLine($"  2. The attribute '{opts.GenerateGrpcRemoteAttributeFullName}' (namespace: PeakSWC.Mvvm.Remote) is correctly applied to the ViewModel class.");
                Console.WriteLine($"  3. The embedded attribute definition '{AttributeDefinitionResourceName}' was parsed without errors and defines the attribute with the FQN above.");
                Console.WriteLine($"  4. There are no critical compilation errors (see diagnostics above) preventing the attribute from being recognized.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            // ... (Derive OutputPath, ProtoNamespace, GrpcServiceName if not provided - same as before) ...
            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace))
            {
                opts.ProtoNamespace = $"{mainViewModelSymbol.ContainingNamespace.ToDisplayString()}.Protos";
                Console.WriteLine($"ProtoGeneratorUtil: Derived Proto Namespace: {opts.ProtoNamespace}");
            }

            if (string.IsNullOrWhiteSpace(opts.GrpcServiceName))
            {
                opts.GrpcServiceName = $"{originalVmName}Service";
                Console.WriteLine($"ProtoGeneratorUtil: Derived gRPC Service Name: {opts.GrpcServiceName}");
            }

            if (string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                var firstVmFileForPathDerivation = opts.ViewModelFiles.FirstOrDefault(f =>
                   mainViewModelSymbol.Locations.Any(loc => loc.SourceTree?.FilePath.Equals(f, StringComparison.OrdinalIgnoreCase) == true));

                if (firstVmFileForPathDerivation == null && opts.ViewModelFiles.Any())
                {
                    firstVmFileForPathDerivation = opts.ViewModelFiles.First();
                }

                var baseDir = Path.GetDirectoryName(firstVmFileForPathDerivation ?? ".");
                if (string.IsNullOrEmpty(baseDir)) baseDir = ".";

                var protosDir = Path.Combine(baseDir, "Protos");
                opts.OutputPath = Path.Combine(protosDir, $"{opts.GrpcServiceName}.proto");
                Console.WriteLine($"ProtoGeneratorUtil: Derived Output Path: {opts.OutputPath}");
            }

            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace) || string.IsNullOrWhiteSpace(opts.GrpcServiceName) || string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ProtoGeneratorUtil: Error: Could not determine essential options (ProtoNamespace, GrpcServiceName, OutputPath) even after attempting to derive them.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"--- ProtoGeneratorUtil: Final effective options ---");
            Console.WriteLine($"  Output .proto: {opts.OutputPath}");
            Console.WriteLine($"  Proto Namespace: {opts.ProtoNamespace}");
            Console.WriteLine($"  gRPC Service Name: {opts.GrpcServiceName}");
            Console.WriteLine($"-------------------------------------------------");

            List<PropertyInfo> properties = GetObservableProperties(mainViewModelSymbol, opts.ObservablePropertyAttributeFullName, compilation);
            List<CommandInfo> commands = GetRelayCommands(mainViewModelSymbol, opts.RelayCommandAttributeFullName, compilation);

            // ... (Warning logic for properties/commands not found - same as before) ...
            bool hasObservableFields = Helpers.GetAllMembers(mainViewModelSymbol).OfType<IFieldSymbol>().Any(f =>
               f.GetAttributes().Any(a => (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.ObservablePropertyAttributeFullName ||
                                           a.AttributeClass?.Name == opts.ObservablePropertyAttributeFullName ||
                                           a.AttributeClass?.Name == Path.GetFileNameWithoutExtension(opts.ObservablePropertyAttributeFullName))));

            bool hasCommandMethods = Helpers.GetAllMembers(mainViewModelSymbol).OfType<IMethodSymbol>().Any(m =>
                m.GetAttributes().Any(a => (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.RelayCommandAttributeFullName ||
                                            a.AttributeClass?.Name == opts.RelayCommandAttributeFullName ||
                                            a.AttributeClass?.Name == Path.GetFileNameWithoutExtension(opts.RelayCommandAttributeFullName))));

            if (!properties.Any() && hasObservableFields)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"ProtoGeneratorUtil: Warning: Fields with '{opts.ObservablePropertyAttributeFullName}' (or similar name) were found in '{originalVmName}', but no observable properties were successfully extracted. Check attribute name and compilation diagnostics (especially for CommunityToolkit.Mvvm.dll).");
                Console.ResetColor();
            }
            if (!commands.Any() && hasCommandMethods)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"ProtoGeneratorUtil: Warning: Methods with '{opts.RelayCommandAttributeFullName}' (or similar name) were found in '{originalVmName}', but no relay commands were successfully extracted. Check attribute name and compilation diagnostics (especially for CommunityToolkit.Mvvm.dll).");
                Console.ResetColor();
            }

            string protoFileContent = GenerateProtoFileContent(
                opts.ProtoNamespace!,
                opts.GrpcServiceName!,
                originalVmName,
                properties,
                commands,
                compilation);

            // ... (File writing logic - same as before) ...
            try
            {
                var outputDir = Path.GetDirectoryName(opts.OutputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"ProtoGeneratorUtil: Created output directory: {outputDir}");
                }
                await File.WriteAllTextAsync(opts.OutputPath, protoFileContent, Encoding.UTF8);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"ProtoGeneratorUtil: Successfully generated .proto file at: {opts.OutputPath}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ProtoGeneratorUtil: Error writing .proto file: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }

        private static void TryAddAssemblyReferenceByObject(List<MetadataReference> references, Type typeInAssembly, HashSet<string> loadedReferencePaths)
        {
            try
            {
                if (typeInAssembly?.Assembly?.Location != null)
                {
                    string location = typeInAssembly.Assembly.Location;
                    if (File.Exists(location) && loadedReferencePaths.Add(location)) // Use HashSet
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Console.WriteLine($"  Added core reference from Type '{typeInAssembly.FullName}': {location}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not load core reference for assembly of type '{typeInAssembly?.FullName ?? "Unknown"}': {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
            }
        }
        private static void TryAddAssemblyReferenceByName(List<MetadataReference> references, string assemblyName, HashSet<string> loadedReferencePaths)
        {
            try
            {
                var assembly = Assembly.Load(assemblyName);
                if (assembly?.Location != null)
                {
                    string location = assembly.Location;
                    if (File.Exists(location) && loadedReferencePaths.Add(location)) // Use HashSet
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Console.WriteLine($"  Added core reference by Name '{assemblyName}': {location}");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"  Warning: Core assembly '{assemblyName}' not found by Assembly.Load. Reference not added.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not load core reference for assembly '{assemblyName}' by name: {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
            }
        }

        // Updated to accept and use loadedReferencePaths
        private static void TryAddAssemblyReference(List<MetadataReference> references, string dllName, string? hintFilePath, bool isOptional, HashSet<string> loadedReferencePaths)
        {
            string? foundPath = null;
            // ... (your existing search logic for foundPath - no changes needed here) ...
            if (Directory.Exists(AppContext.BaseDirectory))
            {
                foundPath = Directory.GetFiles(AppContext.BaseDirectory, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            }
            if (foundPath == null && !string.IsNullOrEmpty(hintFilePath))
            {
                var hintDir = Path.GetDirectoryName(hintFilePath);
                if (string.IsNullOrEmpty(hintDir)) hintDir = Directory.GetCurrentDirectory();
                for (int i = 0; i < 4 && !string.IsNullOrEmpty(hintDir) && foundPath == null; i++)
                {
                    string[] configurations = { "Debug", "Release" };
                    string[] tfms = { "net8.0", "net7.0", "net6.0", "net5.0", "netstandard2.1", "netstandard2.0", "" };
                    foreach (var config in configurations)
                    {
                        var configBinPath = Path.Combine(hintDir, "bin", config);
                        if (Directory.Exists(configBinPath))
                        {
                            foundPath = Directory.GetFiles(configBinPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                            if (foundPath != null) break;
                        }
                        foreach (var tfm in tfms)
                        {
                            var tfmPath = string.IsNullOrEmpty(tfm) ? configBinPath : Path.Combine(configBinPath, tfm);
                            if (Directory.Exists(tfmPath))
                            {
                                foundPath = Directory.GetFiles(tfmPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                                if (foundPath != null) break;
                            }
                        }
                        if (foundPath != null) break;
                    }
                    if (foundPath == null && Directory.Exists(hintDir))
                    {
                        foundPath = Directory.GetFiles(hintDir, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    }
                    if (foundPath != null) break;
                    hintDir = Path.GetDirectoryName(hintDir);
                }
            }
            if (foundPath == null)
            {
                string nugetPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
                if (Directory.Exists(nugetPackagesPath))
                {
                    string packageNameNoExt = Path.GetFileNameWithoutExtension(dllName).ToLowerInvariant();
                    var packageDir = Path.Combine(nugetPackagesPath, packageNameNoExt);
                    if (Directory.Exists(packageDir))
                    {
                        foundPath = Directory.GetFiles(packageDir, dllName, SearchOption.AllDirectories)
                                          .Where(p => p.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar) || p.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar))
                                          .OrderByDescending(f => f.ToLowerInvariant().Contains("net8.0") ? 30 : f.ToLowerInvariant().Contains("net7.0") ? 20 : f.ToLowerInvariant().Contains("net6.0") ? 10 : f.ToLowerInvariant().Contains("netstandard2.0") ? 5 : 0)
                                          .ThenByDescending(f => f).FirstOrDefault();
                    }
                }
            }
            if (foundPath == null)
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? Path.Combine(programFiles, "dotnet");
                if (!Directory.Exists(dotnetRoot) && programFiles != programFilesX86) dotnetRoot = Path.Combine(programFilesX86, "dotnet");
                string[] rootsToSearch = { Path.Combine(dotnetRoot, "packs"), Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App"), Path.Combine(dotnetRoot, "shared", "Microsoft.AspNetCore.App") };
                foreach (var root in rootsToSearch.Where(Directory.Exists))
                {
                    try
                    {
                        foundPath = Directory.GetFiles(root, dllName, SearchOption.AllDirectories)
                                          .OrderByDescending(f => f.ToLowerInvariant().Contains("net8.0") ? 30 : f.ToLowerInvariant().Contains("net7.0") ? 20 : f.ToLowerInvariant().Contains("net6.0") ? 10 : 0)
                                          .ThenByDescending(f => f).FirstOrDefault();
                        if (foundPath != null) break;
                    }
                    catch { }
                }
            }

            if (foundPath != null && File.Exists(foundPath))
            {
                if (loadedReferencePaths.Add(foundPath)) // Use the passed HashSet
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(foundPath));
                        Console.WriteLine($"ProtoGeneratorUtil: Added reference for '{dllName}': {foundPath} (via heuristic search)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not create MetadataReference for found file '{foundPath}': {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
                    }
                }
                else
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Info: Reference for '{dllName}' ({foundPath}) already added or attempted.");
                }
            }
            else
            {
                Console.WriteLine($"ProtoGeneratorUtil: {(isOptional ? "Info: Optional assembly" : "Warning: Could not find")} '{dllName}' via heuristic search. Attribute resolution and type analysis might be affected.");
            }
        }


        private static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName, Compilation compilation)
        {
            var props = new List<PropertyInfo>();
            var processedPropertyNames = new HashSet<string>(StringComparer.Ordinal);

            INamedTypeSymbol? expectedAttributeSymbol = compilation.GetTypeByMetadataName(observablePropertyAttributeFullName);
            if (expectedAttributeSymbol == null)
            {
                Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not find the attribute symbol for ObservableProperty: '{observablePropertyAttributeFullName}'. Property detection might fail.");
            }

            Console.WriteLine($"ProtoGeneratorUtil: Scanning for ObservableProperties in {classSymbol.Name} (and base types) using attribute '{observablePropertyAttributeFullName}'.");
            Console.WriteLine($"  (Resolved expected attribute symbol: {expectedAttributeSymbol?.ToDisplayString() ?? "NOT FOUND"})");


            foreach (var member in Helpers.GetAllMembers(classSymbol))
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    foreach (var attrData in fieldSymbol.GetAttributes())
                    {
                        INamedTypeSymbol? appliedAttributeClass = attrData.AttributeClass;
                        if (appliedAttributeClass == null) continue;

                        bool isMatch = false;
                        if (expectedAttributeSymbol != null)
                        {
                            if (SymbolEqualityComparer.Default.Equals(appliedAttributeClass.OriginalDefinition, expectedAttributeSymbol.OriginalDefinition))
                            {
                                isMatch = true;
                            }
                        }
                        if (!isMatch) // Fallback if symbol comparison didn't work or symbol not found
                        {
                            string appliedAttrFQN = appliedAttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                            if (appliedAttrFQN == observablePropertyAttributeFullName) isMatch = true;
                        }


                        if (isMatch)
                        {
                            string propertyName = fieldSymbol.Name.TrimStart('_');
                            if (propertyName.Length > 0 && char.IsLower(propertyName[0]))
                            {
                                propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                            }
                            else if (propertyName.Length == 0 || (propertyName.Length > 0 && !char.IsLetter(propertyName[0])))
                            {
                                continue;
                            }

                            if (processedPropertyNames.Add(propertyName))
                            {
                                Console.WriteLine($"    -> MATCHED ObservablePropertyAttribute for field '{fieldSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', generating property '{propertyName}' (Type: {fieldSymbol.Type.ToDisplayString()}).");
                                props.Add(new PropertyInfo { Name = propertyName, TypeString = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FullTypeSymbol = fieldSymbol.Type });
                            }
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"ProtoGeneratorUtil: Extracted {props.Count} observable properties from '{classSymbol.Name}' and its base types.");
            return props;
        }

        private static List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol, string relayCommandAttributeFullName, Compilation compilation)
        {
            var cmds = new List<CommandInfo>();
            var processedCommandPropertyNames = new HashSet<string>(StringComparer.Ordinal);

            INamedTypeSymbol? expectedAttributeSymbol = compilation.GetTypeByMetadataName(relayCommandAttributeFullName);
            if (expectedAttributeSymbol == null)
            {
                Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not find the attribute symbol for RelayCommand: '{relayCommandAttributeFullName}'. Command detection might fail.");
            }

            Console.WriteLine($"ProtoGeneratorUtil: Scanning for RelayCommands in {classSymbol.Name} (and base types) using attribute '{relayCommandAttributeFullName}'.");
            Console.WriteLine($"  (Resolved expected attribute symbol: {expectedAttributeSymbol?.ToDisplayString() ?? "NOT FOUND"})");

            foreach (var member in Helpers.GetAllMembers(classSymbol))
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary && !methodSymbol.IsStatic && !methodSymbol.IsOverride)
                {
                    foreach (var attrData in methodSymbol.GetAttributes())
                    {
                        INamedTypeSymbol? appliedAttributeClass = attrData.AttributeClass;
                        if (appliedAttributeClass == null) continue;

                        bool isMatch = false;
                        if (expectedAttributeSymbol != null)
                        {
                            if (SymbolEqualityComparer.Default.Equals(appliedAttributeClass.OriginalDefinition, expectedAttributeSymbol.OriginalDefinition))
                            {
                                isMatch = true;
                            }
                        }
                        if (!isMatch)
                        {
                            string appliedAttrFQN = appliedAttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                            if (appliedAttrFQN == relayCommandAttributeFullName) isMatch = true;
                        }

                        if (isMatch)
                        {
                            string commandPropertyName = methodSymbol.Name;
                            if (commandPropertyName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                            {
                                commandPropertyName = commandPropertyName.Substring(0, commandPropertyName.Length - 5);
                            }
                            if (commandPropertyName.Length > 0 && char.IsLower(commandPropertyName[0]))
                            {
                                commandPropertyName = char.ToUpperInvariant(commandPropertyName[0]) + commandPropertyName.Substring(1);
                            }
                            else if (commandPropertyName.Length == 0 || (commandPropertyName.Length > 0 && !char.IsLetter(commandPropertyName[0])))
                            {
                                continue;
                            }
                            commandPropertyName += "Command";

                            if (processedCommandPropertyNames.Add(commandPropertyName))
                            {
                                Console.WriteLine($"    -> MATCHED RelayCommandAttribute for method '{methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', generating command property '{commandPropertyName}'.");
                                cmds.Add(new CommandInfo
                                {
                                    MethodName = methodSymbol.Name,
                                    CommandPropertyName = commandPropertyName,
                                    Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoForProto { Name = p.Name, TypeString = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FullTypeSymbol = p.Type }).ToList(),
                                    IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.ToDisplayString() == "System.Threading.Tasks.Task" || (rtSym.IsGenericType && rtSym.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")))
                                });
                            }
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"ProtoGeneratorUtil: Extracted {cmds.Count} relay commands from '{classSymbol.Name}' and its base types.");
            return cmds;
        }

        private static string ToSnakeCase(string pascalCaseName)
        {
            if (string.IsNullOrEmpty(pascalCaseName)) return pascalCaseName;
            return Regex.Replace(pascalCaseName, "(?<=[a-z0-9])[A-Z]|(?<=[A-Z])[A-Z](?=[a-z])|^[A-Z]{2,}(?=[A-Z][a-z0-9]|$)|(?<=[a-z])[A-Z0-9]", "_$0").ToLower().TrimStart('_');
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
                case SpecialType.System_Decimal:
                    Console.WriteLine($"ProtoGeneratorUtil: Info: System.Decimal ('{typeSymbol.ToDisplayString()}') mapped to 'string'.");
                    return "string";
                case SpecialType.System_Object:
                    requiredImports.Add("google/protobuf/any.proto");
                    return "google.protobuf.Any";
            }

            string fullTypeName = typeSymbol.OriginalDefinition.ToDisplayString();
            switch (fullTypeName)
            {
                case "System.TimeSpan":
                    requiredImports.Add("google/protobuf/duration.proto");
                    return "google.protobuf.Duration";
                case "System.DateTimeOffset":
                    requiredImports.Add("google/protobuf/timestamp.proto");
                    return "google.protobuf.Timestamp";
                case "System.Guid": return "string";
                case "System.Uri": return "string";
                case "System.Version": return "string";
                case "System.Numerics.BigInteger": return "string";
                case "System.Windows.Threading.DispatcherTimer":
                    Console.WriteLine($"ProtoGeneratorUtil: Warning: Type '{typeSymbol.ToDisplayString()}' (DispatcherTimer) cannot be directly mapped. Mapping to 'google.protobuf.Any'.");
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
                var originalDefFqn = namedType.OriginalDefinition.ToDisplayString();
                if (originalDefFqn == "System.Collections.Generic.List<T>" ||
                    originalDefFqn == "System.Collections.Generic.IList<T>" ||
                    originalDefFqn == "System.Collections.Generic.ICollection<T>" ||
                    originalDefFqn == "System.Collections.Generic.IEnumerable<T>" ||
                    originalDefFqn == "System.Collections.Generic.IReadOnlyList<T>" ||
                    originalDefFqn == "System.Collections.Generic.IReadOnlyCollection<T>" ||
                    originalDefFqn == "System.Collections.ObjectModel.ObservableCollection<T>" ||
                    originalDefFqn == "System.Collections.ObjectModel.ReadOnlyObservableCollection<T>")
                {
                    if (namedType.TypeArguments.Length > 0)
                    {
                        return $"repeated {GetProtoFieldType(namedType.TypeArguments[0], compilation, requiredImports)}";
                    }
                }

                if (originalDefFqn == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
                    originalDefFqn == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                    originalDefFqn == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
                {
                    if (namedType.TypeArguments.Length == 2)
                    {
                        ITypeSymbol keyTypeSymbol = namedType.TypeArguments[0];
                        ITypeSymbol valueTypeSymbol = namedType.TypeArguments[1];

                        if (IsProtoMapKeyType(keyTypeSymbol))
                        {
                            var keyProtoType = GetProtoFieldType(keyTypeSymbol, compilation, requiredImports);
                            var valueProtoType = GetProtoFieldType(valueTypeSymbol, compilation, requiredImports);
                            return $"map<{keyProtoType}, {valueProtoType}>";
                        }
                        else
                        {
                            Console.WriteLine($"ProtoGeneratorUtil: Warning: Dictionary key type '{keyTypeSymbol.ToDisplayString()}' is not a valid Protobuf map key type. Mapping entire Dictionary to 'google.protobuf.Any'.");
                            requiredImports.Add("google/protobuf/any.proto"); return "google.protobuf.Any";
                        }
                    }
                }
            }

            Console.WriteLine($"ProtoGeneratorUtil: Warning: Unhandled type '{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' encountered. Mapping to 'google.protobuf.Any'.");
            requiredImports.Add("google/protobuf/any.proto");
            return "google.protobuf.Any";
        }


        private static string GenerateProtoFileContent(
             string protoNamespaceOption, string grpcServiceName, string originalVmName,
             List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
        {
            var bodySb = new StringBuilder();
            var requiredImports = new HashSet<string> { "google/protobuf/empty.proto" };

            string vmStateMessageName = $"{originalVmName}State";
            bodySb.AppendLine($"// Message representing the full state of the {originalVmName}");
            bodySb.AppendLine($"message {vmStateMessageName} {{");
            int fieldNumber = 1;
            if (!props.Any())
            {
                bodySb.AppendLine("  // No observable properties were mapped for the state message.");
            }
            foreach (var prop in props.OrderBy(p => p.Name))
            {
                string protoFieldType = GetProtoFieldType(prop.FullTypeSymbol, compilation, requiredImports);
                string protoFieldName = ToSnakeCase(prop.Name);
                if (string.IsNullOrWhiteSpace(protoFieldName))
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Warning: Skipping property '{prop.Name}' due to empty snake_case name.");
                    continue;
                }
                bodySb.AppendLine($"  {protoFieldType} {protoFieldName} = {fieldNumber++}; // Original C#: {prop.TypeString} {prop.Name}");
            }
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            bodySb.AppendLine("message PropertyChangeNotification {");
            bodySb.AppendLine("  string property_name = 1;");
            bodySb.AppendLine("  google.protobuf.Any new_value = 2;");
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            bodySb.AppendLine("message UpdatePropertyValueRequest {");
            bodySb.AppendLine("  string property_name = 1;");
            bodySb.AppendLine("  google.protobuf.Any new_value = 2;");
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            foreach (var cmd in cmds.OrderBy(c => c.MethodName))
            {
                string requestMessageName = $"{cmd.MethodName}Request";
                string responseMessageName = $"{cmd.MethodName}Response";

                bodySb.AppendLine($"message {requestMessageName} {{");
                fieldNumber = 1;
                if (!cmd.Parameters.Any())
                {
                    bodySb.AppendLine("  // This command takes no parameters.");
                }
                foreach (var param in cmd.Parameters)
                {
                    string paramProtoFieldType = GetProtoFieldType(param.FullTypeSymbol, compilation, requiredImports);
                    string paramProtoFieldName = ToSnakeCase(param.Name);
                    if (string.IsNullOrWhiteSpace(paramProtoFieldName))
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Skipping parameter '{param.Name}' for command '{cmd.MethodName}' due to empty snake_case name.");
                        continue;
                    }
                    bodySb.AppendLine($"  {paramProtoFieldType} {paramProtoFieldName} = {fieldNumber++}; // Original C#: {param.TypeString} {param.Name}");
                }
                bodySb.AppendLine("}");
                bodySb.AppendLine();

                bodySb.AppendLine($"message {responseMessageName} {{");
                bodySb.AppendLine("  // Add fields here if the command returns data.");
                bodySb.AppendLine("}");
                bodySb.AppendLine();
            }

            bodySb.AppendLine($"service {grpcServiceName} {{");
            bodySb.AppendLine($"  rpc GetState (google.protobuf.Empty) returns ({vmStateMessageName});");
            bodySb.AppendLine($"  rpc SubscribeToPropertyChanges (google.protobuf.Empty) returns (stream PropertyChangeNotification);");
            bodySb.AppendLine($"  rpc UpdatePropertyValue (UpdatePropertyValueRequest) returns (google.protobuf.Empty);");
            foreach (var cmd in cmds.OrderBy(c => c.MethodName))
            {
                bodySb.AppendLine($"  rpc {cmd.MethodName} ({cmd.MethodName}Request) returns ({cmd.MethodName}Response);");
            }
            bodySb.AppendLine("}");
            bodySb.AppendLine();


            var finalProtoSb = new StringBuilder();
            finalProtoSb.AppendLine("syntax = \"proto3\";");
            finalProtoSb.AppendLine();
            string protoPackageName = protoNamespaceOption.ToLowerInvariant().Replace(".", "_");
            finalProtoSb.AppendLine($"package {protoPackageName};");
            finalProtoSb.AppendLine();
            finalProtoSb.AppendLine($"option csharp_namespace = \"{protoNamespaceOption}\";");
            finalProtoSb.AppendLine();

            if (bodySb.ToString().Contains("google.protobuf.Any")) requiredImports.Add("google/protobuf/any.proto");

            foreach (var importPath in requiredImports.OrderBy(x => x))
            {
                finalProtoSb.AppendLine($"import \"{importPath}\";");
            }
            finalProtoSb.AppendLine();
            finalProtoSb.Append(bodySb.ToString());

            return finalProtoSb.ToString();
        }
    }
}