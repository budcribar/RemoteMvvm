using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

// Assuming Helpers class is in this namespace or accessible
// e.g., if Helpers.cs is in the same project and namespace:
// No explicit using static needed if it's just Helpers.GetAllMembers()

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

        [Option("referencePaths", Required = false, HelpText = "Semicolon-separated paths to additional reference assemblies (DLLs).")]
        public string ReferencePathsRaw { get; set; }

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

    // Assuming Helpers class is defined in Helpers.cs in the same namespace or is otherwise accessible.
    // Example structure (ensure your actual Helpers.cs matches this or is compatible):
    /*
    namespace ProtoGeneratorUtil // Or your actual Helpers namespace
    {
        public static class Helpers
        {
            public static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol? typeSymbol)
            {
                var current = typeSymbol;
                var yieldedMemberSignatures = new HashSet<string>(); // To avoid duplicate members

                while (current != null && current.SpecialType != SpecialType.System_Object)
                {
                    foreach (var member in current.GetMembers())
                    {
                        // Create a unique signature string for members
                        // For methods, this might include parameter types to differentiate overloads
                        // For fields/properties, name and kind might be enough
                        string memberSignature = member.Kind.ToString() + "_" + member.Name;
                        if (member is IMethodSymbol ms) {
                            memberSignature += "(" + string.Join(",", ms.Parameters.Select(p => p.Type.ToDisplayString())) + ")";
                        }

                        if (yieldedMemberSignatures.Add(memberSignature))
                        {
                            yield return member;
                        }
                    }
                    current = current.BaseType;
                }
            }
        }
    }
    */


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
                // AppContext.BaseDirectory is more reliable for single-file applications than Assembly.Location
                // Assembly.Location might return an empty string for embedded assemblies.
                // AppContext.BaseDirectory is the base directory of the application domain,
                // which for single-file executables is typically the directory containing the executable.
                var coreLibPath = AppContext.BaseDirectory;
                // We don't need the specific System.Object assembly location itself for later reference loading.
                // The important part is that AppContext.BaseDirectory gives us a root to find other assemblies.
                // The original code was likely trying to find the BCL assemblies, which are now handled by the fallback logic.
                Console.WriteLine($"ProtoGeneratorUtil: Core assembly (System.Object) location: '{coreLibPath ?? "N/A"}' (File Exists: {(!string.IsNullOrEmpty(coreLibPath) && File.Exists(coreLibPath))})");
            }
            catch (Exception ex) // Should be rare for AppContext.BaseDirectory
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
            Console.WriteLine($"  Raw --referencePaths string: '{opts.ReferencePathsRaw ?? "null"}'");
            var explicitRefs = opts.GetReferencePaths().ToList();
            Console.WriteLine($"  Parsed Explicit Reference DLLs ({explicitRefs.Count}): {(explicitRefs.Any() ? string.Join("; ", explicitRefs.Take(5)) + (explicitRefs.Count > 5 ? "..." : "") : "None")}");
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
                        Console.WriteLine("  Ensure the .cs file is marked as EmbeddedResource in ProtoGeneratorUtil.csproj, the <Link> is correct, and the name matches.");
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

            Action<string, string> addRef = (path, source) => // Simplified addRef
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!File.Exists(path))
                {
                    // Console.WriteLine($"ProtoGeneratorUtil: Info: Reference path from {source} does not exist or is invalid: '{path}'"); // Can be noisy
                    return;
                }
                if (loadedReferencePaths.Add(path))
                { // Add returns true if the path was new
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not load reference '{path}' (from {source}): {ex.GetType().Name} - {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
                    }
                }
            };

            var skipAssemblies = new[]
            {
                "Grpc.AspNetCore.dll",
                "Grpc.AspNetCore.Web.dll",
                "Grpc.AspNetCore.Server.dll",
                "Grpc.AspNetCore.Server.ClientFactory.dll",
                "Grpc.Net.Client.dll",
                "Grpc.Net.ClientFactory.dll",
                "Grpc.Net.Common.dll",
                "Grpc.Core.dll",
                "Grpc.Core.Api.dll",
                "Microsoft.Extensions.Hosting.dll"
            };

            var msbuildProvidedRefs = opts.GetReferencePaths()
                .Where(p => !skipAssemblies.Any(skip => p.EndsWith(skip, StringComparison.OrdinalIgnoreCase)))
                .ToList();

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
                Console.WriteLine("ProtoGeneratorUtil: CRITICAL WARNING: No explicit --referencePaths provided by MSBuild. This will likely cause many compilation errors.");
            }

            // Fallback for essential assemblies if still missing (less reliable if MSBuild paths are incomplete)
            bool coreSystemRuntimeLoaded = loadedReferencePaths.Any(p => Path.GetFileName(p).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase));
            bool coreSystemPrivateCoreLibLoaded = loadedReferencePaths.Any(p => Path.GetFileName(p).Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase));

            if (!coreSystemRuntimeLoaded && !coreSystemPrivateCoreLibLoaded)
            {
                Console.WriteLine("ProtoGeneratorUtil: Core runtime (System.Runtime.dll or System.Private.CoreLib.dll) not found in MSBuild paths. Attempting fallback load...");
                // Use AppContext.BaseDirectory for the base directory of the application
                string? coreDir = AppContext.BaseDirectory;
                // If AppContext.BaseDirectory is null or empty (should not happen in a running process, but defensive check)
                if (!string.IsNullOrEmpty(coreDir))
                {
                    addRef(Path.Combine(coreDir, "System.Private.CoreLib.dll"), "Fallback Core"); // Most important for .NET Core+
                    addRef(Path.Combine(coreDir, "System.Runtime.dll"), "Fallback Core");
                    // Also add netstandard.dll which might be needed
                    addRef(Path.Combine(coreDir, "netstandard.dll"), "Fallback Core");
                }
            }

            if (!loadedReferencePaths.Any(p => Path.GetFileName(p).Equals("CommunityToolkit.Mvvm.dll", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("ProtoGeneratorUtil: CommunityToolkit.Mvvm.dll not found in MSBuild-provided references. Attempting heuristic load...");
                TryAddAssemblyReferenceHeuristic(references, "CommunityToolkit.Mvvm.dll", opts.ViewModelFiles.FirstOrDefault(), isOptional: false, loadedReferencePaths);
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: CommunityToolkit.Mvvm.dll was included in MSBuild-provided references.");
            }

            Console.WriteLine($"ProtoGeneratorUtil: Total {references.Count} metadata references collected for compilation.");
            coreSystemRuntimeLoaded = references.Any(r => r.Display != null && Path.GetFileName(r.Display).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase));
            coreSystemPrivateCoreLibLoaded = references.Any(r => r.Display != null && Path.GetFileName(r.Display).Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase));

            if (!coreSystemRuntimeLoaded && !coreSystemPrivateCoreLibLoaded)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ProtoGeneratorUtil: CRITICAL ERROR: A core runtime library (System.Private.CoreLib.dll or System.Runtime.dll) could not be loaded as a MetadataReference. Cannot proceed.");
                Console.ResetColor();
                Environment.ExitCode = 1; return;
            }
            if (references.Count < 50 && msbuildProvidedRefs.Any())
            { // Heuristic check
                Console.WriteLine("ProtoGeneratorUtil: Warning: Significantly fewer references loaded than provided by MSBuild. This might indicate issues with path processing or file existence for the provided reference paths.");
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
            bool hasReportableErrors = false;
            List<Tuple<Diagnostic, DiagnosticSeverity>> filteredDiagnosticsToDisplay = new List<Tuple<Diagnostic, DiagnosticSeverity>>();
            HashSet<string> knownGeneratedPropertyNames = new HashSet<string>();
            HashSet<string> knownGeneratedCommandNames = new HashSet<string>();

            foreach (var tree in syntaxTrees.Where(st => st.FilePath != AttributeDefinitionPlaceholderPath))
            {
                var tempSemanticModel = compilation.GetSemanticModel(tree);
                if (tempSemanticModel == null) continue;
                var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classSyntax in classDeclarations)
                {
                    if (tempSemanticModel.GetDeclaredSymbol(classSyntax) is INamedTypeSymbol classSymbol)
                    {
                        foreach (var member in Helpers.GetAllMembers(classSymbol)) // Using your Helpers class
                        {
                            if (member is IFieldSymbol field && field.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.ObservablePropertyAttributeFullName))
                            {
                                string propName = field.Name.TrimStart('_');
                                if (propName.Length > 0 && char.IsLower(propName[0])) propName = char.ToUpperInvariant(propName[0]) + propName.Substring(1);
                                if (!string.IsNullOrEmpty(propName) && char.IsLetter(propName[0])) knownGeneratedPropertyNames.Add(propName);
                            }
                            else if (member is IMethodSymbol method && method.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.RelayCommandAttributeFullName))
                            {
                                string cmdName = method.Name;
                                if (cmdName.EndsWith("Async", StringComparison.OrdinalIgnoreCase)) cmdName = cmdName.Substring(0, cmdName.Length - 5);
                                if (cmdName.Length > 0 && char.IsLower(cmdName[0])) cmdName = char.ToUpperInvariant(cmdName[0]) + cmdName.Substring(1);
                                if (!string.IsNullOrEmpty(cmdName) && char.IsLetter(cmdName[0])) knownGeneratedCommandNames.Add(cmdName + "Command");
                            }
                        }
                    }
                }
            }

            foreach (var diag in allDiagnostics)
            {
                DiagnosticSeverity displaySeverity = diag.Severity;
                if (diag.Id == "CS0103" && diag.Location.SourceTree != null && diag.Location.SourceTree.FilePath != AttributeDefinitionPlaceholderPath)
                {
                    var match = Regex.Match(diag.GetMessage(), @"The name '([^']*)' does not exist");
                    if (match.Success)
                    {
                        string missingName = match.Groups[1].Value;
                        if (knownGeneratedPropertyNames.Contains(missingName) || knownGeneratedCommandNames.Contains(missingName))
                        {
                            displaySeverity = DiagnosticSeverity.Info;
                        }
                    }
                }

                if (displaySeverity == DiagnosticSeverity.Error)
                {
                    filteredDiagnosticsToDisplay.Add(Tuple.Create(diag, displaySeverity));
                    hasReportableErrors = true;
                }
                else if (displaySeverity == DiagnosticSeverity.Warning && diag.Id != "CS0169" && diag.Id != "CS0414" && diag.Id != "CS8019")
                {
                    filteredDiagnosticsToDisplay.Add(Tuple.Create(diag, displaySeverity));
                }
                else if (displaySeverity == DiagnosticSeverity.Info)
                {
                    filteredDiagnosticsToDisplay.Add(Tuple.Create(diag, displaySeverity));
                }
            }

            if (filteredDiagnosticsToDisplay.Any())
            {
                Console.WriteLine("--- ProtoGeneratorUtil: Compilation Diagnostics ---");
                foreach (var diagItem in filteredDiagnosticsToDisplay.OrderByDescending(d => d.Item1.Severity).ThenByDescending(d => d.Item2).ThenBy(d => d.Item1.Location.SourceTree?.FilePath ?? string.Empty).ThenBy(d => d.Item1.Location.SourceSpan.Start))
                {
                    var originalDiagnostic = diagItem.Item1; var currentDisplaySeverity = diagItem.Item2;
                    ConsoleColor originalColor = Console.ForegroundColor;
                    var lineSpan = originalDiagnostic.Location.GetLineSpan();
                    string locationString = lineSpan.IsValid ? $"{Path.GetFileName(lineSpan.Path)}({lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1})" : "(No location)";
                    Console.ForegroundColor = currentDisplaySeverity switch { DiagnosticSeverity.Error => ConsoleColor.Red, DiagnosticSeverity.Warning => ConsoleColor.Yellow, DiagnosticSeverity.Info => ConsoleColor.Cyan, _ => originalColor };
                    string severityText = originalDiagnostic.Severity.ToString();
                    if (originalDiagnostic.Severity == DiagnosticSeverity.Error && currentDisplaySeverity == DiagnosticSeverity.Info) 
                        severityText = $"Error (demoted to Info by tool due to expected source gen timing)";
                    else 
                        Console.WriteLine($"{originalDiagnostic.Id} ({severityText}): {originalDiagnostic.GetMessage()} {locationString}");
                    Console.ForegroundColor = originalColor;
                }
                Console.WriteLine("---------------------------------------------------");
                if (hasReportableErrors)
                {
                    Console.WriteLine("ProtoGeneratorUtil: Compilation has reportable errors. Proto generation might be incomplete or incorrect.");
                }
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("ProtoGeneratorUtil: Informational: Some 'CS0103: The name '...' does not exist' messages (if shown as Info severity)");
                Console.WriteLine("  may be due to analyzing ViewModel source before other source generators (like CommunityToolkit.Mvvm) have run.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("ProtoGeneratorUtil: Compilation completed with no relevant errors or warnings reported by Roslyn.");
            }

            if (hasReportableErrors && Environment.ExitCode == 0)
            {
                Console.WriteLine("ProtoGeneratorUtil: Exiting due to reportable compilation errors.");
                Environment.ExitCode = 1;
            }

            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";
            // (Find mainViewModelSymbol logic - remains the same)
            foreach (var tree in syntaxTrees.Where(st => st.FilePath != AttributeDefinitionPlaceholderPath))
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                if (semanticModel == null) continue;
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
                                mainViewModelSymbol = classSymbol; originalVmName = classSymbol.Name;
                                Console.WriteLine($"ProtoGeneratorUtil: Found target ViewModel for .proto generation: {originalVmName} (Attribute matched: '{attr.AttributeClass?.ToDisplayString() ?? opts.GenerateGrpcRemoteAttributeFullName}')");
                                foundGenerateAttribute = true; break;
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
                Console.ResetColor(); Environment.ExitCode = 1; return;
            }

            // (Derive OutputPath, ProtoNamespace, GrpcServiceName - same as before)
            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace)) { opts.ProtoNamespace = $"{mainViewModelSymbol.ContainingNamespace.ToDisplayString()}.Protos"; Console.WriteLine($"ProtoGeneratorUtil: Derived Proto Namespace: {opts.ProtoNamespace}"); }
            if (string.IsNullOrWhiteSpace(opts.GrpcServiceName)) { opts.GrpcServiceName = $"{originalVmName}Service"; Console.WriteLine($"ProtoGeneratorUtil: Derived gRPC Service Name: {opts.GrpcServiceName}"); }
            if (string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                var firstVmFileForPathDerivation = opts.ViewModelFiles.FirstOrDefault(f => mainViewModelSymbol.Locations.Any(loc => loc.SourceTree?.FilePath.Equals(f, StringComparison.OrdinalIgnoreCase) == true));
                if (firstVmFileForPathDerivation == null && opts.ViewModelFiles.Any()) firstVmFileForPathDerivation = opts.ViewModelFiles.First();
                var baseDir = Path.GetDirectoryName(firstVmFileForPathDerivation ?? "."); if (string.IsNullOrEmpty(baseDir)) baseDir = ".";
                var protosDir = Path.Combine(baseDir, "Protos"); opts.OutputPath = Path.Combine(protosDir, $"{opts.GrpcServiceName}.proto");
                Console.WriteLine($"ProtoGeneratorUtil: Derived Output Path: {opts.OutputPath}");
            }
            if (string.IsNullOrWhiteSpace(opts.ProtoNamespace) || string.IsNullOrWhiteSpace(opts.GrpcServiceName) || string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("ProtoGeneratorUtil: Error: Could not determine essential options..."); Console.ResetColor(); Environment.ExitCode = 1; return;
            }
            Console.WriteLine($"--- ProtoGeneratorUtil: Final effective options ---");
            Console.WriteLine($"  Output .proto: {opts.OutputPath}"); Console.WriteLine($"  Proto Namespace: {opts.ProtoNamespace}"); Console.WriteLine($"  gRPC Service Name: {opts.GrpcServiceName}"); Console.WriteLine($"-------------------------------------------------");

            List<PropertyInfo> properties = GetObservableProperties(mainViewModelSymbol, opts.ObservablePropertyAttributeFullName, compilation);
            List<CommandInfo> commands = GetRelayCommands(mainViewModelSymbol, opts.RelayCommandAttributeFullName, compilation);

            // (Warning logic for properties/commands not found if fields/methods with attributes exist - same as before)
            bool hasObservableFields = Helpers.GetAllMembers(mainViewModelSymbol).OfType<IFieldSymbol>().Any(f => f.GetAttributes().Any(a => (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.ObservablePropertyAttributeFullName || a.AttributeClass?.Name == opts.ObservablePropertyAttributeFullName || a.AttributeClass?.Name == Path.GetFileNameWithoutExtension(opts.ObservablePropertyAttributeFullName))));
            bool hasCommandMethods = Helpers.GetAllMembers(mainViewModelSymbol).OfType<IMethodSymbol>().Any(m => m.GetAttributes().Any(a => (a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == opts.RelayCommandAttributeFullName || a.AttributeClass?.Name == opts.RelayCommandAttributeFullName || a.AttributeClass?.Name == Path.GetFileNameWithoutExtension(opts.RelayCommandAttributeFullName))));
            if (!properties.Any() && hasObservableFields) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"ProtoGeneratorUtil: Warning: Fields with '{opts.ObservablePropertyAttributeFullName}' attribute found, but no properties extracted. Check diagnostics and attribute names."); Console.ResetColor(); }
            if (!commands.Any() && hasCommandMethods) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"ProtoGeneratorUtil: Warning: Methods with '{opts.RelayCommandAttributeFullName}' attribute found, but no commands extracted. Check diagnostics and attribute names."); Console.ResetColor(); }

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

                bool writeFile = true;
                if (File.Exists(opts.OutputPath))
                {
                    string existingContent = await File.ReadAllTextAsync(opts.OutputPath, Encoding.UTF8);
                    if (NormalizeLineEndings(existingContent) == NormalizeLineEndings(protoFileContent))
                    {
                        writeFile = false;
                        Console.WriteLine($"ProtoGeneratorUtil: Generated .proto content is identical to existing file. Skipping write for: {opts.OutputPath}");
                    }
                }

                if (writeFile)
                {
                    await File.WriteAllTextAsync(opts.OutputPath, protoFileContent, Encoding.UTF8);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"ProtoGeneratorUtil: Successfully generated/updated .proto file at: {opts.OutputPath}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ProtoGeneratorUtil: Error writing .proto file: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static void TryAddAssemblyReferenceHeuristic(List<MetadataReference> references, string dllName, string? hintFilePath, bool isOptional, HashSet<string> loadedReferencePaths)
        {
            string? foundPath = null;
            // 1. AppContext.BaseDirectory
            if (Directory.Exists(AppContext.BaseDirectory))
            {
                foundPath = Directory.GetFiles(AppContext.BaseDirectory, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            }

            // 2. Relative to hintFilePath (ViewModel file's directory and its parent directories)
            if (foundPath == null && !string.IsNullOrEmpty(hintFilePath))
            {
                var hintDir = Path.GetDirectoryName(hintFilePath);
                if (string.IsNullOrEmpty(hintDir)) hintDir = Directory.GetCurrentDirectory();

                for (int i = 0; i < 4 && !string.IsNullOrEmpty(hintDir) && foundPath == null; i++) // Search up to 4 levels
                {
                    // Check direct subfolders common in builds: bin/Debug|Release/tfm
                    string[] configurations = { "Debug", "Release" };
                    string[] tfms = { "net8.0", "net7.0", "net6.0", "net5.0", "netstandard2.1", "netstandard2.0", "" }; // "" for non-tfm paths

                    foreach (var config in configurations)
                    {
                        var configBinPath = Path.Combine(hintDir, "bin", config);
                        if (Directory.Exists(configBinPath))
                        {
                            foundPath = Directory.GetFiles(configBinPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                            if (foundPath != null) break;

                            foreach (var tfm in tfms)
                            {
                                var tfmPath = string.IsNullOrEmpty(tfm) ? configBinPath : Path.Combine(configBinPath, tfm);
                                if (Directory.Exists(tfmPath))
                                {
                                    foundPath = Directory.GetFiles(tfmPath, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                                    if (foundPath != null) break;
                                }
                            }
                        }
                        if (foundPath != null) break;
                    }
                    if (foundPath == null && Directory.Exists(hintDir)) // Check current hintDir directly
                    {
                        foundPath = Directory.GetFiles(hintDir, dllName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    }
                    if (foundPath != null) break;
                    hintDir = Path.GetDirectoryName(hintDir); // Go up one level
                }
            }
            // (The rest of your TryAddAssemblyReferenceHeuristic logic for NuGet cache, SDK packs, etc.)
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
                if (loadedReferencePaths.Add(foundPath))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(foundPath));
                        Console.WriteLine($"ProtoGeneratorUtil: Added reference for '{dllName}': {foundPath} (via heuristic search)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not create MetadataReference for heuristically found file '{foundPath}': {ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
                    }
                }
            }
            else
            {
                if (!isOptional)
                {
                    Console.WriteLine($"ProtoGeneratorUtil: Warning: Could not find required assembly '{dllName}' via heuristic search. Analysis might be incomplete.");
                }
            }
        }


        private static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName, Compilation compilation)
        {
            var props = new List<PropertyInfo>();
            var processedPropertyNames = new HashSet<string>(StringComparer.Ordinal);

            INamedTypeSymbol? expectedAttributeSymbol = compilation.GetTypeByMetadataName(observablePropertyAttributeFullName);
            // Fallback for attribute name if symbol not found (e.g., if CommunityToolkit.Mvvm is not perfectly referenced in this compilation)
            string shortExpectedAttrName = Path.GetFileNameWithoutExtension(observablePropertyAttributeFullName); // "ObservableProperty" from "....ObservablePropertyAttribute"


            Console.WriteLine($"ProtoGeneratorUtil: Scanning for ObservableProperties in {classSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} (Expected FQN: {observablePropertyAttributeFullName}, Resolved Symbol: {expectedAttributeSymbol?.ToDisplayString() ?? "NOT RESOLVED"})");

            foreach (var member in Helpers.GetAllMembers(classSymbol)) // Using your Helpers class
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
                            // Preferred: Direct symbol comparison (OriginalDefinition handles generic attributes if any)
                            if (SymbolEqualityComparer.Default.Equals(appliedAttributeClass.OriginalDefinition, expectedAttributeSymbol.OriginalDefinition))
                            {
                                isMatch = true;
                            }
                        }

                        // Fallback: String comparison if symbol lookup failed or for flexibility
                        if (!isMatch)
                        {
                            string appliedAttrFQN = appliedAttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                            if (appliedAttrFQN == observablePropertyAttributeFullName)
                            {
                                isMatch = true;
                            }
                            else if (appliedAttributeClass.Name == observablePropertyAttributeFullName || appliedAttributeClass.Name == shortExpectedAttrName) // Check short name e.g. "ObservablePropertyAttribute" or "ObservableProperty"
                            {
                                // Console.WriteLine($"    -> Matched ObservableProperty by short name '{appliedAttributeClass.Name}' for field '{fieldSymbol.Name}'.");
                                isMatch = true;
                            }
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
                                // Console.WriteLine($"      Skipping field '{fieldSymbol.Name}' due to invalid derived property name ('{propertyName}').");
                                continue;
                            }

                            if (processedPropertyNames.Add(propertyName))
                            {
                                Console.WriteLine($"    -> MATCHED ObservableProperty for field '{fieldSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', generating property '{propertyName}' (Type: {fieldSymbol.Type.ToDisplayString()}).");
                                props.Add(new PropertyInfo { Name = propertyName, TypeString = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FullTypeSymbol = fieldSymbol.Type });
                            }
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
            var processedCommandPropertyNames = new HashSet<string>(StringComparer.Ordinal);

            INamedTypeSymbol? expectedAttributeSymbol = compilation.GetTypeByMetadataName(relayCommandAttributeFullName);
            string shortExpectedAttrName = Path.GetFileNameWithoutExtension(relayCommandAttributeFullName);

            Console.WriteLine($"ProtoGeneratorUtil: Scanning for RelayCommands in {classSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} (Expected FQN: {relayCommandAttributeFullName}, Resolved Symbol: {expectedAttributeSymbol?.ToDisplayString() ?? "NOT RESOLVED"})");

            foreach (var member in Helpers.GetAllMembers(classSymbol)) // Using your Helpers class
            {
                // RelayCommand can be on instance methods. Static methods are less common for typical viewmodel commands.
                if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary && !methodSymbol.IsStatic) // && !methodSymbol.IsOverride removed as GetAllMembers should handle hierarchy
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
                            if (appliedAttrFQN == relayCommandAttributeFullName)
                            {
                                isMatch = true;
                            }
                            else if (appliedAttributeClass.Name == relayCommandAttributeFullName || appliedAttributeClass.Name == shortExpectedAttrName)
                            {
                                // Console.WriteLine($"    -> Matched RelayCommand by short name '{appliedAttributeClass.Name}' for method '{methodSymbol.Name}'.");
                                isMatch = true;
                            }
                        }

                        if (isMatch)
                        {
                            string commandMethodName = methodSymbol.Name;
                            string commandPropertyNameBase = commandMethodName;
                            if (commandMethodName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                            {
                                commandPropertyNameBase = commandMethodName.Substring(0, commandMethodName.Length - 5);
                            }

                            if (string.IsNullOrEmpty(commandPropertyNameBase)) continue; // Should not happen if methodSymbol.Name is valid

                            string commandPropertyName = (commandPropertyNameBase.Length > 0 && char.IsLower(commandPropertyNameBase[0]))
                                ? char.ToUpperInvariant(commandPropertyNameBase[0]) + commandPropertyNameBase.Substring(1)
                                : commandPropertyNameBase;

                            if (string.IsNullOrEmpty(commandPropertyName) || !char.IsLetter(commandPropertyName[0]))
                            {
                                continue;
                            }
                            commandPropertyName += "Command";


                            if (processedCommandPropertyNames.Add(commandPropertyName))
                            {
                                Console.WriteLine($"    -> MATCHED RelayCommand for method '{methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', generating command property '{commandPropertyName}'.");
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
                case "System.TimeSpan": requiredImports.Add("google/protobuf/duration.proto"); return "google.protobuf.Duration";
                case "System.DateTimeOffset": requiredImports.Add("google/protobuf/timestamp.proto"); return "google.protobuf.Timestamp";
                case "System.Guid": return "string";
                case "System.Uri": return "string";
                case "System.Version": return "string";
                case "System.Numerics.BigInteger": return "string";
                case "System.Windows.Threading.DispatcherTimer": // Example from previous state
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
                string[] listLikeInterfaces = {
                    "System.Collections.Generic.List<T>", "System.Collections.Generic.IList<T>",
                    "System.Collections.Generic.ICollection<T>", "System.Collections.Generic.IEnumerable<T>",
                    "System.Collections.Generic.IReadOnlyList<T>", "System.Collections.Generic.IReadOnlyCollection<T>",
                    "System.Collections.ObjectModel.ObservableCollection<T>", "System.Collections.ObjectModel.ReadOnlyObservableCollection<T>"
                };
                if (listLikeInterfaces.Contains(originalDefFqn))
                {
                    if (namedType.TypeArguments.Length > 0)
                    {
                        return $"repeated {GetProtoFieldType(namedType.TypeArguments[0], compilation, requiredImports)}";
                    }
                }

                string[] dictionaryLikeInterfaces = {
                    "System.Collections.Generic.Dictionary<TKey, TValue>", "System.Collections.Generic.IDictionary<TKey, TValue>",
                    "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                };
                if (dictionaryLikeInterfaces.Contains(originalDefFqn))
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
            // Make package name valid (proto3 package names are usually all lowercase with underscores)
            string protoPackageName = Regex.Replace(protoNamespaceOption.ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(protoPackageName) || !char.IsLetter(protoPackageName[0]))
            {
                protoPackageName = "generated_" + protoPackageName; // Ensure it starts with a letter
            }
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