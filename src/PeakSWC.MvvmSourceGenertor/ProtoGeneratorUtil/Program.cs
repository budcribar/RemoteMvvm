using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Reflection; // For Assembly.Location
using System.Text;
using System.Text.RegularExpressions;

// Note: The duplicate 'using System.Diagnostics.CodeAnalysis;' was removed if present.

namespace ProtoGeneratorUtil
{
    public class Options
    {
        [Option('v', "viewModelFiles", Required = true, HelpText = "Paths to the C# ViewModel files to process (comma-separated).", Separator = ',')]
        public IEnumerable<string> ViewModelFiles { get; set; }

        [Option("attributeDefinitionSourceFile", Required = true, HelpText = "Path to the C# source file defining the GenerateGrpcRemoteAttribute.")]
        public string AttributeDefinitionSourceFile { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output path for the generated .proto file. Default: Protos/{ServiceName}.proto")]
        public string? OutputPath { get; set; }

        [Option('p', "protoNamespace", Required = false, HelpText = "The C# namespace for the generated proto types. Default: {ViewModelNamespace}.Protos")]
        public string? ProtoNamespace { get; set; }

        [Option('s', "serviceName", Required = false, HelpText = "The gRPC service name. Default: {ViewModelName}Service")]
        public string? GrpcServiceName { get; set; }

        [Option('a', "attributeFullName", Required = false, HelpText = "The full name of the GenerateGrpcRemoteAttribute (must match definition in attributeDefinitionSourceFile).")]
        public string GenerateGrpcRemoteAttributeFullName { get; set; } = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";

        [Option("observablePropertyAttribute", Required = false, HelpText = "Full name of the ObservableProperty attribute.")]
        public string ObservablePropertyAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";

        [Option("relayCommandAttribute", Required = false, HelpText = "Full name of the RelayCommand attribute.")]
        public string RelayCommandAttributeFullName { get; set; } = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        [Option("referencePaths", Required = false, HelpText = "Comma-separated paths to additional reference assemblies (DLLs). Use this for assemblies like CommunityToolkit.Mvvm.dll.", Separator = ',')]
        public IEnumerable<string> ReferencePaths { get; set; }


        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(ProtoGeneratorUtil.Options))]
        public Options()
        {
            ViewModelFiles = new List<string>();
            AttributeDefinitionSourceFile = string.Empty; // Will be validated for existence
            ReferencePaths = new List<string>();
        }
    }

    // Helper records
    internal record PropertyInfo { public string Name = ""; public string TypeString = ""; public required ITypeSymbol FullTypeSymbol; }
    internal record CommandInfo { public string MethodName = ""; public string CommandPropertyName = ""; public List<ParameterInfoForProto> Parameters = []; public bool IsAsync; }
    internal record ParameterInfoForProto { public string Name = ""; public string TypeString = ""; public required ITypeSymbol FullTypeSymbol; }


    class Program
    {
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
            Console.WriteLine($"  Attribute Definition Source File: {opts.AttributeDefinitionSourceFile}");
            Console.WriteLine($"  Explicit Reference DLLs: {(opts.ReferencePaths.Any() ? string.Join(", ", opts.ReferencePaths) : "None")}");
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

            // Load actual ViewModel files
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

            // Load the attribute definition source file (required)
            if (!string.IsNullOrEmpty(opts.AttributeDefinitionSourceFile) && File.Exists(opts.AttributeDefinitionSourceFile))
            {
                var attrFileContent = await File.ReadAllTextAsync(opts.AttributeDefinitionSourceFile);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(attrFileContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: opts.AttributeDefinitionSourceFile));
                Console.WriteLine($"ProtoGeneratorUtil: Added syntax tree for attribute definition: {opts.AttributeDefinitionSourceFile}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ProtoGeneratorUtil: Error: GenerateGrpcRemoteAttribute source file not found or not specified. Path: '{opts.AttributeDefinitionSourceFile}'. This file is required and must be passed via --attributeDefinitionSourceFile.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }


            var references = new List<MetadataReference>();
            bool tpaLoadedSuccessfully = false;

            string? trustedAssembliesPaths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(trustedAssembliesPaths))
            {
                var paths = trustedAssembliesPaths.Split(Path.PathSeparator);
                int loadedFromTpa = 0;
                foreach (var path in paths)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path) && !references.Any(r => r.Display?.Equals(path, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        try
                        {
                            references.Add(MetadataReference.CreateFromFile(path));
                            loadedFromTpa++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not load TPA reference '{path}': {ex.GetType().Name} - {ex.Message.Split('\n')[0]}");
                        }
                    }
                }
                if (loadedFromTpa > 0)
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Loaded {loadedFromTpa} distinct references from TRUSTED_PLATFORM_ASSEMBLIES.");
                    tpaLoadedSuccessfully = true;
                }
                else
                {
                    Console.WriteLine("ProtoGeneratorUtil: Warning: TRUSTED_PLATFORM_ASSEMBLIES was present but no valid references were loaded from it.");
                }
            }

            if (!tpaLoadedSuccessfully)
            {
                Console.WriteLine("ProtoGeneratorUtil: Warning: TRUSTED_PLATFORM_ASSEMBLIES not available or failed to load references. Falling back to manual reference loading for core assemblies.");
                TryAddAssemblyReferenceByObject(references, typeof(object));
                TryAddAssemblyReferenceByObject(references, typeof(System.ComponentModel.INotifyPropertyChanged));
                TryAddAssemblyReferenceByObject(references, typeof(System.Linq.Enumerable));
                TryAddAssemblyReferenceByObject(references, typeof(System.Collections.Generic.List<>));
                TryAddAssemblyReferenceByObject(references, typeof(System.Collections.ObjectModel.ObservableCollection<>));
                TryAddAssemblyReferenceByObject(references, typeof(System.Threading.Tasks.Task));
                TryAddAssemblyReferenceByObject(references, typeof(System.Uri));
                TryAddAssemblyReferenceByName(references, "System.Runtime");
                TryAddAssemblyReferenceByName(references, "netstandard");
            }

            if (opts.ReferencePaths.Any())
            {
                Console.WriteLine("ProtoGeneratorUtil: Attempting to load explicitly provided reference DLLs via --referencePaths...");
                foreach (var refPath in opts.ReferencePaths)
                {
                    if (File.Exists(refPath))
                    {
                        if (!references.Any(r => r.Display?.Equals(refPath, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            try
                            {
                                references.Add(MetadataReference.CreateFromFile(refPath));
                                Console.WriteLine($"  Added explicit reference: {refPath}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Warning: Could not load explicit reference '{refPath}': {ex.Message.Split('\n')[0]}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  Explicit reference for '{refPath}' already added.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Warning: Explicit reference path not found: {refPath}");
                    }
                }
            }

            if (!references.Any(r => r.Display?.Contains("CommunityToolkit.Mvvm.dll", StringComparison.OrdinalIgnoreCase) == true))
            {
                TryAddAssemblyReference(references, "CommunityToolkit.Mvvm.dll", opts.ViewModelFiles.FirstOrDefault(), isOptional: false);
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: CommunityToolkit.Mvvm.dll likely already added via explicit reference or TPA.");
            }

            Console.WriteLine($"ProtoGeneratorUtil: Total {references.Count} metadata references collected for compilation.");

            Compilation compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees, // Includes user ViewModels AND GenerateGrpcRemoteAttribute.cs
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
                }
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: Compilation completed with no relevant errors or warnings reported by Roslyn.");
            }

            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";

            // Iterate through syntax trees that are NOT the attribute definition file to find the main ViewModel
            foreach (var tree in syntaxTrees.Where(st => !st.FilePath.Equals(opts.AttributeDefinitionSourceFile, StringComparison.OrdinalIgnoreCase)))
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
                                (attrFqn == null && attrShortName == opts.GenerateGrpcRemoteAttributeFullName) ||
                                attrShortName == Path.GetFileNameWithoutExtension(opts.GenerateGrpcRemoteAttributeFullName)
                                )
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
                Console.WriteLine($"  2. The attribute '{opts.GenerateGrpcRemoteAttributeFullName}' is correctly applied to the ViewModel class.");
                Console.WriteLine($"  3. The '--attributeDefinitionSourceFile \"{opts.AttributeDefinitionSourceFile}\"' correctly points to the attribute's C# source and was parsed without errors.");
                Console.WriteLine($"  4. There are no compilation errors (see diagnostics above) preventing the attribute from being recognized.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

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
                    firstVmFileForPathDerivation = opts.ViewModelFiles.First(); // Fallback if symbol location doesn't match file list directly
                }


                var baseDir = Path.GetDirectoryName(firstVmFileForPathDerivation ?? opts.AttributeDefinitionSourceFile); // Fallback further if still null
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

            bool hasObservableFields = mainViewModelSymbol.GetMembersIncludingBaseTypes().OfType<IFieldSymbol>().Any(f =>
                f.GetAttributes().Any(a => (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.ObservablePropertyAttributeFullName ||
                                            a.AttributeClass?.Name == opts.ObservablePropertyAttributeFullName ||
                                            a.AttributeClass?.Name == Path.GetFileNameWithoutExtension(opts.ObservablePropertyAttributeFullName))));

            bool hasCommandMethods = mainViewModelSymbol.GetMembersIncludingBaseTypes().OfType<IMethodSymbol>().Any(m =>
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

        private static void TryAddAssemblyReferenceByObject(List<MetadataReference> references, Type typeInAssembly)
        {
            try
            {
                if (typeInAssembly?.Assembly?.Location != null)
                {
                    string location = typeInAssembly.Assembly.Location;
                    if (File.Exists(location) && !references.Any(r => r.Display?.Equals(location, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Console.WriteLine($"  Added core reference from Type '{typeInAssembly.FullName}': {location}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Warning: Could not get assembly location for core type '{typeInAssembly?.FullName ?? "Unknown"}'. Reference not added.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not load core reference for assembly of type '{typeInAssembly?.FullName ?? "Unknown"}': {ex.Message.Split('\n')[0]}");
            }
        }
        private static void TryAddAssemblyReferenceByName(List<MetadataReference> references, string assemblyName)
        {
            try
            {
                var assembly = Assembly.Load(assemblyName);
                if (assembly?.Location != null)
                {
                    string location = assembly.Location;
                    if (File.Exists(location) && !references.Any(r => r.Display?.Equals(location, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        references.Add(MetadataReference.CreateFromFile(location));
                        Console.WriteLine($"  Added core reference by Name '{assemblyName}': {location}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Warning: Could not get assembly location for core assembly loaded by name '{assemblyName}'. Reference not added.");
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"  Warning: Core assembly '{assemblyName}' not found by Assembly.Load. Reference not added.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not load core reference for assembly '{assemblyName}' by name: {ex.Message.Split('\n')[0]}");
            }
        }

        private static void TryAddAssemblyReference(List<MetadataReference> references, string dllName, string? hintFilePath, bool isOptional)
        {
            string? foundPath = null;
            var searchPathsAttempted = new List<string>();

            // 1. ProtoGeneratorUtil.exe's directory
            if (Directory.Exists(AppContext.BaseDirectory))
            {
                foundPath = Directory.GetFiles(AppContext.BaseDirectory, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (foundPath != null) searchPathsAttempted.Add($"ProtoUtil BaseDir: {foundPath} (FOUND)");
                else searchPathsAttempted.Add($"ProtoUtil BaseDir: {AppContext.BaseDirectory} (not found for {dllName})");
            }

            // 2. Relative to hintFilePath (e.g., a ViewModel file)
            if (foundPath == null && !string.IsNullOrEmpty(hintFilePath))
            {
                var hintDir = Path.GetDirectoryName(hintFilePath);
                if (string.IsNullOrEmpty(hintDir)) hintDir = Directory.GetCurrentDirectory();

                for (int i = 0; i < 4 && !string.IsNullOrEmpty(hintDir) && foundPath == null; i++)
                {
                    searchPathsAttempted.Add($"Searching near: {hintDir} for {dllName}");
                    string[] configurations = { "Debug", "Release" };
                    string[] tfms = { "net8.0", "net7.0", "net6.0", "net5.0", "netstandard2.1", "netstandard2.0", "" }; // Empty string for non-tfm specific paths
                    foreach (var config in configurations)
                    {
                        var configBinPath = Path.Combine(hintDir, "bin", config);
                        // Check directly in bin/config too before TFMs
                        if (Directory.Exists(configBinPath))
                        {
                            foundPath = Directory.GetFiles(configBinPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                            if (foundPath != null) { searchPathsAttempted.Add($"  In {configBinPath} (FOUND)"); break; }
                        }
                        foreach (var tfm in tfms)
                        {
                            var tfmPath = string.IsNullOrEmpty(tfm) ? configBinPath : Path.Combine(configBinPath, tfm);
                            if (Directory.Exists(tfmPath))
                            {
                                foundPath = Directory.GetFiles(tfmPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                                if (foundPath != null) { searchPathsAttempted.Add($"  In {tfmPath} (FOUND)"); break; }
                            }
                        }
                        if (foundPath != null) break;
                    }
                    if (foundPath == null && Directory.Exists(hintDir))
                    {
                        foundPath = Directory.GetFiles(hintDir, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (foundPath != null) searchPathsAttempted.Add($"  In {hintDir} directly (FOUND)");
                    }
                    if (foundPath != null) break;
                    hintDir = Path.GetDirectoryName(hintDir);
                }
            }

            // 3. NuGet global packages folder
            if (foundPath == null)
            {
                string nugetPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
                if (Directory.Exists(nugetPackagesPath))
                {
                    string packageNameNoExt = Path.GetFileNameWithoutExtension(dllName).ToLowerInvariant();
                    var packageDir = Path.Combine(nugetPackagesPath, packageNameNoExt);
                    searchPathsAttempted.Add($"NuGet Cache Search Base: {packageDir} for {dllName}");
                    if (Directory.Exists(packageDir))
                    {
                        foundPath = Directory.GetFiles(packageDir, dllName, SearchOption.AllDirectories)
                                           .Where(p => p.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar) || p.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar))
                                           .OrderByDescending(f => f.ToLowerInvariant().Contains("net8.0") ? 30 : f.ToLowerInvariant().Contains("net7.0") ? 20 : f.ToLowerInvariant().Contains("net6.0") ? 10 : f.ToLowerInvariant().Contains("netstandard2.0") ? 5 : 0)
                                           .ThenByDescending(f => f)
                                           .FirstOrDefault();
                        if (foundPath != null) searchPathsAttempted.Add($"  NuGet Cache: {foundPath} (FOUND)");
                    }
                }
            }

            // 4. .NET SDK Packs / Shared
            if (foundPath == null)
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? Path.Combine(programFiles, "dotnet");
                if (!Directory.Exists(dotnetRoot) && programFiles != programFilesX86) dotnetRoot = Path.Combine(programFilesX86, "dotnet");

                string[] rootsToSearch = {
                    Path.Combine(dotnetRoot, "packs"),
                    Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App"),
                    Path.Combine(dotnetRoot, "shared", "Microsoft.AspNetCore.App")
                };
                searchPathsAttempted.Add($"SDK Search Roots for {dllName}: {string.Join("; ", rootsToSearch)}");

                foreach (var root in rootsToSearch.Where(Directory.Exists))
                {
                    try
                    {
                        foundPath = Directory.GetFiles(root, dllName, SearchOption.AllDirectories)
                                           .OrderByDescending(f => f.ToLowerInvariant().Contains("net8.0") ? 30 : f.ToLowerInvariant().Contains("net7.0") ? 20 : f.ToLowerInvariant().Contains("net6.0") ? 10 : 0)
                                           .ThenByDescending(f => f)
                                           .FirstOrDefault();
                        if (foundPath != null) { searchPathsAttempted.Add($"  SDK Path: {foundPath} (FOUND)"); break; }
                    }
                    catch (Exception ex) { searchPathsAttempted.Add($"  Error searching SDK root '{root}' for {dllName}: {ex.Message.Split('\n')[0]}"); }
                }
            }

            if (foundPath != null && File.Exists(foundPath))
            {
                if (!references.Any(r => r.Display?.Equals(foundPath, StringComparison.OrdinalIgnoreCase) == true))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(foundPath));
                        Console.WriteLine($"ProtoGeneratorUtil: Added reference for '{dllName}': {foundPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not create MetadataReference for found file '{foundPath}': {ex.Message.Split('\n')[0]}");
                    }
                }
                else
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Info: Reference for '{dllName}' ({foundPath}) already added.");
                }
            }
            else
            {
                Console.WriteLine($"ProtoGeneratorUtil: {(isOptional ? "Info: Optional assembly" : "Warning: Could not find")} '{dllName}'. Attribute resolution and type analysis might be affected.");
                // Uncomment for detailed debugging of search paths:
                // Console.WriteLine($"  Search paths attempted for {dllName}:");
                // foreach(var sPath in searchPathsAttempted) { Console.WriteLine($"    {sPath}"); }
            }
        }

        private static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName, Compilation compilation)
        {
            var props = new List<PropertyInfo>();
            string shortObservableName = Path.GetFileNameWithoutExtension(observablePropertyAttributeFullName);
            Console.WriteLine($"ProtoGeneratorUtil: Scanning for ObservableProperties in {classSymbol.Name} (and base types) using attribute '{observablePropertyAttributeFullName}' (or short name '{shortObservableName}')...");

            foreach (var member in classSymbol.GetMembersIncludingBaseTypes())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    foreach (var attr in fieldSymbol.GetAttributes())
                    {
                        string? attrClassFQN = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                        string? attrClassName = attr.AttributeClass?.Name;

                        if (attrClassFQN == observablePropertyAttributeFullName ||
                            attrClassName == observablePropertyAttributeFullName ||
                            (attrClassName != null && attrClassName == shortObservableName))
                        {
                            Console.WriteLine($"    -> MATCHED ObservablePropertyAttribute for field '{fieldSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' (Type: {fieldSymbol.Type.Name})!");
                            string propertyName = fieldSymbol.Name.TrimStart('_');
                            if (propertyName.Length > 0 && char.IsLower(propertyName[0]))
                            {
                                propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                            }
                            else if (propertyName.Length == 0 || !char.IsLetter(propertyName[0]))
                            {
                                Console.WriteLine($"      Skipping field '{fieldSymbol.Name}' due to invalid derived property name ('{propertyName}').");
                                continue;
                            }
                            if (props.Any(p => p.Name.Equals(propertyName, StringComparison.Ordinal)))
                            {
                                Console.WriteLine($"      Warning: Property name '{propertyName}' derived from field '{fieldSymbol.Name}' conflicts with an existing property. Skipping.");
                                continue;
                            }
                            props.Add(new PropertyInfo { Name = propertyName, TypeString = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FullTypeSymbol = fieldSymbol.Type });
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"ProtoGeneratorUtil: Extracted {props.Count} observable properties from '{classSymbol.Name}'.");
            return props;
        }

        private static List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol, string relayCommandAttributeFullName, Compilation compilation)
        {
            var cmds = new List<CommandInfo>();
            string shortRelayName = Path.GetFileNameWithoutExtension(relayCommandAttributeFullName);
            Console.WriteLine($"ProtoGeneratorUtil: Scanning for RelayCommands in {classSymbol.Name} (and base types) using attribute '{relayCommandAttributeFullName}' (or short name '{shortRelayName}')...");

            foreach (var member in classSymbol.GetMembersIncludingBaseTypes())
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary)
                {
                    foreach (var attr in methodSymbol.GetAttributes())
                    {
                        string? attrClassFQN = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                        string? attrClassName = attr.AttributeClass?.Name;

                        if (attrClassFQN == relayCommandAttributeFullName ||
                            attrClassName == relayCommandAttributeFullName ||
                            (attrClassName != null && attrClassName == shortRelayName))
                        {
                            Console.WriteLine($"    -> MATCHED RelayCommandAttribute for method '{methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'!");
                            string commandPropertyName = methodSymbol.Name;
                            if (commandPropertyName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                            {
                                commandPropertyName = commandPropertyName.Substring(0, commandPropertyName.Length - 5);
                            }
                            if (commandPropertyName.Length > 0 && char.IsLower(commandPropertyName[0]))
                            {
                                commandPropertyName = char.ToUpperInvariant(commandPropertyName[0]) + commandPropertyName.Substring(1);
                            }
                            else if (commandPropertyName.Length == 0)
                            {
                                Console.WriteLine($"      Skipping method '{methodSymbol.Name}' due to invalid derived command property name.");
                                continue;
                            }
                            commandPropertyName += "Command";

                            if (cmds.Any(c => c.CommandPropertyName.Equals(commandPropertyName, StringComparison.Ordinal)))
                            {
                                Console.WriteLine($"      Warning: Command property name '{commandPropertyName}' derived from method '{methodSymbol.Name}' conflicts with an existing command. Skipping.");
                                continue;
                            }

                            cmds.Add(new CommandInfo
                            {
                                MethodName = methodSymbol.Name,
                                CommandPropertyName = commandPropertyName,
                                Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoForProto { Name = p.Name, TypeString = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FullTypeSymbol = p.Type }).ToList(),
                                IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.ToDisplayString() == "System.Threading.Tasks.Task" || (rtSym.IsGenericType && rtSym.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")))
                            });
                            break;
                        }
                    }
                }
            }
            Console.WriteLine($"ProtoGeneratorUtil: Extracted {cmds.Count} relay commands from '{classSymbol.Name}'.");
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
                case SpecialType.System_Byte: return "uint32"; // For single byte properties; byte[] is "bytes"
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
                case "System.Windows.Threading.DispatcherTimer": // From original user code
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
                string requestType = cmd.Parameters.Any() ? $"{cmd.MethodName}Request" : "google.protobuf.Empty";
                // If a command has no parameters, its actual request message will be empty.
                // For consistency in generation, we use the generated {CmdName}Request which might be empty.
                // Alternatively, if the request message is truly empty, gRPC tooling often optimizes to google.protobuf.Empty.
                // We will use the generated request message type for now.
                bodySb.AppendLine($"  rpc {cmd.MethodName} ({cmd.MethodName}Request) returns ({cmd.MethodName}Response);");
            }
            bodySb.AppendLine("}");
            bodySb.AppendLine();

            var finalProtoSb = new StringBuilder();
            finalProtoSb.AppendLine("syntax = \"proto3\";");
            finalProtoSb.AppendLine();
            string protoPackageName = protoNamespaceOption.ToLowerInvariant();
            finalProtoSb.AppendLine($"package {protoPackageName};");
            finalProtoSb.AppendLine();
            finalProtoSb.AppendLine($"option csharp_namespace = \"{protoNamespaceOption}\";");
            finalProtoSb.AppendLine();

            if (bodySb.ToString().Contains("google.protobuf.Any")) requiredImports.Add("google/protobuf/any.proto");
            if (bodySb.ToString().Contains("google.protobuf.Timestamp")) requiredImports.Add("google/protobuf/timestamp.proto");
            if (bodySb.ToString().Contains("google.protobuf.Duration")) requiredImports.Add("google/protobuf/duration.proto");

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