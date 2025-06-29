using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcRemoteMvvmModelUtil
{
    public class ViewModelAnalyzer
    {
        public static async Task<(INamedTypeSymbol? ViewModelSymbol, string ViewModelName, List<PropertyInfo> Properties, List<CommandInfo> Commands, Compilation Compilation)> AnalyzeAsync(
            IEnumerable<string> viewModelFiles,
            string observablePropertyAttributeFullName,
            string relayCommandAttributeFullName,
            string generateGrpcRemoteAttributeFullName,
            IEnumerable<string> referencePaths,
            string? attributeSourceContent = null,
            string? attributeSourcePath = null)
        {
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in viewModelFiles)
            {
                var fileContent = await File.ReadAllTextAsync(filePath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: filePath));
            }
            if (!string.IsNullOrEmpty(attributeSourceContent) && !string.IsNullOrEmpty(attributeSourcePath))
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(attributeSourceContent, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: attributeSourcePath));
            }
            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
            foreach (var refPath in referencePaths)
            {
                if (File.Exists(refPath))
                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(refPath));
            }
            var compilation = CSharpCompilation.Create("ViewModelAssembly",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

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
                        foreach (var attr in classSymbol.GetAttributes())
                        {
                            string? attrFqn = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                            string? attrShortName = attr.AttributeClass?.Name;
                            if (attrFqn == generateGrpcRemoteAttributeFullName ||
                                (attr.AttributeClass != null && !attr.AttributeClass.IsUnboundGenericType && attrFqn == null && attrShortName == Path.GetFileNameWithoutExtension(generateGrpcRemoteAttributeFullName)))
                            {
                                mainViewModelSymbol = classSymbol;
                                originalVmName = classSymbol.Name;
                                break;
                            }
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
            return (mainViewModelSymbol, originalVmName, properties, commands, compilation);
        }

        public static List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol, string observablePropertyAttributeFullName, Compilation compilation)
        {
            var props = new List<PropertyInfo>();
            foreach (var member in Helpers.GetAllMembers(classSymbol))
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    var obsPropAttribute = fieldSymbol.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == observablePropertyAttributeFullName);
                    if (obsPropAttribute != null)
                    {
                        string propertyName = fieldSymbol.Name.TrimStart('_');
                        if (propertyName.Length > 0)
                        {
                            propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                        }
                        else continue;
                        props.Add(new PropertyInfo(propertyName, fieldSymbol.Type.ToDisplayString(), fieldSymbol.Type));
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
                    var relayCmdAttribute = methodSymbol.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == relayCommandAttributeFullName);
                    if (relayCmdAttribute != null)
                    {
                        string baseMethodName = methodSymbol.Name;
                        if (baseMethodName.EndsWith("Async", System.StringComparison.Ordinal))
                        {
                            baseMethodName = baseMethodName.Substring(0, baseMethodName.Length - "Async".Length);
                        }
                        string commandPropertyName = baseMethodName + "Command";
                        var parameters = methodSymbol.Parameters.Select(p => new ParameterInfo(p.Name, p.Type.ToDisplayString(), p.Type)).ToList();
                        bool isAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.Task")));
                        cmds.Add(new CommandInfo(methodSymbol.Name, commandPropertyName, parameters, isAsync));
                    }
                }
            }
            return cmds;
        }
    }
}
