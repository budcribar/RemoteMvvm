using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions; // Only used for ToSnakeCase, not directly in C# generation

namespace PeakSWC.MvvmSourceGenerator
{
    [Generator]
    public class GrpcRemoteMvvmGenerator : IIncrementalGenerator
    {
        private const string GenerateGrpcRemoteAttributeFullName = "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute";
        private const string ObservablePropertyAttributeFullName = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
        private const string RelayCommandAttributeFullName = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find classes annotated with [GenerateGrpcRemote]
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

        // Helper record/class for storing extracted info (ensure these are accessible or defined within)
        // Using classes as per your provided code.
        internal class PropertyInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }
        internal class CommandInfoData { public string MethodName { get; set; } = ""; public string CommandPropertyName { get; set; } = ""; public List<ParameterInfoData> Parameters { get; set; } = new List<ParameterInfoData>(); public bool IsAsync { get; set; } }
        internal class ParameterInfoData { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public ITypeSymbol? FullTypeSymbol { get; set; } }


        private void Execute(Compilation compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var classSyntax in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

                if (semanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                var attributeData = classSymbol.GetAttributes().FirstOrDefault(ad =>
                    ad.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == GenerateGrpcRemoteAttributeFullName ||
                    ad.AttributeClass?.Name == GenerateGrpcRemoteAttributeFullName); // Added short name check for flexibility

                if (attributeData == null) continue;

                // Extract attribute arguments for namespaces
                string protoCsNamespace = string.Empty; // This is for the .proto's csharp_namespace, used by Grpc.Tools
                string grpcServiceNameFromAttribute = string.Empty; // The service name defined in the .proto

                if (attributeData.ConstructorArguments.Length >= 2)
                {
                    protoCsNamespace = attributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                    grpcServiceNameFromAttribute = attributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                }

                // These are the C# namespaces for the generated implementation and proxy
                string serverImplNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ServerImplNamespace").Value.Value?.ToString()
                                             ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.GrpcServices";
                string clientProxyNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ClientProxyNamespace").Value.Value?.ToString()
                                               ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.RemoteClients";

                var originalViewModelName = classSymbol.Name;
                var originalViewModelFullName = classSymbol.ToDisplayString();

                List<PropertyInfoData> properties = GetObservableProperties(classSymbol);
                List<CommandInfoData> commands = GetRelayCommands(classSymbol);

                // 1. Generate Server-Side gRPC Service Implementation
                string serverImplCode = GenerateServerImplementation(
                    serverImplNamespace,
                    originalViewModelName,
                    originalViewModelFullName,
                    protoCsNamespace, // Pass the proto C# namespace
                    grpcServiceNameFromAttribute, // Pass the proto service name
                    properties,
                    commands,
                    compilation); // Pass compilation for GetProtoWellKnownTypeFor
                context.AddSource($"{originalViewModelName}GrpcService.g.cs", SourceText.From(serverImplCode, Encoding.UTF8));

                // 2. Generate Client-Side Proxy ViewModel
                string clientProxyCode = GenerateClientProxyViewModel(
                    clientProxyNamespace,
                    originalViewModelName,
                    protoCsNamespace, // Pass the proto C# namespace
                    grpcServiceNameFromAttribute, // Pass the proto service name
                    properties,
                    commands,
                    compilation); // Pass compilation for GetProtoWellKnownTypeFor
                context.AddSource($"{originalViewModelName}RemoteClient.g.cs", SourceText.From(clientProxyCode, Encoding.UTF8));
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

                        // In a source generator, the generated property might not exist in *this* compilation pass's symbols.
                        // We infer based on the field.
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
                        // We infer the command property name and details from the method.
                        cmds.Add(new CommandInfoData
                        {
                            MethodName = methodSymbol.Name,
                            CommandPropertyName = commandPropertyName,
                            Parameters = methodSymbol.Parameters.Select(p => new ParameterInfoData { Name = p.Name, Type = p.Type.ToDisplayString(), FullTypeSymbol = p.Type }).ToList(),
                            IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.ConstructedFrom?.Name == "Task" && rtSym.ConstructedFrom.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")))
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
            sb.AppendLine($"using {protoCsNamespace}; // For gRPC base, messages, and well-known types if not fully qualified below");
            sb.AppendLine("using Google.Protobuf.WellKnownTypes; // For Empty, StringValue, Int32Value, Any etc.");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {serverImplNamespace}"); // Use the C# namespace for this generated server class
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
                // This requires mapping from C# property to proto state field.
                // Assumes direct assignment or simple conversion.
                // Example: state.{ToSnakeCase(prop.Name)} = _viewModel.{prop.Name}; (if proto uses snake_case and C# uses PascalCase)
                // For now, this part needs careful implementation based on how .proto fields are named and typed.
                // We'll assume for now that the .proto file (generated by ProtoGeneratorUtil) has fields
                // that can be mapped from the _viewModel.
                // This part is complex due to type conversion and naming conventions.
                sb.AppendLine($"            // TODO: Map _viewModel.{prop.Name} (type {prop.Type}) to state.{ToSnakeCase(prop.Name)}");
                sb.AppendLine($"            // Example: state.{ToSnakeCase(prop.Name)} = _viewModel.{prop.Name}; // if types are directly compatible");
                sb.AppendLine($"            // Or use a helper: state.Set{prop.Name}(_viewModel.{prop.Name});");
            }
            sb.AppendLine("            return Task.FromResult(state);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override async Task SubscribeToPropertyChanges(Empty request, IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification> responseStream, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            lock(_subscriberLock) { _subscribers.Add(responseStream); }");
            sb.AppendLine("            try { await context.CancellationToken.WhenCancelled(); }");
            sb.AppendLine("            catch (OperationCanceledException) { /* Expected on client disconnect */ }");
            sb.AppendLine("            catch (Exception ex) { Console.WriteLine($\"Error in SubscribeToPropertyChanges: {ex.Message}\"); }");
            sb.AppendLine("            finally { lock(_subscriberLock) { _subscribers.Remove(responseStream); } }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override Task<Empty> UpdatePropertyValue({protoCsNamespace}.UpdatePropertyValueRequest request, ServerCallContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            // This requires robust type unpacking and setting strategy based on request.PropertyName and request.NewValue
            // Example:
            // switch (request.PropertyName)
            // {
            // case "MonsterName": // Assuming property name matches
            // if (request.NewValue.Is(StringValue.Descriptor))
            // _viewModel.MonsterName = request.NewValue.Unpack<StringValue>().Value;
            // break;
            // // ... other properties
            // }");
            sb.AppendLine("            return Task.FromResult(new Empty());");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                sb.AppendLine($"        public override async Task<{protoCsNamespace}.{cmd.MethodName}Response> {cmd.MethodName}({protoCsNamespace}.{cmd.MethodName}Request request, ServerCallContext context)");
                sb.AppendLine("        {");
                // Parameter mapping from request to _viewModel command execution
                string argsList = string.Join(", ", cmd.Parameters.Select(p => $"request.{ToPascalCase(p.Name)}")); // Assuming request fields are PascalCase

                sb.AppendLine($"            // TODO: Map parameters from 'request' to the actual command execution on _viewModel.");
                if (cmd.IsAsync)
                {
                    sb.AppendLine($"            // Example: await _viewModel.{cmd.CommandPropertyName}.ExecuteAsync({(cmd.Parameters.Any() ? "mappedArgs" : "null")});");
                }
                else
                {
                    sb.AppendLine($"            // Example: _viewModel.{cmd.CommandPropertyName}.Execute({(cmd.Parameters.Any() ? "mappedArgs" : "null")});");
                }
                sb.AppendLine($"            return new {protoCsNamespace}.{cmd.MethodName}Response();");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine($"        private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (e.PropertyName == null) return;");
            sb.AppendLine("            object? newValue = null; try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); } catch { /* best effort */ }");
            sb.AppendLine($"            var notification = new {protoCsNamespace}.PropertyChangeNotification {{ PropertyName = e.PropertyName }};");
            sb.AppendLine("            // TODO: Pack newValue into notification.NewValue (e.g., using Any or specific types based on GetProtoWellKnownTypeFor)");
            sb.AppendLine("            // Example: if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });");
            sb.AppendLine();
            sb.AppendLine($"            List<IServerStreamWriter<{protoCsNamespace}.PropertyChangeNotification>> currentSubscribers;");
            sb.AppendLine("            lock(_subscriberLock) { currentSubscribers = _subscribers.ToList(); }");
            sb.AppendLine();
            sb.AppendLine("            foreach (var sub in currentSubscribers)");
            sb.AppendLine("            {");
            sb.AppendLine("                try { await sub.WriteAsync(notification); }");
            sb.AppendLine("                catch { lock(_subscriberLock) { _subscribers.Remove(sub); } }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateClientProxyViewModel(
            string clientProxyNamespace, string originalVmName,
            string protoCsNamespace, string grpcServiceName,
            List<PropertyInfoData> props, List<CommandInfoData> cmds, Compilation compilation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated by GrpcRemoteMvvmGenerator at {DateTime.Now}");
            sb.AppendLine($"// Client Proxy ViewModel for {originalVmName}");
            sb.AppendLine();
            sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
            sb.AppendLine("using CommunityToolkit.Mvvm.Input;");
            sb.AppendLine("using Grpc.Core;");
            sb.AppendLine("using Grpc.Net.Client; // Often used for channel creation");
            sb.AppendLine($"using {protoCsNamespace}; // For gRPC client, messages, and WKT if not fully qualified");
            sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic; // For List if used");
            // Potentially add System.Windows if Dispatcher is needed for UI updates, though ideally avoided
            // sb.AppendLine("using System.Windows; // For Application.Current.Dispatcher if updating UI directly");


            sb.AppendLine();
            sb.AppendLine($"namespace {clientProxyNamespace}"); // Use the C# namespace for this generated client class
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {originalVmName}RemoteClient : ObservableObject, IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {protoCsNamespace}.{grpcServiceName}.{grpcServiceName}Client _grpcClient;");
            sb.AppendLine("        private CancellationTokenSource _cts = new CancellationTokenSource();");
            sb.AppendLine("        private bool _isInitialized = false;");
            sb.AppendLine();

            foreach (var prop in props)
            {
                sb.AppendLine($"        private {prop.Type} _{LowercaseFirst(prop.Name)};");
                sb.AppendLine($"        public {prop.Type} {prop.Name}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => _{LowercaseFirst(prop.Name)};");
                sb.AppendLine("            private set // Properties are typically set by server updates");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (SetProperty(ref _{LowercaseFirst(prop.Name)}, value))");
                sb.AppendLine("                {");
                sb.AppendLine("                    // Optionally, if two-way binding is desired and implemented:");
                sb.AppendLine($"                    // if (_isInitialized) Task.Run(async () => await UpdateServerPropertyAsync(nameof({prop.Name}), value));");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var cmd in cmds)
            {
                string commandInterfaceType = cmd.IsAsync ? "IAsyncRelayCommand" : "IRelayCommand";
                if (!cmd.IsAsync && cmd.Parameters.Any())
                {
                    // Assuming single parameter for simplicity, similar to CommunityToolkit.Mvvm
                    commandInterfaceType = $"IRelayCommand<{cmd.Parameters[0].Type}>";
                }
                else if (cmd.IsAsync && cmd.Parameters.Any())
                {
                    commandInterfaceType = $"IAsyncRelayCommand<{cmd.Parameters[0].Type}>"; // If CTK.Mvvm supported this directly, else object
                }


                sb.AppendLine($"        public {commandInterfaceType} {cmd.CommandPropertyName} {{ get; }}");
            }
            sb.AppendLine();

            sb.AppendLine($"        public {originalVmName}RemoteClient({protoCsNamespace}.{grpcServiceName}.{grpcServiceName}Client grpcClient)");
            sb.AppendLine("        {");
            sb.AppendLine("            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));");
            foreach (var cmd in cmds)
            {
                string remoteExecuteMethodName = $"RemoteExecute{cmd.MethodName}";
                if (cmd.IsAsync)
                {
                    if (cmd.Parameters.Any())
                    {
                        // Assuming single parameter for AsyncRelayCommand<T>
                        // This requires AsyncRelayCommand<T> to exist or for the method to take object
                        sb.AppendLine($"            {cmd.CommandPropertyName} = new AsyncRelayCommand<{cmd.Parameters[0].Type}>({remoteExecuteMethodName}Async); // Adjust if AsyncRelayCommand<T> not available");
                    }
                    else
                    {
                        sb.AppendLine($"            {cmd.CommandPropertyName} = new AsyncRelayCommand({remoteExecuteMethodName}Async);");
                    }
                }
                else
                {
                    if (cmd.Parameters.Any())
                    {
                        sb.AppendLine($"            {cmd.CommandPropertyName} = new RelayCommand<{cmd.Parameters[0].Type}>({remoteExecuteMethodName});");
                    }
                    else
                    {
                        sb.AppendLine($"            {cmd.CommandPropertyName} = new RelayCommand({remoteExecuteMethodName});");
                    }
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public async Task InitializeRemoteAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_isInitialized) return;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("                // Unpack state and set initial local properties WITHOUT triggering server updates (use SetProperty directly)");
            foreach (var prop in props)
            {
                // This requires mapping from proto state field to C# property.
                // Assumes direct assignment or simple conversion.
                sb.AppendLine($"                // TODO: Map state.{ToSnakeCase(prop.Name)} to this.{prop.Name}");
                sb.AppendLine($"                // Example: this.{prop.Name} = state.{ToPascalCase(ToSnakeCase(prop.Name))}; // if proto uses snake_case");
            }
            sb.AppendLine("                _isInitialized = true;");
            sb.AppendLine("                StartListeningToPropertyChanges(_cts.Token);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (RpcException ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Console.WriteLine($\"Failed to initialize remote client: {ex.Status}\");");
            sb.AppendLine("                // Handle error appropriately, e.g., set an error message property");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Generate RemoteExecute<CommandName> methods
            foreach (var cmd in cmds)
            {
                string paramListWithType = string.Join(", ", cmd.Parameters.Select(p => $"{p.Type} {LowercaseFirst(p.Name)}"));
                string paramListNames = string.Join(", ", cmd.Parameters.Select(p => LowercaseFirst(p.Name)));
                string requestCreation = $"new {protoCsNamespace}.{cmd.MethodName}Request()"; // Default for no params

                if (cmd.Parameters.Any())
                {
                    var paramAssignments = cmd.Parameters.Select(p => $"{ToPascalCase(p.Name)} = {LowercaseFirst(p.Name)}"); // Assuming proto fields are PascalCase
                    requestCreation = $"new {protoCsNamespace}.{cmd.MethodName}Request {{ {string.Join(", ", paramAssignments)} }}";
                }

                if (cmd.IsAsync)
                {
                    sb.AppendLine($"        private async Task RemoteExecute{cmd.MethodName}Async({paramListWithType})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            if (!_isInitialized) {{ Console.WriteLine(\"Client not initialized for command {cmd.MethodName}.\"); return; }}");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                await _grpcClient.{cmd.MethodName}Async({requestCreation});");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (RpcException ex) { Console.WriteLine($\"Error executing command {cmd.MethodName}: {ex.Status}\"); }");
                    sb.AppendLine("        }");
                }
                else
                {
                    sb.AppendLine($"        private void RemoteExecute{cmd.MethodName}({paramListWithType})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            if (!_isInitialized) {{ Console.WriteLine(\"Client not initialized for command {cmd.MethodName}.\"); return; }}");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                _ = _grpcClient.{cmd.MethodName}Async({requestCreation}); // Fire and forget or handle response");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (RpcException ex) { Console.WriteLine($\"Error executing command {cmd.MethodName}: {ex.Status}\"); }");
                    sb.AppendLine("        }");
                }
                sb.AppendLine();
            }

            sb.AppendLine("        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine("            Task.Run(async () =>");
            sb.AppendLine("            {");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    using var call = _grpcClient.SubscribeToPropertyChanges(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        // Dispatch to UI thread if necessary before updating properties");
            sb.AppendLine("                        // Example for WPF: Application.Current.Dispatcher.Invoke(() => { /* update logic */ });");
            sb.AppendLine("                        // TODO: Implement robust type unpacking from PropertyChangeNotification and update corresponding client property");
            sb.AppendLine("                        /* Example: ");
            sb.AppendLine("                        switch (update.PropertyName)");
            sb.AppendLine("                        {");
            foreach (var prop in props)
            {
                sb.AppendLine($"                            case nameof({prop.Name}):");
                sb.AppendLine($"                                // this.{prop.Name} = update.NewValue.Unpack<{GetProtoWellKnownTypeFor(prop.FullTypeSymbol, compilation)}>().Value; // Requires GetProtoWellKnownTypeFor");
                sb.AppendLine("                                break;");
            }
            sb.AppendLine("                        }");
            sb.AppendLine("                        */");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)");
            sb.AppendLine("                { Console.WriteLine(\"Property subscription cancelled.\"); }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                { Console.WriteLine($\"Error in property listener: {ex.Message}\"); }");
            sb.AppendLine("            }, cancellationToken);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            _cts.Cancel();");
            sb.AppendLine("            _cts.Dispose();");
            sb.AppendLine("            // TODO: Dispose GrpcChannel if created and managed by this client proxy");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string LowercaseFirst(string str) => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str.Substring(1);
        private string ToPascalCase(string str) => string.IsNullOrEmpty(str) ? str : char.ToUpperInvariant(str[0]) + str.Substring(1);

        // GetProtoWellKnownTypeFor would be needed here if doing specific unpacking in StartListeningToPropertyChanges
        // For simplicity, it's assumed the server sends Any and the client has a strategy or uses Any directly.
        // If you need it here, you'd copy it from your ProtoGeneratorUtil or a shared library.
        private string GetProtoWellKnownTypeFor(ITypeSymbol typeSymbol, Compilation compilation)
        {
            // Simplified version or call to shared logic
            if (typeSymbol.SpecialType == SpecialType.System_String) return "StringValue";
            if (typeSymbol.SpecialType == SpecialType.System_Int32) return "Int32Value";
            if (typeSymbol.SpecialType == SpecialType.System_Boolean) return "BoolValue";
            // ... add more mappings as needed from your ProtoGeneratorUtil
            return "Any"; // Default
        }
    }
}
