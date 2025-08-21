using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcRemoteMvvmModelUtil
{
    public class ViewModelAnalyzer
    {
        private static readonly SymbolDisplayFormat FullNameFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                                      SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        public static async Task<(INamedTypeSymbol? ViewModelSymbol, string ViewModelName, List<PropertyInfo> Properties, List<CommandInfo> Commands, Compilation Compilation)> AnalyzeAsync(
            IEnumerable<string> viewModelFiles,
            string observablePropertyAttributeFullName,
            string relayCommandAttributeFullName,
            IEnumerable<string> referencePaths,
         
            string observableObjectFullName = "CommunityToolkit.Mvvm.ComponentModel.ObservableObject" )
        {
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in viewModelFiles)
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"ViewModel file not found: {filePath}");
                }
                var fileContent = await File.ReadAllTextAsync(filePath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: filePath));
            }
           
            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
            foreach (var refPath in referencePaths)
            {
                if (File.Exists(refPath))
                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(refPath));
            }
            var mvvmAssemblyPath = typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject).Assembly.Location;
            if (File.Exists(mvvmAssemblyPath) && !references.Any(r => string.Equals(r.Display, mvvmAssemblyPath, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(mvvmAssemblyPath));
            }
            var compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            var missingTypeDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id == "CS0246")
                .ToArray();
            if (missingTypeDiagnostics.Length > 0)
            {
                var missingNames = missingTypeDiagnostics
                    .Select(d =>
                    {
                        var msg = d.GetMessage();
                        var start = msg.IndexOf('\'');
                        var end = msg.IndexOf('\'', start + 1);
                        return start >= 0 && end > start ? msg.Substring(start + 1, end - start - 1) : msg;
                    })
                    .Distinct();
                Console.Error.WriteLine("Warning: unable to locate the following type definitions: " + string.Join(", ", missingNames));
            }

            var duplicateTypeDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id == "CS0101")
                .ToArray();
            if (duplicateTypeDiagnostics.Length > 0)
            {
                foreach (var grp in duplicateTypeDiagnostics.GroupBy(d => d.GetMessage()))
                {
                    var files = grp.Select(d => d.Location.SourceTree?.FilePath)
                                   .Where(p => !string.IsNullOrEmpty(p))
                                   .Distinct();
                    Console.Error.WriteLine("Warning: duplicate type definition - " + grp.Key + " in: " + string.Join(", ", files));
                }
            }

            INamedTypeSymbol? mainViewModelSymbol = null;
            string originalVmName = "";
            foreach (var tree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var classDeclarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classSyntax in classDeclarations)
                {
                    if (semanticModel.GetDeclaredSymbol(classSyntax) is INamedTypeSymbol classSymbol)
                    {
                        bool match = Helpers.InheritsFrom(classSymbol, observableObjectFullName);
                      

                        if (match)
                        {
                            mainViewModelSymbol = classSymbol;
                            originalVmName = classSymbol.Name;
                            break;
                        }
                    }
                    if (mainViewModelSymbol != null) break;
                }
                if (mainViewModelSymbol != null) break;
            }
            if (mainViewModelSymbol == null)
                return (null, "", new List<PropertyInfo>(), new List<CommandInfo>(), compilation);
            var properties = GetObservableProperties(mainViewModelSymbol, observablePropertyAttributeFullName, compilation);
            var commands = GetRelayCommands(mainViewModelSymbol, relayCommandAttributeFullName, compilation);

            var searchDirs = viewModelFiles.Select(f => Path.GetDirectoryName(f)!)
                                          .Where(p => !string.IsNullOrEmpty(p))
                                          .Distinct();

            compilation = await LoadDependentTypesAsync((CSharpCompilation)compilation, searchDirs,
                properties.Select(p => p.FullTypeSymbol!).Concat(commands.SelectMany(c => c.Parameters.Select(p => p.FullTypeSymbol!))));

            mainViewModelSymbol = compilation.GetTypeByMetadataName(mainViewModelSymbol.ToDisplayString()) ?? mainViewModelSymbol;
            properties = GetObservableProperties(mainViewModelSymbol, observablePropertyAttributeFullName, compilation);
            commands = GetRelayCommands(mainViewModelSymbol, relayCommandAttributeFullName, compilation);

            return (mainViewModelSymbol, originalVmName, properties, commands, compilation);
        }

        public static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName, Compilation compilation)
        {
            var props = new List<PropertyInfo>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    if (fieldSymbol.IsStatic) continue;
                    var obsPropAttribute = fieldSymbol.GetAttributes().FirstOrDefault(a =>
                        Helpers.AttributeMatches(a, observablePropertyAttributeFullName));
                    if (obsPropAttribute != null)
                    {
                        string propertyName = fieldSymbol.Name.TrimStart('_');
                        if (propertyName.Length > 0)
                        {
                            propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                        }
                        else continue;
                        var typeName = fieldSymbol.Type.ToDisplayString(FullNameFormat).Replace("global::", "");
                        props.Add(new PropertyInfo(propertyName, typeName, fieldSymbol.Type));
                    }
                }
                else if (member is IPropertySymbol propertySymbol)
                {
                    if (propertySymbol.IsStatic) continue;
                    var obsPropAttribute = propertySymbol.GetAttributes().FirstOrDefault(a =>
                        Helpers.AttributeMatches(a, observablePropertyAttributeFullName));
                    if (obsPropAttribute != null)
                    {
                        var typeName = propertySymbol.Type.ToDisplayString(FullNameFormat).Replace("global::", "");
                        props.Add(new PropertyInfo(propertySymbol.Name, typeName, propertySymbol.Type));
                    }
                }
            }
            return props;
        }
        public static List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol, string relayCommandAttributeFullName, Compilation compilation)
        {
            var cmds = new List<CommandInfo>();
            foreach (var member in Helpers.GetAllMembers(classSymbol))
            {
                if (member is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.IsStatic) continue;
                    var relayCmdAttribute = methodSymbol.GetAttributes().FirstOrDefault(a =>
                        Helpers.AttributeMatches(a, relayCommandAttributeFullName));
                    if (relayCmdAttribute != null)
                    {
                        var parameters = methodSymbol.Parameters
                            .Select(p => new ParameterInfo(p.Name, p.Type.ToDisplayString(FullNameFormat).Replace("global::", ""), p.Type))
                            .ToList();
                        bool isAsync = methodSymbol.IsAsync ||
                            (methodSymbol.ReturnType is INamedTypeSymbol rtSym &&
                                (rtSym.Name == "Task" || rtSym.Name == "ValueTask" ||
                                 (rtSym.IsGenericType &&
                                    (rtSym.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.Task" ||
                                     rtSym.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.ValueTask"))));
                        string baseMethodName = methodSymbol.Name;
                        if (isAsync && baseMethodName.EndsWith("Async", System.StringComparison.Ordinal))
                        {
                            baseMethodName = baseMethodName.Substring(0, baseMethodName.Length - "Async".Length);
                        }
                        string commandPropertyName = baseMethodName + "Command";
                        cmds.Add(new CommandInfo(methodSymbol.Name, commandPropertyName, parameters, isAsync));
                    }
                }
            }
            return cmds;
        }

        private static async Task<CSharpCompilation> LoadDependentTypesAsync(CSharpCompilation compilation, IEnumerable<string> searchDirs, IEnumerable<ITypeSymbol?> rootTypes)
        {
            var queue = new Queue<ITypeSymbol>(rootTypes.Where(t => t != null)!);
            var processed = new HashSet<string>();
            var missing = new Dictionary<string, string>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current is IArrayTypeSymbol arr)
                {
                    queue.Enqueue(arr.ElementType);
                    continue;
                }

                if (current is not INamedTypeSymbol named)
                    continue;

                string fullName = named.ToDisplayString();
                if (!processed.Add(fullName))
                    continue;

                // Skip expanding System.Threading.Tasks.Task and Task<T> to avoid traversing
                // a large graph of framework types. For generic Task<T>, ensure T itself
                // is processed so that its definition is still included.
                if (fullName == "System.Threading.Tasks.Task" ||
                    fullName.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal))
                {
                    if (named.IsGenericType)
                    {
                        foreach (var arg in named.TypeArguments)
                            queue.Enqueue(arg);
                    }
                    continue;
                }

                if (named.TypeKind == TypeKind.Error)
                {
                    string? filePath = null;
                    foreach (var dir in searchDirs)
                    {
                        var candidate = Path.Combine(dir, named.Name + ".cs");
                        if (File.Exists(candidate)) { filePath = candidate; break; }
                    }
                    if (filePath != null)
                    {
                        var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
                        if (tree == null)
                        {
                            var content = await File.ReadAllTextAsync(filePath);
                            tree = CSharpSyntaxTree.ParseText(content, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: filePath);
                            compilation = compilation.AddSyntaxTrees(tree);
                        }

                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = tree.GetRoot();
                        var typeDecl = root.DescendantNodes().FirstOrDefault(n =>
                            (n is TypeDeclarationSyntax t && t.Identifier.Text == named.Name) ||
                            (n is EnumDeclarationSyntax e && e.Identifier.Text == named.Name));
                        if (typeDecl != null && semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol resolvedSym)
                        {
                            named = resolvedSym;
                        }
                    }
                    if (named.TypeKind == TypeKind.Error)
                    {
                        var searched = filePath ?? string.Join(", ", searchDirs.Select(d => Path.Combine(d, named.Name + ".cs")));
                        missing[fullName] = searched;
                        continue;
                    }
                }

                if (named.IsGenericType)
                {
                    foreach (var arg in named.TypeArguments)
                        queue.Enqueue(arg);
                }

                foreach (var prop in Helpers.GetAllMembers(named).OfType<IPropertySymbol>())
                    queue.Enqueue(prop.Type);
            }

            if (missing.Count > 0)
            {
                foreach (var kv in missing)
                    Console.Error.WriteLine($"Warning: unable to locate type definition for {kv.Key} (searched: {kv.Value})");
            }

            return compilation;
        }
    }
}
