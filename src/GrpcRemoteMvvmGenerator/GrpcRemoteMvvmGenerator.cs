using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// Required for Channel<T>
using System.Threading.Channels;

namespace PeakSWC.MvvmSourceGenerator
{
    [Generator]
    public class GrpcRemoteMvvmGenerator : IIncrementalGenerator
    {
        private const string GenerateGrpcRemoteAttributeFullName = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";
        private const string ObservablePropertyAttributeFullName = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
        private const string RelayCommandAttributeFullName = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        // Diagnostic Descriptors
        private static readonly DiagnosticDescriptor SGINFO001_GeneratorStarted = new DiagnosticDescriptor(
            id: "SGINFO001", title: "Generator Execution", messageFormat: "GrpcRemoteMvvmGenerator Execute method started",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO002_NoClassesFound = new DiagnosticDescriptor(
            id: "SGINFO002", title: "Generator Execution", messageFormat: "No classes found with the GenerateGrpcRemoteAttribute",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO003_ProcessingClass = new DiagnosticDescriptor(
            id: "SGINFO003", title: "Generator Execution", messageFormat: "Processing class: {0}",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGWARN001_AttributeNotFound = new DiagnosticDescriptor(
            id: "SGWARN001", title: "Attribute Resolution", messageFormat: "GenerateGrpcRemoteAttribute not found or not resolved on class {0}. Expected FQN: {1}.",
            category: "SourceGenerator", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO004_AttributeFound = new DiagnosticDescriptor(
            id: "SGINFO004", title: "Attribute Resolution", messageFormat: "Found GenerateGrpcRemoteAttribute on {0}",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGERR001_MissingAttributeArgs = new DiagnosticDescriptor(
            id: "SGERR001", title: "Attribute Usage", messageFormat: "Class '{0}' is missing required constructor arguments (protoCsNamespace, grpcServiceName) for [GenerateGrpcRemoteAttribute]",
            category: "SourceGenerator", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO005_ExtractedMembers = new DiagnosticDescriptor(
            id: "SGINFO005", title: "Generator Execution", messageFormat: "Extracted {0} properties and {1} commands for {2}",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO006_GeneratedServerImpl = new DiagnosticDescriptor(
            id: "SGINFO006", title: "Code Generation", messageFormat: "Generated server implementation for {0} in namespace {1}",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SGINFO007_GeneratedClientProxy = new DiagnosticDescriptor(
            id: "SGINFO007", title: "Code Generation", messageFormat: "Generated client proxy for {0} in namespace {1}",
            category: "SourceGenerator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        /// <summary>
        /// Load the attribute definition from the embedded .cs file.
        /// </summary>
        private static string GetAttributeSource()
        {
            var assembly = typeof(GrpcRemoteMvvmGenerator).Assembly;
            using var stream = assembly.GetManifestResourceStream("GrpcRemoteMvvmGenerator.attributes.GenerateGrpcRemoteAttribute.cs" );
            if (stream == null)
                throw new InvalidOperationException(
                    "Unable to find embedded resource " +
                    "'PeakSWC.MvvmSourceGenerator.attributes.GenerateGrpcRemoteAttribute.cs'"
                );
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string GetGrpcRemoteOptionsSource()
        {
            var assembly = typeof(GrpcRemoteMvvmGenerator).Assembly;
            using var stream = assembly.GetManifestResourceStream("GrpcRemoteMvvmGenerator.attributes.GrpcRemoteOptions.cs");
            if (stream == null)
                throw new InvalidOperationException(
                    "Unable to find embedded resource 'GrpcRemoteMvvmGenerator.attributes.GrpcRemoteOptions.cs'");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(piContext =>
            {
                string attributeSource = GetAttributeSource();
                piContext.AddSource(
                    "GenerateGrpcRemoteAttribute.g.cs",
                    SourceText.From(attributeSource, Encoding.UTF8)
                );
                string optionsSource = GetGrpcRemoteOptionsSource();
                piContext.AddSource(
                    "GrpcRemoteOptions.g.cs",
                    SourceText.From(optionsSource, Encoding.UTF8)
                );
            });
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GenerateGrpcRemoteAttributeFullName,
                    predicate: (node, _) => node is ClassDeclarationSyntax,
                    transform: (ctx, _) => (ClassDeclarationSyntax)ctx.TargetNode);

            IncrementalValueProvider<(Compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            // Combine with AnalyzerConfigOptionsProvider to pick up the GrpcServices setting
            var optionsProvider = context.AnalyzerConfigOptionsProvider;
            var fullInput = compilationAndClasses.Combine(optionsProvider);

            context.RegisterSourceOutput(fullInput, (spc, input) =>
            {
                var ((compilation, classes), configOptions) = input;
                Execute(compilation, classes, spc, configOptions);
            });
        }


        internal class PropertyInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }
        internal class CommandInfoData { public string MethodName { get; set; } = ""; public string CommandPropertyName { get; set; } = ""; public List<ParameterInfoData> Parameters { get; set; } = new List<ParameterInfoData>(); public bool IsAsync { get; set; } }
        internal class ParameterInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }
        // Add the following attribute to enable analyzer release tracking for the diagnostic descriptor SGWARN001.
        // This attribute is required to comply with RS2008.

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public class GrpcRemoteMvvmGeneratorAnalyzer : DiagnosticAnalyzer
        {

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
                ImmutableArray.Create(SGWARN001_AttributeNotFound);

            public override void Initialize(AnalysisContext context)
            {
                // Analyzer initialization logic (if needed)
            }
        }

        private void Execute(Compilation compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context, AnalyzerConfigOptionsProvider optionsProvider)
        {
            context.ReportDiagnostic(Diagnostic.Create(SGINFO001_GeneratorStarted, Location.None));

            if (classes.IsDefaultOrEmpty)
            {
                context.ReportDiagnostic(Diagnostic.Create(SGINFO002_NoClassesFound, Location.None));
                return;
            }

            // Read project-level <GrpcServices> (default to "Both")
            optionsProvider.GlobalOptions.TryGetValue("build_property.GrpcServices", out var rawGrpcServices);
            var grpcServices = string.IsNullOrWhiteSpace(rawGrpcServices)
                ? "Both"
                : rawGrpcServices?.Trim();

            // *** DEBUG DUMP ***
            context.ReportDiagnostic(Diagnostic.Create(
                SGINFO003_ProcessingClass,  // re-using an Info descriptor for simplicity
                Location.None,
                $"[DEBUG] rawGrpcServices = '{rawGrpcServices ?? "<null>"}'"));
            context.ReportDiagnostic(Diagnostic.Create(
                SGINFO003_ProcessingClass,
                Location.None,
                $"[DEBUG] grpcServices     = '{grpcServices}'"));
            // *** end dump ***

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

                context.ReportDiagnostic(Diagnostic.Create(SGINFO005_ExtractedMembers,Location.None,
                    properties.Count, commands.Count, originalViewModelName));

                // CONDITIONAL: generate server stub?
                if (grpcServices is not null && ( grpcServices.Equals("Server", StringComparison.OrdinalIgnoreCase) || grpcServices.Equals("Both", StringComparison.OrdinalIgnoreCase)))
                {
                    string serverImplCode = GenerateServerImplementation(
                        serverImplNamespace, originalViewModelName, originalViewModelFullName,
                        protoCsNamespace, grpcServiceNameFromAttribute,
                        properties, commands, compilation);
                    context.AddSource($"{originalViewModelName}GrpcServiceImpl.g.cs", SourceText.From(serverImplCode, Encoding.UTF8));
                    context.ReportDiagnostic(Diagnostic.Create(SGINFO006_GeneratedServerImpl, Location.None, originalViewModelName, serverImplNamespace));
                }

                // CONDITIONAL: generate client proxy?
                if (grpcServices is not null && (grpcServices.Equals("Client", StringComparison.OrdinalIgnoreCase) || grpcServices.Equals("Both", StringComparison.OrdinalIgnoreCase)))
                {
                    string clientProxyCode = GenerateClientProxyViewModel(
                        clientProxyNamespace, originalViewModelName,
                        protoCsNamespace, grpcServiceNameFromAttribute,
                        properties, commands, compilation);
                    context.AddSource($"{originalViewModelName}RemoteClient.g.cs", SourceText.From(clientProxyCode, Encoding.UTF8));
                    context.ReportDiagnostic(Diagnostic.Create(SGINFO007_GeneratedClientProxy, Location.None, originalViewModelName, clientProxyNamespace));
                }

                // Generate partial class for ViewModel with options-based constructor
                string viewModelCode = GenerateViewModelPartialClass(originalViewModelName, classSymbol.ContainingNamespace.ToDisplayString(), serverImplNamespace);
                context.AddSource($"{originalViewModelName}.GrpcRemoteOptions.g.cs", SourceText.From(viewModelCode, Encoding.UTF8));
                context.ReportDiagnostic(Diagnostic.Create(SGINFO007_GeneratedClientProxy, Location.None, originalViewModelName, $"{classSymbol.ContainingNamespace.ToDisplayString()} (GrpcRemoteOptions)"));
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
                        string baseMethodName = methodSymbol.Name;
                        if (baseMethodName.EndsWith("Async", StringComparison.Ordinal))
                        {
                            baseMethodName = baseMethodName.Substring(0, baseMethodName.Length - "Async".Length);
                        }
                        string commandPropertyName = baseMethodName + "Command";

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
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Grpc.Core;");
            sb.AppendLine($"using {protoCsNamespace}; // For gRPC base, messages");
            sb.AppendLine("using Google.Protobuf.WellKnownTypes; // For Empty, StringValue, Int32Value, Any etc.");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Concurrent;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Diagnostics;");
            sb.AppendLine("using System.Threading.Channels;");
            sb.AppendLine("using System.Windows.Threading; // For Dispatcher");
            sb.AppendLine();
            sb.AppendLine($"namespace {serverImplNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {vmName}GrpcServiceImpl : {protoCsNamespace}.{grpcServiceName}.{grpcServiceName}Base");
            sb.AppendLine("    {");
            // Add static event and property for client count
            sb.AppendLine("        public static event System.EventHandler<int>? ClientCountChanged;");
            sb.AppendLine("        private static int _clientCount = -1;");
            sb.AppendLine("        public static int ClientCount");
            sb.AppendLine("        {");
            sb.AppendLine("            get => _clientCount;");
            sb.AppendLine("            private set");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_clientCount != value)");
            sb.AppendLine("                {");
            sb.AppendLine("                    _clientCount = value;");
            sb.AppendLine("                    ClientCountChanged?.Invoke(null, value);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        static {vmName}GrpcServiceImpl()");
            sb.AppendLine("        {");
            sb.AppendLine("            ClientCount = 0;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private readonly {vmFullName} _viewModel;");
            sb.AppendLine($"        private readonly ConcurrentDictionary<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>, System.Threading.Channels.Channel<{protoCsNamespace}.PropertyChangeNotification>> _subscriberChannels = new ConcurrentDictionary<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>, System.Threading.Channels.Channel<{protoCsNamespace}.PropertyChangeNotification>>();");
            sb.AppendLine("        private readonly Dispatcher _dispatcher; // For UI thread marshalling");
            sb.AppendLine();
            sb.AppendLine($"        public {vmName}GrpcServiceImpl({vmFullName} viewModel, Dispatcher dispatcher)");
            sb.AppendLine("        {");
            sb.AppendLine("            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
            sb.AppendLine("            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));");
            sb.AppendLine("            if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<{protoCsNamespace}.{vmName}State> GetState(Empty request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.WriteLine(\"[" + vmName + "GrpcServiceImpl] GetState called.\");");
            sb.AppendLine($"            var state = new {protoCsNamespace}.{vmName}State();");
            foreach (var prop in props)
            {
                string csharpPropertyName = prop.Name;
                string protoMessageFieldName = ToPascalCase(prop.Name);
                sb.AppendLine($"            // Mapping property: {csharpPropertyName} to state.{protoMessageFieldName}");
                sb.AppendLine($"            try {{");
                sb.AppendLine($"                var propValue = _viewModel.{csharpPropertyName};");
                sb.AppendLine("                Debug.WriteLine(\"[GrpcService:" + vmName + "] GetState: Property '\" + \"" + csharpPropertyName + "\" + \"' has value '\" + propValue + \"'.\");");
                if (prop.FullTypeSymbol?.IsReferenceType == true || prop.FullTypeSymbol?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    sb.AppendLine($"                if (propValue != null) state.{protoMessageFieldName} = propValue;");
                }
                else
                {
                    sb.AppendLine($"                state.{protoMessageFieldName} = propValue;");
                }
                sb.AppendLine($"            }} catch (Exception ex) {{ Debug.WriteLine(\"[GrpcService:" + vmName + $"] Error mapping property {csharpPropertyName} to state.{protoMessageFieldName}: \" + ex.Message); }}");
            }
            sb.AppendLine("            return Task.FromResult(state);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override async Task SubscribeToPropertyChanges(Empty request, IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification> responseStream, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + "] Client subscribed to property changes.\");");
            sb.AppendLine($"            var channel = System.Threading.Channels.Channel.CreateUnbounded<{protoCsNamespace}.PropertyChangeNotification>(new UnboundedChannelOptions {{ SingleReader = true, SingleWriter = false }});");
            sb.AppendLine("            _subscriberChannels.TryAdd(responseStream, channel);");
            sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + "] Channel created and added for subscriber.\");");
            sb.AppendLine();
            sb.AppendLine("            ClientCount = _subscriberChannels.Count; // Update count on subscribe");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                await foreach (var notification in channel.Reader.ReadAllAsync(context.CancellationToken))");
            sb.AppendLine("                {");
            sb.AppendLine("                    Debug.WriteLine(\"[GrpcService:" + vmName + "] Sending notification for '\" + notification.PropertyName + \"' to a subscriber.\");");
            sb.AppendLine("                    await responseStream.WriteAsync(notification);");
            sb.AppendLine("                    Debug.WriteLine(\"[GrpcService:" + vmName + "] Successfully sent notification for '\" + notification.PropertyName + \"'.\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (OperationCanceledException) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Subscription cancelled by client or server shutdown.\"); }");
            sb.AppendLine("            catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error in subscriber sender task: \" + ex.ToString()); }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                _subscriberChannels.TryRemove(responseStream, out _);");
            sb.AppendLine("                channel.Writer.TryComplete();");
            sb.AppendLine("                ClientCount = _subscriberChannels.Count; // Update count on unsubscribe");
            sb.AppendLine("                Debug.WriteLine(\"[GrpcService:" + vmName + "] Client unsubscribed and channel completed.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<Empty> UpdatePropertyValue({protoCsNamespace}.UpdatePropertyValueRequest request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue called for '\" + request.PropertyName + \"'.\");");
            sb.AppendLine("            _dispatcher.Invoke(() => {");
            sb.AppendLine("                var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);");
            sb.AppendLine("                if (propertyInfo != null && propertyInfo.CanWrite)");
            sb.AppendLine("                {");
            sb.AppendLine("                    try {");
            sb.AppendLine($"                       if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);");
            sb.AppendLine($"                       else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);");
            sb.AppendLine($"                       else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);");
            sb.AppendLine("                        // TODO: Add more type checks and unpacking logic here (double, float, long, DateTime/Timestamp etc.)");
            sb.AppendLine("                        else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Unpacking not implemented for property \\\"\" + request.PropertyName + \"\\\" and type \\\"\" + request.NewValue.TypeUrl + \"\\\".\"); }");
            sb.AppendLine("                    } catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error setting property \\\"\" + request.PropertyName + \"\\\": \" + ex.Message); }");
            sb.AppendLine("                }");
            sb.AppendLine("                else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Property \\\"\" + request.PropertyName + \"\\\" not found or not writable.\"); }");
            sb.AppendLine("            });");
            sb.AppendLine("            return Task.FromResult(new Empty());");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        public override Task<ConnectionStatusResponse> Ping(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            return Task.FromResult(new ConnectionStatusResponse { Status = ConnectionStatus.Connected });");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                sb.AppendLine($"        public override async Task<{protoCsNamespace}.{cmd.MethodName}Response> {cmd.MethodName}({protoCsNamespace}.{cmd.MethodName}Request request, ServerCallContext context)");
                sb.AppendLine("        {");
                sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + $"] Executing command {cmd.MethodName}.\");");
                sb.AppendLine("            try {");
                sb.AppendLine("                await _dispatcher.InvokeAsync(async () => {");
                string commandPropertyAccess = $"_viewModel.{cmd.CommandPropertyName}";
                if (cmd.IsAsync)
                {
                    sb.AppendLine($"                    var command = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;");
                    sb.AppendLine($"                    if (command != null)");
                    sb.AppendLine("                    {");
                    if (cmd.Parameters.Count == 1 && cmd.Parameters[0].FullTypeSymbol != null)
                    {
                        sb.AppendLine($"                        var typedCommand = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<{cmd.Parameters[0].Type}>;");
                        sb.AppendLine($"                        if (typedCommand != null) await typedCommand.ExecuteAsync(request.{ToPascalCase(cmd.Parameters[0].Name)});");
                        sb.AppendLine($"                        else await command.ExecuteAsync(request);");
                    }
                    else if (cmd.Parameters.Count == 0)
                        sb.AppendLine("                        await command.ExecuteAsync(null);");
                    else
                        sb.AppendLine("                        await command.ExecuteAsync(request);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                    else { Debug.WriteLine(\"[GrpcService:" + vmName + $"] Command {cmd.CommandPropertyName} not found or not IAsyncRelayCommand.\"); }}");
                }
                else
                {
                    sb.AppendLine($"                    var command = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand;");
                    sb.AppendLine($"                    if (command != null)");
                    sb.AppendLine("                    {");
                    if (cmd.Parameters.Count == 1 && cmd.Parameters[0].FullTypeSymbol != null)
                    {
                        sb.AppendLine($"                       var typedCommand = {commandPropertyAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand<{cmd.Parameters[0].Type}>;");
                        sb.AppendLine($"                       if (typedCommand != null) typedCommand.Execute(request.{ToPascalCase(cmd.Parameters[0].Name)});");
                        sb.AppendLine($"                       else command.Execute(request);");
                    }
                    else if (cmd.Parameters.Count == 0)
                        sb.AppendLine("                        command.Execute(null);");
                    else
                        sb.AppendLine("                        command.Execute(request);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                    else { Debug.WriteLine(\"[GrpcService:" + vmName + $"] Command {cmd.CommandPropertyName} not found or not IRelayCommand.\"); }}");
                }
                sb.AppendLine("                });");
                sb.AppendLine("            } catch (Exception ex) {");
                sb.AppendLine("                Debug.WriteLine(\"[GrpcService:" + vmName + $"] Exception during command execution for {cmd.MethodName}: \" + ex.ToString());");
                sb.AppendLine("                throw new RpcException(new Status(StatusCode.Internal, \"Error executing command on server: \" + ex.Message));");
                sb.AppendLine("            }");
                sb.AppendLine($"            return new {protoCsNamespace}.{cmd.MethodName}Response();");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine($"        private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (string.IsNullOrEmpty(e.PropertyName)) return;");
            sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + "] ViewModel_PropertyChanged for '\" + e.PropertyName + \"'.\");");
            sb.AppendLine("            object? newValue = null;");
            sb.AppendLine($"            try {{ newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }}");
            sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine(\"[GrpcService:" + vmName + $"] Error getting property value for \\\"\" + e.PropertyName + \"\\\": \" + ex.Message); return; }}");
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
            // Corrected string interpolation for generated code
            sb.AppendLine($"            else {{ Debug.WriteLine($\"[GrpcService:" + vmName + $"] PropertyChanged: Packing not implemented for type {{(newValue?.GetType().FullName ?? \"null\")}} of property {{e.PropertyName}}.\"); notification.NewValue = Any.Pack(new StringValue {{ Value = newValue.ToString() }}); }}");
            sb.AppendLine();
            sb.AppendLine("            Debug.WriteLine(\"[GrpcService:" + vmName + "] Queuing notification for '\" + e.PropertyName + \"' to \" + _subscriberChannels.Count + \" subscribers.\");");
            sb.AppendLine("            foreach (var channelWriter in _subscriberChannels.Values.Select(c => c.Writer))");
            sb.AppendLine("            {");
            sb.AppendLine("                try { await channelWriter.WriteAsync(notification); }");
            sb.AppendLine("                catch (ChannelClosedException) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Channel closed for a subscriber, cannot write notification for '\" + e.PropertyName + \"'. Subscriber likely disconnected.\"); /* Handled by finally in SubscribeToPropertyChanges */ }");
            sb.AppendLine("                catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error writing to subscriber channel for '\" + e.PropertyName + \"': \" + ex.Message); }");
            sb.AppendLine("            }");
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
            sb.AppendLine("#nullable enable");
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
            sb.AppendLine("        private string _connectionStatus = \"Unknown\";");
            sb.AppendLine("        public string ConnectionStatus");
            sb.AppendLine("        {");
            sb.AppendLine("            get => _connectionStatus;");
            sb.AppendLine("            private set => SetProperty(ref _connectionStatus, value);");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var prop in props)
            {
                string backingFieldName = $"_{LowercaseFirst(prop.Name)}";
                sb.AppendLine($"        private {prop.Type} {backingFieldName} = default!;");
                sb.AppendLine($"        public {prop.Type} {prop.Name}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => {backingFieldName};");
                sb.AppendLine($"            private set => SetProperty(ref {backingFieldName}, value);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var cmd in cmds)
            {
                string commandInterfaceType = cmd.IsAsync ? "IAsyncRelayCommand" : "IRelayCommand";
                string methodGenericTypeArg = "";

                if (cmd.Parameters.Any())
                {
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

            sb.AppendLine("        private async Task StartPingLoopAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            string lastStatus = ConnectionStatus;");
            sb.AppendLine("            while (!_isDisposed)");
            sb.AppendLine("            {");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine("                    var response = await _grpcClient.PingAsync(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: _cts.Token);");
            sb.AppendLine("                    if (response.Status == MonsterClicker.ViewModels.Protos.ConnectionStatus.Connected)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (lastStatus != \"Connected\")");
            sb.AppendLine("                        {");
            sb.AppendLine("                            // Reconnected: fetch state and resubscribe");
            sb.AppendLine("                            try");
            sb.AppendLine("                            {");
            sb.AppendLine("                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);");
            foreach (var prop in props)
            {
                string protoStateFieldName = ToPascalCase(prop.Name);
                sb.AppendLine($"                                this.{prop.Name} = state.{protoStateFieldName};");
            }
            sb.AppendLine("                                Debug.WriteLine(\"[ClientProxy] State re-synced after reconnect.\");");
            sb.AppendLine("                                StartListeningToPropertyChanges(_cts.Token);");
            sb.AppendLine("                            }");
            sb.AppendLine("                            catch (Exception ex)");
            sb.AppendLine("                            {");
            sb.AppendLine("                                Debug.WriteLine($\"[ClientProxy] Error re-syncing state after reconnect: {ex.Message}\");");
            sb.AppendLine("                            }");
            sb.AppendLine("                        }");
            sb.AppendLine("                        ConnectionStatus = \"Connected\";");
            sb.AppendLine("                        lastStatus = \"Connected\";");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine("                        ConnectionStatus = \"Disconnected\";");
            sb.AppendLine("                        lastStatus = \"Disconnected\";");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ConnectionStatus = \"Disconnected\";");
            sb.AppendLine("                    lastStatus = \"Disconnected\";");
            sb.AppendLine("                    Debug.WriteLine($\"[ClientProxy] Ping failed: {ex.Message}. Attempting to reconnect...\");");
            sb.AppendLine("                }");
            sb.AppendLine("                await Task.Delay(5000);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public async Task InitializeRemoteAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_isInitialized || _isDisposed) return;");
            sb.AppendLine("            Debug.WriteLine(\"[" + originalVmName + "RemoteClient] Initializing...\");");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);");
            sb.AppendLine($"                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);");
            sb.AppendLine($"                Debug.WriteLine(\"[{originalVmName}RemoteClient] Initial state received.\");");
            foreach (var prop in props)
            {
                string protoStateFieldName = ToPascalCase(prop.Name);
                sb.AppendLine($"                this.{prop.Name} = state.{protoStateFieldName};");
            }
            sb.AppendLine("                _isInitialized = true;");
            sb.AppendLine($"                Debug.WriteLine(\"[{originalVmName}RemoteClient] Initialized successfully.\");");
            sb.AppendLine("                StartListeningToPropertyChanges(_cts.Token);");
            sb.AppendLine("                _ = StartPingLoopAsync();");
            sb.AppendLine("            }");
            sb.AppendLine($"            catch (RpcException ex) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Failed to initialize: \" + ex.Status.StatusCode + \" - \" + ex.Status.Detail); }}");
            sb.AppendLine($"            catch (OperationCanceledException) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Initialization cancelled.\"); }}");
            sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Unexpected error during initialization: \" + ex.Message); }}");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                string cmdMethodNameForLog = cmd.MethodName;
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
                string earlyExit = cmd.IsAsync ? "return;" : "return;";
                sb.AppendLine($"            if (!_isInitialized || _isDisposed) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Not initialized or disposed, command {cmdMethodNameForLog} skipped.\"); {earlyExit} }}");
                sb.AppendLine($"            Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Executing command {cmdMethodNameForLog} remotely...\");");
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
                sb.AppendLine($"            catch (RpcException ex) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Error executing command {cmdMethodNameForLog}: \" + ex.Status.StatusCode + \" - \" + ex.Status.Detail); }}");
                sb.AppendLine($"            catch (OperationCanceledException) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Command {cmdMethodNameForLog} cancelled.\"); }}");
                sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine(\"[ClientProxy:" + originalVmName + $"] Unexpected error executing command {cmdMethodNameForLog}: \" + ex.Message); }}");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine("            _ = Task.Run(async () => ");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_isDisposed) return;");
            sb.AppendLine("                Debug.WriteLine(\"[" + originalVmName + "RemoteClient] Starting property change listener...\");");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    using var call = _grpcClient.SubscribeToPropertyChanges(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("                    Debug.WriteLine(\"[" + originalVmName + "RemoteClient] Subscribed to property changes. Waiting for updates...\");");
            sb.AppendLine("                    int updateCount = 0;");
            sb.AppendLine("                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        updateCount++;");
            sb.AppendLine("                        if (_isDisposed) { Debug.WriteLine(\"[" + originalVmName + "RemoteClient] Disposed during update \" + updateCount + \", exiting property update loop.\"); break; }");
            sb.AppendLine("                        Debug.WriteLine($\"[" + originalVmName + "RemoteClient] RAW UPDATE #\" + updateCount + \" RECEIVED: PropertyName=\\\"\" + update.PropertyName + \"\\\", ValueTypeUrl=\\\"\" + (update.NewValue?.TypeUrl ?? \"null_type_url\") + \"\\\"\");");
            sb.AppendLine("                        Action updateAction = () => {");
            sb.AppendLine("                           try {");
            sb.AppendLine("                               Debug.WriteLine(\"[" + originalVmName + "RemoteClient] Dispatcher: Attempting to update \\\"\" + update.PropertyName + \"\\\" (Update #\" + updateCount + \").\");");
            sb.AppendLine("                               switch (update.PropertyName)");
            sb.AppendLine("                               {");
            foreach (var prop in props)
            {
                string wkt = GetProtoWellKnownTypeFor(prop.FullTypeSymbol!, compilation);
                string csharpPropName = prop.Name;
                sb.AppendLine($"                                   case nameof({csharpPropName}):");
                if (wkt == "StringValue") sb.AppendLine($"                                       if (update.NewValue!.Is(StringValue.Descriptor)) {{ var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($\"Updating {csharpPropName} from \\\"{{this.{csharpPropName}}}\\\" to '\\\"{{val}}\\\".\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is '\\\"{{this.{csharpPropName}}}\\\".\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected StringValue.\"); }} break;");
                else if (wkt == "Int32Value") sb.AppendLine($"                                       if (update.NewValue!.Is(Int32Value.Descriptor)) {{ var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($\"Updating {csharpPropName} from {{this.{csharpPropName}}} to {{val}}.\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is {{this.{csharpPropName}}}.\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected Int32Value.\"); }} break;");
                else if (wkt == "BoolValue") sb.AppendLine($"                                       if (update.NewValue!.Is(BoolValue.Descriptor)) {{ var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($\"Updating {csharpPropName} from {{this.{csharpPropName}}} to {{val}}.\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is {{this.{csharpPropName}}}.\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected BoolValue.\"); }} break;");
                else sb.AppendLine($"                                       Debug.WriteLine($\"[ClientProxy:" + originalVmName + $"] Unpacking for {prop.Name} with WKT {wkt} not fully implemented or is Any.\"); break;");
            }
            sb.AppendLine($"                                   default: Debug.WriteLine($\"[ClientProxy:" + originalVmName + $"] Unknown property in notification: \\\"{{update.PropertyName}}\\\"\"); break;");
            sb.AppendLine("                               }");
            sb.AppendLine("                           } catch (Exception exInAction) { Debug.WriteLine($\"[ClientProxy:" + originalVmName + $"] EXCEPTION INSIDE updateAction for \\\"{{update.PropertyName}}\\\": \" + exInAction.ToString()); }}");
            sb.AppendLine("                        };");
            sb.AppendLine("                        #if WPF_DISPATCHER");
            sb.AppendLine("                        Application.Current?.Dispatcher.Invoke(updateAction);");
            sb.AppendLine("                        #else");
            sb.AppendLine("                        updateAction();");
            sb.AppendLine("                        #endif");
            sb.AppendLine($"                        Debug.WriteLine(\"[{originalVmName}RemoteClient] Processed update #\" + updateCount + \" for \\\"\" + update.PropertyName + \"\\\". Still listening...\");");
            sb.AppendLine("                    }");
            sb.AppendLine("                    Debug.WriteLine(\"[" + originalVmName + "RemoteClient] ReadAllAsync completed or cancelled after \" + updateCount + \" updates.\");");
            sb.AppendLine("                }");
            sb.AppendLine($"                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) {{ Debug.WriteLine(\"[ClientProxy:{originalVmName}] Property subscription RpcException Cancelled.\"); }}");
            sb.AppendLine($"                catch (OperationCanceledException) {{ Debug.WriteLine($\"[ClientProxy:{originalVmName}] Property subscription OperationCanceledException.\"); }}");
            sb.AppendLine($"                catch (Exception ex) {{ if (!_isDisposed) Debug.WriteLine($\"[ClientProxy:{originalVmName}] Error in property listener: \" + ex.GetType().Name + \" - \" + ex.Message + \"\\nStackTrace: \" + ex.StackTrace); }}");
            sb.AppendLine($"                Debug.WriteLine(\"[{originalVmName}RemoteClient] Property change listener task finished.\");");
            sb.AppendLine("            }, cancellationToken);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_isDisposed) return;");
            sb.AppendLine("            _isDisposed = true;");
            sb.AppendLine($"            Debug.WriteLine(\"[{originalVmName}RemoteClient] Disposing...\");");
            sb.AppendLine("            _cts.Cancel();");
            sb.AppendLine("            _cts.Dispose();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateViewModelPartialClass(string viewModelName, string viewModelNamespace, string serverImplNamespace)
        {
            var sb = new StringBuilder();
            // Only emit ServerOptions/ClientOptions if they are not already defined (avoid duplicate definitions)
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            // Only add usings to the generated file, not to the generator itself
            sb.AppendLine("using Microsoft.Extensions.Hosting;");
            sb.AppendLine("using Microsoft.AspNetCore.Hosting;");
            sb.AppendLine("using Microsoft.AspNetCore.Builder;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Grpc.AspNetCore.Web;");
            sb.AppendLine("using Microsoft.AspNetCore.Server.Kestrel.Core;");
            sb.AppendLine();
            sb.AppendLine($"namespace {viewModelNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {viewModelName}");
            sb.AppendLine("    {");
            sb.AppendLine("        public object? GrpcHostOrClient { get; private set; }");
            sb.AppendLine($"        public {viewModelName}(object? options = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (options is PeakSWC.Mvvm.Remote.ServerOptions serverOptions)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Server mode: start gRPC server with options");
            sb.AppendLine("                var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();");
            sb.AppendLine("                builder.ConfigureWebHostDefaults(webBuilder =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    webBuilder.UseKestrel(kestrelOptions =>");
            sb.AppendLine("                    {");
            sb.AppendLine("                        kestrelOptions.ListenLocalhost(serverOptions.Port, listenOptions =>");
            sb.AppendLine("                        {");
            sb.AppendLine("                            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;");
            sb.AppendLine("                            if (serverOptions.UseHttps)");
            sb.AppendLine("                                listenOptions.UseHttps();");
            sb.AppendLine("                        });");
            sb.AppendLine("                    });");
            sb.AppendLine("                    webBuilder.ConfigureServices(services =>");
            sb.AppendLine("                    {");
            sb.AppendLine("                        services.AddSingleton(this);");
            sb.AppendLine("                        services.AddGrpc(options => { options.EnableDetailedErrors = true; });");
            sb.AppendLine("                        services.AddCors(corsOptions =>");
            sb.AppendLine("                        {");
            sb.AppendLine("                            corsOptions.AddPolicy(serverOptions.CorsPolicyName ?? \"AllowAll\", policy =>");
            sb.AppendLine("                            {");
            sb.AppendLine("                                if (serverOptions.AllowedOrigins != null) policy.WithOrigins(serverOptions.AllowedOrigins);");
            sb.AppendLine("                                else policy.AllowAnyOrigin();");
            sb.AppendLine("                                if (serverOptions.AllowedMethods != null) policy.WithMethods(serverOptions.AllowedMethods);");
            sb.AppendLine("                                else policy.AllowAnyMethod();");
            sb.AppendLine("                                if (serverOptions.AllowedHeaders != null) policy.WithHeaders(serverOptions.AllowedHeaders);");
            sb.AppendLine("                                else policy.AllowAnyHeader();");
            sb.AppendLine("                                if (serverOptions.ExposedHeaders != null) policy.WithExposedHeaders(serverOptions.ExposedHeaders);");
            sb.AppendLine("                                else policy.WithExposedHeaders(\"Grpc-Status\", \"Grpc-Message\", \"Grpc-Encoding\", \"Grpc-Accept-Encoding\");");
            sb.AppendLine("                            });");
            sb.AppendLine("                        });");
            sb.AppendLine("                        services.AddLogging(logging =>");
            sb.AppendLine("                        {");
            sb.AppendLine("                            logging.AddConsole();");
            sb.AppendLine("                            logging.SetMinimumLevel(Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(serverOptions.LogLevel, out var lvl) ? lvl : Microsoft.Extensions.Logging.LogLevel.Debug);");
            sb.AppendLine("                        });");
            sb.AppendLine("                    });");
            sb.AppendLine("                    webBuilder.Configure((ctx, app) =>");
            sb.AppendLine("                    {");
            sb.AppendLine("                        app.UseRouting();");
            sb.AppendLine("                        app.UseCors(serverOptions.CorsPolicyName ?? \"AllowAll\");");
            sb.AppendLine("                        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });");
            sb.AppendLine("                        app.UseEndpoints(endpoints =>");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            endpoints.MapGrpcService<{serverImplNamespace}.{viewModelName}GrpcServiceImpl>().EnableGrpcWeb().RequireCors(serverOptions.CorsPolicyName ?? \"AllowAll\");");
            sb.AppendLine("                        });");
            sb.AppendLine("                    });");
            sb.AppendLine("                });");
            sb.AppendLine("                var host = builder.Build();");
            sb.AppendLine("                host.StartAsync();");
            sb.AppendLine("                GrpcHostOrClient = host;");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (options is PeakSWC.Mvvm.Remote.ClientOptions clientOptions)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Client mode: connect to gRPC server");
            sb.AppendLine("                var channel = Grpc.Net.Client.GrpcChannel.ForAddress(clientOptions.Address);");
            sb.AppendLine($"                var grpcClient = new {viewModelNamespace}.Protos.{viewModelName}Service.{viewModelName}ServiceClient(channel);");
            sb.AppendLine("                GrpcHostOrClient = grpcClient;");
            sb.AppendLine("                // Optionally: initialize remote client logic here");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                // Local mode");
            sb.AppendLine("                ResetGame();");
            sb.AppendLine("            }");
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
            return Regex.Replace(pascalCaseName, @"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z])(?=[a-z])", "_$1$2").ToLower();
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
