using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PeakSWC.MvvmSourceGenerator
{
    [Generator]
    public class GrpcRemoteMvvmGenerator : IIncrementalGenerator
    {
        private const string GenerateGrpcRemoteAttributeFullName = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";
        private const string ObservablePropertyAttributeFullName = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
        private const string RelayCommandAttributeFullName = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        // Diagnostic Descriptors - Ensure messageFormats are single lines or properly concatenated.
        private static readonly DiagnosticDescriptor SGINFO001_GeneratorStarted = new DiagnosticDescriptor(
            id: "SGINFO001", title: "Generator Execution", messageFormat: "GrpcRemoteMvvmGenerator Execute method started.",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO002_NoClassesFound = new DiagnosticDescriptor(
            id: "SGINFO002", title: "Generator Execution", messageFormat: "No classes found with the GenerateGrpcRemoteAttribute.",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO003_ProcessingClass = new DiagnosticDescriptor(
            id: "SGINFO003", title: "Generator Execution", messageFormat: "Processing class: {0}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGWARN001_AttributeNotFound = new DiagnosticDescriptor(
            id: "SGWARN001", title: "Attribute Resolution", messageFormat: "GenerateGrpcRemoteAttribute not found or not resolved on class {0}. Expected FQN: {1}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO004_AttributeFound = new DiagnosticDescriptor(
            id: "SGINFO004", title: "Attribute Resolution", messageFormat: "Found GenerateGrpcRemoteAttribute on {0}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGERR001_MissingAttributeArgs = new DiagnosticDescriptor(
            id: "SGERR001", title: "Attribute Usage", messageFormat: "Class '{0}' is missing required constructor arguments (protoCsNamespace, grpcServiceName) for [GenerateGrpcRemoteAttribute].", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO005_ExtractedMembers = new DiagnosticDescriptor(
            id: "SGINFO005", title: "Generator Execution", messageFormat: "Extracted {0} properties and {1} commands for {2}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO006_GeneratedServerImpl = new DiagnosticDescriptor(
            id: "SGINFO006", title: "Code Generation", messageFormat: "Generated server implementation for {0} in namespace {1}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO007_GeneratedClientProxy = new DiagnosticDescriptor(
            id: "SGINFO007", title: "Code Generation", messageFormat: "Generated client proxy for {0} in namespace {1}.", // Corrected placeholder
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);


        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GenerateGrpcRemoteAttributeFullName,
                    predicate: (node, _) => node is ClassDeclarationSyntax,
                    transform: (ctx, _) => (ClassDeclarationSyntax)ctx.TargetNode);

            IncrementalValueProvider<(Compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
                Execute(source.Item1, source.Item2, spc));
        }

        internal class PropertyInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }
        internal class CommandInfoData { public string MethodName { get; set; } = ""; public string CommandPropertyName { get; set; } = ""; public List<ParameterInfoData> Parameters { get; set; } = new List<ParameterInfoData>(); public bool IsAsync { get; set; } }
        internal class ParameterInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }


        private void Execute(Compilation compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            context.ReportDiagnostic(Diagnostic.Create(SGINFO001_GeneratorStarted, Location.None));

            if (classes.IsDefaultOrEmpty)
            {
                context.ReportDiagnostic(Diagnostic.Create(SGINFO002_NoClassesFound, Location.None));
                return;
            }

            foreach (var classSyntax in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

                if (semanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(SGINFO003_ProcessingClass, classSyntax.GetLocation(), classSymbol.ToDisplayString()));

                var attributeData = classSymbol.GetAttributes().FirstOrDefault(ad =>
                {
                    if (ad.AttributeClass == null) return false;
                    string? fqn = ad.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                    return fqn == GenerateGrpcRemoteAttributeFullName || ad.AttributeClass.Name == GenerateGrpcRemoteAttributeFullName;
                });

                if (attributeData == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SGWARN001_AttributeNotFound, classSyntax.GetLocation(), classSymbol.Name, GenerateGrpcRemoteAttributeFullName));
                    continue;
                }
                context.ReportDiagnostic(Diagnostic.Create(SGINFO004_AttributeFound, classSyntax.GetLocation(), classSymbol.Name));


                string protoCsNamespace = string.Empty;
                string grpcServiceNameFromAttribute = string.Empty;

                if (attributeData.ConstructorArguments.Length >= 1)
                {
                    protoCsNamespace = attributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                }
                if (attributeData.ConstructorArguments.Length >= 2)
                {
                    grpcServiceNameFromAttribute = attributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(protoCsNamespace) || string.IsNullOrEmpty(grpcServiceNameFromAttribute))
                {
                    Location errorLocation = attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? classSyntax.GetLocation();
                    context.ReportDiagnostic(Diagnostic.Create(SGERR001_MissingAttributeArgs, errorLocation, classSymbol.Name));
                    continue;
                }

                string serverImplNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ServerImplNamespace").Value.Value?.ToString()
                                             ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.GrpcServices";
                string clientProxyNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ClientProxyNamespace").Value.Value?.ToString()
                                               ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.RemoteClients";

                var originalViewModelName = classSymbol.Name;
                var originalViewModelFullName = classSymbol.ToDisplayString();

                List<PropertyInfoData> properties = GetObservableProperties(classSymbol);
                List<CommandInfoData> commands = GetRelayCommands(classSymbol);

                context.ReportDiagnostic(Diagnostic.Create(SGINFO005_ExtractedMembers, Location.None, properties.Count, commands.Count, originalViewModelName));


                string serverImplCode = GenerateServerImplementation(
                    serverImplNamespace, originalViewModelName, originalViewModelFullName,
                    protoCsNamespace, grpcServiceNameFromAttribute,
                    properties, commands, compilation);
                context.AddSource($"{originalViewModelName}GrpcServiceImpl.g.cs", SourceText.From(serverImplCode, Encoding.UTF8));
                context.ReportDiagnostic(Diagnostic.Create(SGINFO006_GeneratedServerImpl, Location.None, originalViewModelName, serverImplNamespace));


                string clientProxyCode = GenerateClientProxyViewModel(
                    clientProxyNamespace, originalViewModelName,
                    protoCsNamespace, grpcServiceNameFromAttribute,
                    properties, commands, compilation);
                context.AddSource($"{originalViewModelName}RemoteClient.g.cs", SourceText.From(clientProxyCode, Encoding.UTF8));
                context.ReportDiagnostic(Diagnostic.Create(SGINFO007_GeneratedClientProxy, Location.None, originalViewModelName, clientProxyNamespace));
            }
        }

        private List<PropertyInfoData> GetObservableProperties(INamedTypeSymbol classSymbol)
        {
            var props = new List<PropertyInfoData>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    var obsPropAttribute = fieldSymbol.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == ObservablePropertyAttributeFullName ||
                        a.AttributeClass?.Name == ObservablePropertyAttributeFullName);

                    if (obsPropAttribute != null)
                    {
                        string propertyName = fieldSymbol.Name.TrimStart('_');
                        if (propertyName.Length > 0)
                        {
                            propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                        }
                        else continue;

                        props.Add(new PropertyInfoData { Name = propertyName, Type = fieldSymbol.Type.ToDisplayString(), FullTypeSymbol = fieldSymbol.Type });
                    }
                }
            }
            return props;
        }
        private List<CommandInfoData> GetRelayCommands(INamedTypeSymbol classSymbol)
        {
            var cmds = new List<CommandInfoData>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol)
                {
                    var relayCmdAttribute = methodSymbol.GetAttributes().FirstOrDefault(a =>
                       a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == RelayCommandAttributeFullName ||
                       a.AttributeClass?.Name == RelayCommandAttributeFullName);

                    if (relayCmdAttribute != null)
                    {
                        string commandPropertyName = methodSymbol.Name + "Command";
                        cmds.Add(new CommandInfoData
                        {
                            MethodName = methodSymbol.Name,
                            CommandPropertyName = commandPropertyName,
                            Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoData { Name = p.Name, Type = p.Type.ToDisplayString(), FullTypeSymbol = p.Type }).ToList(),
                            IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.Task")))
                        });
                    }
                }
            }
            return cmds;
        }

        private string GenerateServerImplementation(
            string serverImplNamespace, string vmName, string vmFullName,
            string protoCsNamespace, string grpcServiceName,
            List<PropertyInfoData> props, List<CommandInfoData> cmds, Compilation compilation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated by GrpcRemoteMvvmGenerator at {DateTime.Now}");
            sb.AppendLine($"// Server implementation for {vmFullName}");
            sb.AppendLine($"// PROTO CS NAMESPACE: {protoCsNamespace}");
            sb.AppendLine($"// GRPC SERVICE NAME: {grpcServiceName}");
            sb.AppendLine();
            sb.AppendLine("using Grpc.Core;");
            sb.AppendLine($"using {protoCsNamespace}; // For gRPC base, messages");
            sb.AppendLine("using Google.Protobuf.WellKnownTypes; // For Empty, StringValue, Int32Value, Any etc.");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {serverImplNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {vmName}GrpcServiceImpl : {protoCsNamespace}.{grpcServiceName}.{grpcServiceName}Base");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {vmFullName} _viewModel;");
            sb.AppendLine($"        private readonly List<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>> _subscribers = new List<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>>();");
            sb.AppendLine($"        private readonly object _subscriberLock = new object();");
            sb.AppendLine();
            sb.AppendLine($"        public {vmName}GrpcServiceImpl({vmFullName} viewModel)");
            sb.AppendLine("        {");
            sb.AppendLine("            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
            sb.AppendLine("            if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<{protoCsNamespace}.{vmName}State> GetState(Empty request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var state = new {protoCsNamespace}.{vmName}State();");
            foreach (var prop in props)
            {
                string csharpPropertyName = prop.Name;
                string protoMessageFieldName = ToPascalCase(prop.Name);
                sb.AppendLine($"            // Mapping property: {csharpPropertyName} to state.{protoMessageFieldName}");
                sb.AppendLine($"            try {{");
                sb.AppendLine($"                var propValue = _viewModel.{csharpPropertyName};");
                // Corrected: Check for null before assignment for reference types.
                // For value types, direct assignment is fine.
                // This assumes the generated proto C# property can handle null if the C# property is nullable.
                // If prop.Type is a value type, 'propValue != null' is usually not needed unless it's Nullable<T>.
                // A more robust check would involve prop.FullTypeSymbol.IsReferenceType or IsValueType.
                if (prop.FullTypeSymbol?.IsReferenceType == true || prop.FullTypeSymbol?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    sb.AppendLine($"                if (propValue != null) state.{protoMessageFieldName} = propValue;");
                }
                else
                {
                    sb.AppendLine($"                state.{protoMessageFieldName} = propValue;");
                }
                sb.AppendLine($"            }} catch (Exception ex) {{ Console.WriteLine($\"Error mapping property {csharpPropertyName} to state.{protoMessageFieldName}: {{ex.Message}}\"); }}");
            }
            sb.AppendLine("            return Task.FromResult(state);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override async Task SubscribeToPropertyChanges(Empty request, IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification> responseStream, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            lock(_subscriberLock) { _subscribers.Add(responseStream); }");
            sb.AppendLine("            try { await context.CancellationToken.WhenCancelled(); }");
            sb.AppendLine("            catch (OperationCanceledException) { /* Expected */ }");
            sb.AppendLine("            catch (Exception ex) { Console.WriteLine($\"[GrpcService:{vmName}] Error in Subscribe: {ex.Message}\"); }"); // Corrected: ex.Message
            sb.AppendLine("            finally { lock(_subscriberLock) { _subscribers.Remove(responseStream); } }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<Empty> UpdatePropertyValue({protoCsNamespace}.UpdatePropertyValueRequest request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);");
            sb.AppendLine("            if (propertyInfo != null && propertyInfo.CanWrite)");
            sb.AppendLine("            {");
            sb.AppendLine("                try {");
            sb.AppendLine($"                   if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);");
            sb.AppendLine($"                   else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);");
            sb.AppendLine($"                   else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);");
            sb.AppendLine("                    // TODO: Add more type checks and unpacking logic here (double, float, long, DateTime/Timestamp etc.)");
            sb.AppendLine("                    else { Console.WriteLine($\"[GrpcService:{vmName}] UpdatePropertyValue: Unpacking not implemented for property {request.PropertyName} and type {request.NewValue.TypeUrl}\"); }");
            sb.AppendLine("                } catch (Exception ex) { Console.WriteLine($\"[GrpcService:{vmName}] Error setting property {request.PropertyName}: {{ex.Message}}\"); }"); // Corrected: {{ex.Message}}
            sb.AppendLine("            }");
            sb.AppendLine("            else { Console.WriteLine($\"[GrpcService:{vmName}] UpdatePropertyValue: Property {request.PropertyName} not found or not writable.\"); }");
            sb.AppendLine("            return Task.FromResult(new Empty());");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                sb.AppendLine($"        public override async Task<{protoCsNamespace}.{cmd.MethodName}Response> {cmd.MethodName}({protoCsNamespace}.{cmd.MethodName}Request request, ServerCallContext context)");
                sb.AppendLine("        {");
                string commandPropertyAccess = $"_viewModel.{cmd.CommandPropertyName}";

                if (cmd.IsAsync)
                {
                    sb.AppendLine($"            var command = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;"); // More specific type
                    sb.AppendLine($"            if (command != null)");
                    sb.AppendLine("            {");
                    if (cmd.Parameters.Count == 1 && cmd.Parameters[0].FullTypeSymbol != null)
                    {
                        // Attempt to cast to generic version if parameter exists
                        sb.AppendLine($"                var typedCommand = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<{cmd.Parameters[0].Type}>;");
                        sb.AppendLine($"                if (typedCommand != null) await typedCommand.ExecuteAsync(request.{ToPascalCase(cmd.Parameters[0].Name)});");
                        sb.AppendLine($"                else await command.ExecuteAsync(request); // Fallback if cast fails or not generic with that param type");
                    }
                    else if (cmd.Parameters.Count == 0)
                        sb.AppendLine("                await command.ExecuteAsync(null);");
                    else
                        sb.AppendLine("                await command.ExecuteAsync(request); // Pass request object for multi-param or if command expects it");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine($"            var command = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand;"); // More specific type
                    sb.AppendLine($"            if (command != null)");
                    sb.AppendLine("            {");
                    if (cmd.Parameters.Count == 1 && cmd.Parameters[0].FullTypeSymbol != null)
                    {
                        sb.AppendLine($"               var typedCommand = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand<{cmd.Parameters[0].Type}>;");
                        sb.AppendLine($"               if (typedCommand != null) typedCommand.Execute(request.{ToPascalCase(cmd.Parameters[0].Name)});");
                        sb.AppendLine($"               else command.Execute(request);");
                    }
                    else if (cmd.Parameters.Count == 0)
                        sb.AppendLine("                command.Execute(null);");
                    else
                        sb.AppendLine("                command.Execute(request); // Pass request object for multi-param");
                    sb.AppendLine("            }");
                }
                sb.AppendLine($"            return new {protoCsNamespace}.{cmd.MethodName}Response();");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine($"        private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (string.IsNullOrEmpty(e.PropertyName)) return;");
            sb.AppendLine("            object? newValue = null;");
            sb.AppendLine("            try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }");
            sb.AppendLine("            catch (Exception ex) { Console.WriteLine($\"[GrpcService:{vmName}] Error getting property value for {e.PropertyName}: {{ex.Message}}\"); return; }"); // Corrected: {{ex.Message}}
            sb.AppendLine();
            sb.AppendLine($"            var notification = new {protoCsNamespace}.PropertyChangeNotification {{ PropertyName = e.PropertyName }};");
            sb.AppendLine("            if (newValue == null) notification.NewValue = Any.Pack(new Empty());");
            sb.AppendLine("            else if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });");
            sb.AppendLine("            else if (newValue is int i) notification.NewValue = Any.Pack(new Int32Value { Value = i });");
            sb.AppendLine("            else if (newValue is bool b) notification.NewValue = Any.Pack(new BoolValue { Value = b });");
            sb.AppendLine("            else if (newValue is double d) notification.NewValue = Any.Pack(new DoubleValue { Value = d });");
            sb.AppendLine("            else if (newValue is float f) notification.NewValue = Any.Pack(new FloatValue { Value = f });");
            sb.AppendLine("            else if (newValue is long l) notification.NewValue = Any.Pack(new Int64Value { Value = l });");
            sb.AppendLine("            else if (newValue is DateTime dt) notification.NewValue = Any.Pack(Timestamp.FromDateTime(dt.ToUniversalTime()));");
            sb.AppendLine("            else { Console.WriteLine($\"[GrpcService:{vmName}] PropertyChanged: Packing not implemented for type {newValue.GetType().FullName} of property {e.PropertyName}\"); notification.NewValue = Any.Pack(new StringValue { Value = newValue.ToString() }); }");
            sb.AppendLine();
            sb.AppendLine($"            List<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>> currentSubscribers;");
            sb.AppendLine("            lock(_subscriberLock) { currentSubscribers = _subscribers.ToList(); }");
            sb.AppendLine();
            sb.AppendLine("            var writeTasks = new List<Task>();");
            sb.AppendLine("            foreach (var sub in currentSubscribers)");
            sb.AppendLine("            {");
            sb.AppendLine("                writeTasks.Add(Task.Run(async () => {"); // Consider Task.Factory.StartNew for more control if needed
            sb.AppendLine("                   try { await sub.WriteAsync(notification); }");
            sb.AppendLine("                   catch { lock(_subscriberLock) { _subscribers.Remove(sub); } }");
            sb.AppendLine("                }));");
            sb.AppendLine("            }");
            sb.AppendLine("            try { await Task.WhenAll(writeTasks); } catch (Exception ex) { Console.WriteLine($\"[GrpcService:{vmName}] Error writing notifications: {{ex.Message}}\"); }"); // Corrected: {{ex.Message}}
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateClientProxyViewModel(
            string clientProxyNamespace, string originalVmName,
            string protoCsNamespace, string grpcServiceNameFromAttribute,
            List<PropertyInfoData> props, List<CommandInfoData> cmds, Compilation compilation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated by GrpcRemoteMvvmGenerator at {DateTime.Now}");
            sb.AppendLine($"// Client Proxy ViewModel for {originalVmName}");
            sb.AppendLine();
            sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
            sb.AppendLine("using CommunityToolkit.Mvvm.Input;");
            sb.AppendLine("using Grpc.Core;");
            sb.AppendLine("using Grpc.Net.Client;");
            sb.AppendLine($"using {protoCsNamespace};");
            sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Diagnostics;");
            sb.AppendLine("#if WPF_DISPATCHER");
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sb.AppendLine($"namespace {clientProxyNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {originalVmName}RemoteClient : ObservableObject, IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {protoCsNamespace}.{grpcServiceNameFromAttribute}.{grpcServiceNameFromAttribute}Client _grpcClient;");
            sb.AppendLine("        private CancellationTokenSource _cts = new CancellationTokenSource();");
            sb.AppendLine("        private bool _isInitialized = false;");
            sb.AppendLine("        private bool _isDisposed = false;");
            sb.AppendLine();

            foreach (var prop in props)
            {
                sb.AppendLine($"        private {prop.Type} _{LowercaseFirst(prop.Name)};");
                sb.AppendLine($"        public {prop.Type} {prop.Name}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => _{LowercaseFirst(prop.Name)};");
                sb.AppendLine("            private set => SetProperty(ref _{LowercaseFirst(prop.Name)}, value);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var cmd in cmds)
            {
                string commandInterfaceType = cmd.IsAsync ? "IAsyncRelayCommand" : "IRelayCommand";
                string methodGenericTypeArg = "";

                if (cmd.Parameters.Any())
                {
                    // For simplicity, assume single parameter for generic commands.
                    // CommunityToolkit.Mvvm supports RelayCommand<T> and AsyncRelayCommand<T>.
                    var paramType = cmd.Parameters[0].Type;
                    methodGenericTypeArg = $"<{paramType}>";
                    commandInterfaceType = cmd.IsAsync ? $"IAsyncRelayCommand{methodGenericTypeArg}" : $"IRelayCommand{methodGenericTypeArg}";
                }
                sb.AppendLine($"        public {commandInterfaceType} {cmd.CommandPropertyName} {{ get; }}");
            }
            sb.AppendLine();

            sb.AppendLine($"        public {originalVmName}RemoteClient({protoCsNamespace}.{grpcServiceNameFromAttribute}.{grpcServiceNameFromAttribute}Client grpcClient)");
            sb.AppendLine("        {");
            sb.AppendLine("            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));");
            foreach (var cmd in cmds)
            {
                string remoteExecuteMethodName = $"RemoteExecute_{cmd.MethodName}";
                string methodGenericTypeArg = "";
                string commandConcreteType = cmd.IsAsync ? "AsyncRelayCommand" : "RelayCommand";

                if (cmd.Parameters.Any())
                {
                    methodGenericTypeArg = $"<{cmd.Parameters[0].Type}>";
                    commandConcreteType += methodGenericTypeArg;
                }

                if (cmd.IsAsync)
                {
                    sb.AppendLine($"            {cmd.CommandPropertyName} = new {commandConcreteType}({remoteExecuteMethodName}Async);");
                }
                else
                {
                    sb.AppendLine($"            {cmd.CommandPropertyName} = new {commandConcreteType}({remoteExecuteMethodName});");
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public async Task InitializeRemoteAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_isInitialized || _isDisposed) return;");
            sb.AppendLine("            Debug.WriteLine($\"[{originalVmName}RemoteClient] Initializing...\");");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);");
            sb.AppendLine($"                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);");
            sb.AppendLine("                Debug.WriteLine($\"[{originalVmName}RemoteClient] Initial state received.\");");
            foreach (var prop in props)
            {
                string protoStateFieldName = ToPascalCase(prop.Name);
                sb.AppendLine($"                this.{prop.Name} = state.{protoStateFieldName};");
            }
            sb.AppendLine("                _isInitialized = true;");
            sb.AppendLine("                Debug.WriteLine($\"[{originalVmName}RemoteClient] Initialized successfully.\");");
            sb.AppendLine("                StartListeningToPropertyChanges(_cts.Token);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (RpcException ex) { Debug.WriteLine($\"[ClientProxy:{originalVmName}] Failed to initialize: {ex.Status.StatusCode} - {ex.Status.Detail}\"); }");
            sb.AppendLine("            catch (OperationCanceledException) { Debug.WriteLine($\"[ClientProxy:{originalVmName}] Initialization cancelled.\"); }");
            sb.AppendLine("            catch (Exception ex) { Debug.WriteLine($\"[ClientProxy:{originalVmName}] Unexpected error during initialization: {{ex.Message}}\"); }"); // Corrected: {{ex.Message}}
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                string paramListWithType = string.Join(", ", cmd.Parameters.Select(p => $"{p.Type} {LowercaseFirst(p.Name)}"));
                string requestCreation = $"new {protoCsNamespace}.{cmd.MethodName}Request()";

                if (cmd.Parameters.Any())
                {
                    var paramAssignments = cmd.Parameters.Select(p => $"{ToPascalCase(p.Name)} = {LowercaseFirst(p.Name)}");
                    requestCreation = $"new {protoCsNamespace}.{cmd.MethodName}Request {{ {string.Join(", ", paramAssignments)} }}";
                }

                string methodSignature = cmd.IsAsync ? $"private async Task RemoteExecute_{cmd.MethodName}Async({paramListWithType})"
                                                     : $"private void RemoteExecute_{cmd.MethodName}({paramListWithType})";
                sb.AppendLine($"        {methodSignature}");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (!_isInitialized || _isDisposed) {{ Debug.WriteLine(\"[ClientProxy:{originalVmName}] Not initialized or disposed, command {cmd.MethodName} skipped.\"); {(cmd.IsAsync ? "return Task.CompletedTask;" : "return;")} }}");
                sb.AppendLine("            Debug.WriteLine($\"[ClientProxy:{originalVmName}] Executing command {cmd.MethodName} remotely...\");");
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                if (cmd.IsAsync)
                {
                    sb.AppendLine($"                await _grpcClient.{cmd.MethodName}Async({requestCreation}, cancellationToken: _cts.Token);");
                }
                else
                {
                    sb.AppendLine($"                _ = _grpcClient.{cmd.MethodName}Async({requestCreation}, cancellationToken: _cts.Token);");
                }
                sb.AppendLine("            }");
                sb.AppendLine($"            catch (RpcException ex) {{ Debug.WriteLine($\"[ClientProxy:{originalVmName}] Error executing command {cmd.MethodName}: {{ex.Status.StatusCode}} - {{ex.Status.Detail}}\"); }}"); // Corrected: {{ex.Status...}}
                sb.AppendLine($"            catch (OperationCanceledException) {{ Debug.WriteLine($\"[ClientProxy:{originalVmName}] Command {cmd.MethodName} cancelled.\"); }}");
                sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine($\"[ClientProxy:{originalVmName}] Unexpected error executing command {cmd.MethodName}: {{ex.Message}}\"); }}"); // Corrected: {{ex.Message}}
                if (cmd.IsAsync && !cmd.Parameters.Any()) sb.AppendLine("            // return Task.CompletedTask; // Not strictly needed for AsyncRelayCommand");
                else if (cmd.IsAsync && cmd.Parameters.Any()) sb.AppendLine("            // return Task.CompletedTask; // Not strictly needed for AsyncRelayCommand<T>");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine("            _ = Task.Run(async () => ");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_isDisposed) return;");
            sb.AppendLine("                Debug.WriteLine($\"[{originalVmName}RemoteClient] Starting property change listener...\");");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    using var call = _grpcClient.SubscribeToPropertyChanges(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (_isDisposed) break;");
            sb.AppendLine("                        Debug.WriteLine($\"[{originalVmName}RemoteClient] Received property update: {update.PropertyName}\");");
            sb.AppendLine("                        Action updateAction = () => {");
            sb.AppendLine("                            switch (update.PropertyName)");
            sb.AppendLine("                            {");
            foreach (var prop in props)
            {
                string wkt = GetProtoWellKnownTypeFor(prop.FullTypeSymbol!, compilation);
                string csharpPropName = prop.Name;
                sb.AppendLine($"                                case nameof({csharpPropName}):");
                if (wkt == "StringValue") sb.AppendLine($"                                    if (update.NewValue.Is(StringValue.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<StringValue>().Value; break;");
                else if (wkt == "Int32Value") sb.AppendLine($"                                    if (update.NewValue.Is(Int32Value.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<Int32Value>().Value; break;");
                else if (wkt == "BoolValue") sb.AppendLine($"                                    if (update.NewValue.Is(BoolValue.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<BoolValue>().Value; break;");
                else if (wkt == "DoubleValue") sb.AppendLine($"                                    if (update.NewValue.Is(DoubleValue.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<DoubleValue>().Value; break;");
                else if (wkt == "FloatValue") sb.AppendLine($"                                    if (update.NewValue.Is(FloatValue.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<FloatValue>().Value; break;");
                else if (wkt == "Int64Value") sb.AppendLine($"                                    if (update.NewValue.Is(Int64Value.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<Int64Value>().Value; break;");
                else if (wkt == "Timestamp" && prop.Type == "DateTime") sb.AppendLine($"                                    if (update.NewValue.Is(Timestamp.Descriptor)) this.{csharpPropName} = update.NewValue.Unpack<Timestamp>().ToDateTime(); break;");
                else sb.AppendLine($"                                    Debug.WriteLine($\"[ClientProxy:{originalVmName}] Unpacking for {prop.Name} with WKT {wkt} not fully implemented or is Any.\"); break;");
            }
            sb.AppendLine("                                default: Debug.WriteLine($\"[ClientProxy:{originalVmName}] Unknown property in notification: {update.PropertyName}\"); break;");
            sb.AppendLine("                            }");
            sb.AppendLine("                        };");
            sb.AppendLine("                        #if WPF_DISPATCHER");
            sb.AppendLine("                        Application.Current?.Dispatcher.Invoke(updateAction);");
            sb.AppendLine("                        #else");
            sb.AppendLine("                        updateAction();");
            sb.AppendLine("                        #endif");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine($\"[ClientProxy:{originalVmName}] Property subscription cancelled.\"); }");
            sb.AppendLine("                catch (OperationCanceledException) { Debug.WriteLine($\"[ClientProxy:{originalVmName}] Property subscription task cancelled.\"); }");
            sb.AppendLine("                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($\"[ClientProxy:{originalVmName}] Error in property listener: {{ex.GetType().Name}} - {{ex.Message}}\"); }"); // Corrected: {{ex...}}
            sb.AppendLine($"                Debug.WriteLine($\"[{originalVmName}RemoteClient] Property change listener stopped.\");");
            sb.AppendLine("            }, cancellationToken);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_isDisposed) return;");
            sb.AppendLine("            _isDisposed = true;");
            sb.AppendLine("            Debug.WriteLine($\"[{originalVmName}RemoteClient] Disposing...\");");
            sb.AppendLine("            _cts.Cancel();");
            sb.AppendLine("            _cts.Dispose();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string LowercaseFirst(string str) => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str.Substring(1);
        private string ToPascalCase(string str) => string.IsNullOrEmpty(str) ? str : char.ToUpperInvariant(str[0]) + str.Substring(1);
        private string ToSnakeCase(string pascalCaseName)
        {
            if (string.IsNullOrEmpty(pascalCaseName)) return pascalCaseName;
            return Regex.Replace(pascalCaseName, "(?<=.)([A-Z])", "_$1").ToLower();
        }

        private string GetProtoWellKnownTypeFor(ITypeSymbol typeSymbol, Compilation compilation)
        {
            if (typeSymbol is null) return "Any";

            if (typeSymbol is INamedTypeSymbol namedTypeSymbolNullable &&
                namedTypeSymbolNullable.IsGenericType &&
                namedTypeSymbolNullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            {
                typeSymbol = namedTypeSymbolNullable.TypeArguments[0];
            }

            if (typeSymbol.TypeKind == TypeKind.Enum) return "Int32Value";

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_String: return "StringValue";
                case SpecialType.System_Boolean: return "BoolValue";
                case SpecialType.System_Single: return "FloatValue";
                case SpecialType.System_Double: return "DoubleValue";
                case SpecialType.System_Int32: return "Int32Value";
                case SpecialType.System_Int64: return "Int64Value";
                case SpecialType.System_UInt32: return "UInt32Value";
                case SpecialType.System_UInt64: return "UInt64Value";
                case SpecialType.System_SByte: return "Int32Value";
                case SpecialType.System_Byte: return "UInt32Value";
                case SpecialType.System_Int16: return "Int32Value";
                case SpecialType.System_UInt16: return "UInt32Value";
                case SpecialType.System_Char: return "StringValue";
                case SpecialType.System_DateTime: return "Timestamp";
                case SpecialType.System_Decimal: return "StringValue";
                case SpecialType.System_Object: return "Any";
            }

            string fullTypeName = typeSymbol.OriginalDefinition.ToDisplayString();
            switch (fullTypeName)
            {
                case "System.TimeSpan": return "Duration";
                case "System.Guid": return "StringValue";
                case "System.DateTimeOffset": return "Timestamp";
                case "System.Uri": return "StringValue";
                case "System.Version": return "StringValue";
                case "System.Numerics.BigInteger": return "StringValue";
            }

            if (typeSymbol.TypeKind == TypeKind.Array && typeSymbol is IArrayTypeSymbol arraySymbol)
            {
                if (arraySymbol.ElementType.SpecialType == SpecialType.System_Byte && arraySymbol.Rank == 1)
                {
                    return "BytesValue";
                }
            }
            return "Any";
        }
    }
}
