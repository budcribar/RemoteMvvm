using System;
using System.Collections.Generic;
using System.Reflection; // Not directly used in this snippet but present in original
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace PeakSWC.MvvmSourceGenerator
{
    [Generator]
    public class GrpcRemoteMvvmGenerator : IIncrementalGenerator
    {
        private const string GenerateGrpcRemoteAttributeFullName = "PeakSWC.Mvvm.Remote.Mvvm.GenerateGrpcRemoteAttribute";
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

        private void Execute(Compilation compilation, System.Collections.Immutable.ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var classSyntax in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

                // GetDeclaredSymbol returns ISymbol?. For a ClassDeclarationSyntax, 
                // this should be an INamedTypeSymbol if it's not null.
                // Use 'is' pattern matching to check the type and cast.
                if (semanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
                {
                    // If the symbol is null or not an INamedTypeSymbol, skip this class.
                    continue;
                }
                // From this point onwards, 'classSymbol' is guaranteed to be an INamedTypeSymbol and not null.

                // Get attribute data
                var attributeData = classSymbol.GetAttributes().FirstOrDefault(ad =>
                    ad.AttributeClass?.ToDisplayString() == GenerateGrpcRemoteAttributeFullName);

                if (attributeData == null) continue;

                string protoNamespace = attributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                string grpcServiceName = attributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                string serverImplNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ServerImplNamespace").Value.Value?.ToString()
                                             ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.GrpcService";
                string clientProxyNamespace = attributeData.NamedArguments.FirstOrDefault(na => na.Key == "ClientProxyNamespace").Value.Value?.ToString()
                                               ?? $"{classSymbol.ContainingNamespace.ToDisplayString()}.RemoteClient";

                var originalViewModelName = classSymbol.Name;
                var originalViewModelFullName = classSymbol.ToDisplayString();

                // Now, classSymbol is correctly typed as INamedTypeSymbol for these calls.
                List<PropertyInfo> properties = GetObservableProperties(classSymbol);
                List<CommandInfo> commands = GetRelayCommands(classSymbol);

                // 1. Generate Server-Side gRPC Service Implementation
                string serverImplCode = GenerateServerImplementation(
                    serverImplNamespace,
                    originalViewModelName,
                    originalViewModelFullName,
                    protoNamespace,
                    grpcServiceName,
                    properties,
                    commands);
                context.AddSource($"{originalViewModelName}GrpcService.g.cs", SourceText.From(serverImplCode, Encoding.UTF8));

                // 2. Generate Client-Side Proxy ViewModel
                string clientProxyCode = GenerateClientProxyViewModel(
                    clientProxyNamespace,
                    originalViewModelName,
                    protoNamespace,
                    grpcServiceName,
                    properties,
                    commands);
                context.AddSource($"{originalViewModelName}RemoteClient.g.cs", SourceText.From(clientProxyCode, Encoding.UTF8));
            }
        }

        private List<PropertyInfo> GetObservableProperties(INamedTypeSymbol classSymbol)
        {
            var props = new List<PropertyInfo>();
            // Iterate through members, find fields annotated with [ObservableProperty]
            // The CommunityToolkit.Mvvm generates properties from these fields.
            // We need to find the generated *property* symbol.
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol)
                {
                    var obsPropAttribute = fieldSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ObservablePropertyAttributeFullName);
                    if (obsPropAttribute != null)
                    {
                        // The generated property name is derived from the field name (e.g., _userName -> UserName)
                        string propertyName = fieldSymbol.Name.TrimStart('_');
                        if (propertyName.Length == 0 && fieldSymbol.Name.Length > 0) // Handle case like "_"
                        {
                            continue;
                        }
                        if (propertyName.Length > 0)
                        {
                            propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
                        }
                        else // field was likely just "_" or empty after trim, invalid for property
                        {
                            continue;
                        }


                        // Find the actual generated property symbol
                        var actualPropertySymbol = classSymbol.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
                        if (actualPropertySymbol != null)
                        {
                            props.Add(new PropertyInfo { Name = actualPropertySymbol.Name, Type = actualPropertySymbol.Type.ToDisplayString(), FullTypeSymbol = actualPropertySymbol.Type });
                        }
                    }
                }
            }
            return props;
        }
        private List<CommandInfo> GetRelayCommands(INamedTypeSymbol classSymbol)
        {
            var cmds = new List<CommandInfo>();
            // Iterate through members, find methods annotated with [RelayCommand]
            // The CommunityToolkit.Mvvm generates command properties from these methods.
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol)
                {
                    var relayCmdAttribute = methodSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == RelayCommandAttributeFullName);
                    if (relayCmdAttribute != null)
                    {
                        // The generated command property name is method name + "Command" (e.g., Increment -> IncrementCommand)
                        string commandPropertyName = methodSymbol.Name + "Command";
                        var actualCommandPropertySymbol = classSymbol.GetMembers(commandPropertyName).OfType<IPropertySymbol>().FirstOrDefault();

                        if (actualCommandPropertySymbol != null)
                        {
                            cmds.Add(new CommandInfo
                            {
                                MethodName = methodSymbol.Name,
                                CommandPropertyName = commandPropertyName,
                                Parameters = methodSymbol.Parameters.Select(p => new ParameterInfo { Name = p.Name, Type = p.Type.ToDisplayString() }).ToList(),
                                IsAsync = methodSymbol.IsAsync || (methodSymbol.ReturnType is INamedTypeSymbol rtSym && (rtSym.Name == "Task" || (rtSym.IsGenericType && rtSym.ConstructedFrom?.Name == "Task")))
                            });
                        }
                    }
                }
            }
            return cmds;
        }

        // Placeholder for code generation methods - these would use StringBuilder
        private string GenerateServerImplementation(string serverImplNamespace, string vmName, string vmFullName, string protoNs, string grpcServiceName, List<PropertyInfo> props, List<CommandInfo> cmds)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated at {DateTime.Now}");
            sb.AppendLine($"// Server implementation for {vmFullName}");
            sb.AppendLine($"// PROTO CS NAMESPACE: {protoNs}");
            sb.AppendLine($"// GRPC SERVICE NAME: {grpcServiceName}");
            sb.AppendLine($"using Grpc.Core;");
            sb.AppendLine($"using {protoNs}; // Assuming this is where your gRPC base and messages are");
            sb.AppendLine($"using Google.Protobuf.WellKnownTypes; // For Empty, StringValue, Int32Value, Any etc.");
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Linq;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine($"using System.Collections.Generic;");
            sb.AppendLine($"using System.ComponentModel;");
            sb.AppendLine();
            sb.AppendLine($"namespace {serverImplNamespace};");
            sb.AppendLine();
            // Assumes user's proto generates: {protoNs}.{grpcServiceName}.{grpcServiceName}Base
            sb.AppendLine($"public class {vmName}GrpcServiceImpl : {protoNs}.{grpcServiceName}.{grpcServiceName}Base");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {vmFullName} _viewModel;");
            sb.AppendLine($"    private readonly List<IServerStreamWriter<{protoNs}.PropertyChangeNotification>> _subscribers = new();");
            sb.AppendLine($"    private readonly object _subscriberLock = new();");
            sb.AppendLine();
            sb.AppendLine($"    public {vmName}GrpcServiceImpl({vmFullName} viewModel)");
            sb.AppendLine("    {");
            sb.AppendLine("        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
            sb.AppendLine("        _viewModel.PropertyChanged += ViewModel_PropertyChanged;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public override Task<{protoNs}.{vmName}State> GetState(Empty request, ServerCallContext context)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var state = new {protoNs}.{vmName}State();");
            foreach (var prop in props)
            {
                // Simplified: assumes direct property access and simple type mapping
                // Needs robust type mapping to Protobuf WellKnownTypes or custom messages
                sb.AppendLine($"        // Example for property: {prop.Name} of type {prop.Type}");
                sb.AppendLine($"        // state.{prop.Name} = _viewModel.{prop.Name}; // Direct assignment if proto matches exactly");
                sb.AppendLine($"        // OR use specific packers: state.{prop.Name} = ValueConverter.Pack(_viewModel.{prop.Name});");
                // Example for string - ASSUMES your {vmName}State has a field like 'string {prop.Name}Value' or similar
                // Or if using Any: state.Properties.Add("{prop.Name}", Any.Pack(new StringValue {{ Value = _viewModel.{prop.Name} }}));
                sb.AppendLine($"        // THIS PART REQUIRES A ROBUST TYPE MAPPING STRATEGY AND PROTO DEFINITION KNOWLEDGE");
                sb.AppendLine($"        // For example, if state has a map: state.Properties.Add(\"{prop.Name}\", Google.Protobuf.WellKnownTypes.Any.Pack(new StringValue {{ Value = _viewModel.{prop.Name}?.ToString() ?? \"\" }}));");

            }
            sb.AppendLine("        return Task.FromResult(state);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public override async Task SubscribeToPropertyChanges(Empty request, IServerStreamWriter<{protoNs}.PropertyChangeNotification> responseStream, ServerCallContext context)");
            sb.AppendLine("    {");
            sb.AppendLine("        lock(_subscriberLock) { _subscribers.Add(responseStream); }");
            sb.AppendLine("        try { await context.CancellationToken.WhenCancelled(); }");
            sb.AppendLine("        catch { /* Ignore cancellation exceptions */ }");
            sb.AppendLine("        finally { lock(_subscriberLock) { _subscribers.Remove(responseStream); } }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public override Task<Empty> UpdatePropertyValue({protoNs}.UpdatePropertyValueRequest request, ServerCallContext context)");
            sb.AppendLine("    {");
            sb.AppendLine("        // THIS PART REQUIRES A ROBUST TYPE UNPACKING AND SETTING STRATEGY");
            sb.AppendLine("        // Example for string: if (request.PropertyName == \"UserName\") _viewModel.UserName = request.NewStringValue;");
            sb.AppendLine("        // Or if using Any: ");
            sb.AppendLine("        /*");
            sb.AppendLine("        switch (request.PropertyName)");
            sb.AppendLine("        {");
            foreach (var prop in props)
            {
                sb.AppendLine($"            case \"{prop.Name}\":");
                sb.AppendLine($"                // _viewModel.{prop.Name} = request.NewValue.Unpack<{GetProtoWellKnownTypeFor(prop.FullTypeSymbol)}>().Value;");
                sb.AppendLine($"                // Example: _viewModel.{prop.Name} = request.NewValue.Unpack<StringValue>().Value;");
                sb.AppendLine("                break;");
            }
            sb.AppendLine("        }");
            sb.AppendLine("        */");
            sb.AppendLine("        return Task.FromResult(new Empty());");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                // Assumes proto request/response: {protoNs}.{cmd.MethodName}Request, {protoNs}.{cmd.MethodName}Response
                sb.AppendLine($"    public override async Task<{protoNs}.{cmd.MethodName}Response> {cmd.MethodName}({protoNs}.{cmd.MethodName}Request request, ServerCallContext context)");
                sb.AppendLine("    {");
                // Parameter handling needs to be generated based on cmd.Parameters
                string args = cmd.Parameters.Count > 0
                    ? string.Join(", ", cmd.Parameters.Select(p => $"request.{p.Name}")) // Assumes request has fields named after params
                    : (cmd.IsAsync ? "null" : ""); // AsyncRelayCommand takes object, RelayCommand might take specific type or object

                if (cmd.IsAsync)
                {
                    sb.AppendLine($"        await _viewModel.{cmd.CommandPropertyName}.ExecuteAsync({(cmd.Parameters.Count > 0 ? "request" : "null")}); // Adjust parameter passing");
                }
                else
                {
                    sb.AppendLine($"        _viewModel.{cmd.CommandPropertyName}.Execute({(cmd.Parameters.Count > 0 ? "request" : "null")}); // Adjust parameter passing");
                }
                sb.AppendLine($"        return new {protoNs}.{cmd.MethodName}Response();");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("    private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)");
            sb.AppendLine("    {");
            sb.AppendLine("        object newValue = null; try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); } catch { /* best effort */ }");
            sb.AppendLine($"        var notification = new {protoNs}.PropertyChangeNotification {{ PropertyName = e.PropertyName }};");
            sb.AppendLine("        // Pack newValue into notification.NewValue (e.g., using Any or specific types)");
            sb.AppendLine("        // notification.NewValue = Google.Protobuf.WellKnownTypes.Any.Pack(new StringValue { Value = newValue?.ToString() ?? \"\" });");
            sb.AppendLine();
            sb.AppendLine("        List<IServerStreamWriter<PropertyChangeNotification>> currentSubscribers;"); // Corrected type from original comment
            sb.AppendLine("        lock(_subscriberLock) { currentSubscribers = _subscribers.ToList(); }");
            sb.AppendLine();
            sb.AppendLine("        foreach (var sub in currentSubscribers)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { await sub.WriteAsync(notification); }");
            sb.AppendLine("            catch { lock(_subscriberLock) { _subscribers.Remove(sub); } }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateClientProxyViewModel(string clientProxyNamespace, string originalVmName, string protoNs, string grpcServiceName, List<PropertyInfo> props, List<CommandInfo> cmds)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated at {DateTime.Now}");
            sb.AppendLine($"// Client Proxy ViewModel for {originalVmName}");
            sb.AppendLine($"using CommunityToolkit.Mvvm.ComponentModel; // For ObservableObject");
            sb.AppendLine($"using CommunityToolkit.Mvvm.Input; // For RelayCommand, AsyncRelayCommand");
            sb.AppendLine($"using Grpc.Core;"); // For CancellationToken
            sb.AppendLine($"using {protoNs}; // Assuming this is where your gRPC client and messages are");
            sb.AppendLine($"using Google.Protobuf.WellKnownTypes; // For Empty, StringValue, Int32Value, Any etc.");
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Threading;");
            sb.AppendLine($"using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine($"namespace {clientProxyNamespace};");
            sb.AppendLine();
            // Assumes user's proto generates: {protoNs}.{grpcServiceName}.{grpcServiceName}Client
            sb.AppendLine($"public partial class {originalVmName}RemoteClient : ObservableObject, IDisposable");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {protoNs}.{grpcServiceName}.{grpcServiceName}Client _grpcClient;");
            sb.AppendLine("    private CancellationTokenSource _cts = new CancellationTokenSource();");
            sb.AppendLine();

            foreach (var prop in props)
            {
                // Needs robust type mapping
                sb.AppendLine($"    private {prop.Type} _{LowercaseFirst(prop.Name)};");
                sb.AppendLine($"    public {prop.Type} {prop.Name}");
                sb.AppendLine("    {");
                sb.AppendLine($"        get => _{LowercaseFirst(prop.Name)};");
                sb.AppendLine("        set");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (SetProperty(ref _{LowercaseFirst(prop.Name)}, value)) // From ObservableObject");
                sb.AppendLine("            {");
                sb.AppendLine("                // THIS PART REQUIRES ROBUST TYPE PACKING");
                sb.AppendLine($"                // Example for string: var packedValue = Any.Pack(new StringValue {{ Value = value }});");
                sb.AppendLine($"                // var request = new {protoNs}.UpdatePropertyValueRequest {{ PropertyName = nameof({prop.Name}), NewValue = packedValue }};");
                sb.AppendLine($"                // Task.Run(async () => await _grpcClient.UpdatePropertyValueAsync(request));");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            foreach (var cmd in cmds)
            {
                string commandType = cmd.IsAsync ? "IAsyncRelayCommand" : (cmd.Parameters.Any() ? $"IRelayCommand<{cmd.Parameters[0].Type}>" : "IRelayCommand");
                //string commandImplType = cmd.IsAsync ? "AsyncRelayCommand" : (cmd.Parameters.Any() ? $"RelayCommand<{cmd.Parameters[0].Type}>" : "RelayCommand");
                //string methodName = $"Execute{cmd.MethodName}Async"; // Client methods often async

                sb.AppendLine($"    public {commandType} {cmd.CommandPropertyName} {{ get; }}");
                sb.AppendLine();
            }

            sb.AppendLine($"    public {originalVmName}RemoteClient({protoNs}.{grpcServiceName}.{grpcServiceName}Client grpcClient)");
            sb.AppendLine("    {");
            sb.AppendLine("        _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));");
            foreach (var cmd in cmds)
            {
                string executeMethodName = $"Remote{cmd.MethodName}";
                if (cmd.IsAsync)
                    sb.AppendLine($"        {cmd.CommandPropertyName} = new AsyncRelayCommand({executeMethodName}Async);");
                else if (cmd.Parameters.Any())
                    sb.AppendLine($"        {cmd.CommandPropertyName} = new RelayCommand<{cmd.Parameters[0].Type}>({executeMethodName});");
                else
                    sb.AppendLine($"        {cmd.CommandPropertyName} = new RelayCommand({executeMethodName});");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public async Task InitializeRemoteAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("        // Unpack state and set initial local properties WITHOUT triggering server updates");
            sb.AppendLine("        // Use OnPropertyChanged directly after setting backing fields.");
            foreach (var prop in props)
            {
                sb.AppendLine($"        // _{LowercaseFirst(prop.Name)} = state.{prop.Name}; // Direct if proto matches");
                sb.AppendLine($"        // OnPropertyChanged(nameof({prop.Name}));");
                sb.AppendLine("        // THIS REQUIRES ROBUST TYPE UNPACKING FROM STATE MESSAGE");
            }
            sb.AppendLine("        StartListeningToPropertyChanges(_cts.Token);");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var cmd in cmds)
            {
                // Method signature needs to match constructor delegate for RelayCommand/AsyncRelayCommand
                string paramList = cmd.Parameters.Any() ? $"{cmd.Parameters[0].Type} {LowercaseFirst(cmd.Parameters[0].Name)}" : "";
                string requestInstanceName = cmd.Parameters.Any() ? LowercaseFirst(cmd.Parameters[0].Name) : "";

                string requestCreation;
                if (cmd.Parameters.Any())
                {
                    // Assuming the parameter name in proto message matches the C# parameter name (after lowercasing first char for var name)
                    // And assuming the proto message field is PascalCase.
                    string protoParamName = char.ToUpperInvariant(cmd.Parameters[0].Name[0]) + cmd.Parameters[0].Name.Substring(1);
                    requestCreation = $"new {protoNs}.{cmd.MethodName}Request {{ {protoParamName} = {requestInstanceName} }}";
                }
                else
                {
                    requestCreation = $"new {protoNs}.{cmd.MethodName}Request()";
                }


                if (cmd.IsAsync)
                {
                    sb.AppendLine($"    private async Task Remote{cmd.MethodName}Async({(cmd.Parameters.Any() ? paramList : "")})");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        await _grpcClient.{cmd.MethodName}Async({requestCreation});");
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine($"    private void Remote{cmd.MethodName}({paramList})");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        _ = _grpcClient.{cmd.MethodName}Async({requestCreation}); // Fire and forget or await response");
                    sb.AppendLine("    }");
                }
                sb.AppendLine();
            }

            sb.AppendLine("    private void StartListeningToPropertyChanges(CancellationToken cancellationToken)");
            sb.AppendLine("    {");
            sb.AppendLine("        Task.Run(async () =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            using var call = _grpcClient.SubscribeToPropertyChanges(new Empty(), cancellationToken: cancellationToken);");
            sb.AppendLine("            await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))");
            sb.AppendLine("            {");
            sb.AppendLine("                // Dispatch to UI thread if necessary before updating properties");
            sb.AppendLine("                // THIS REQUIRES ROBUST TYPE UNPACKING FROM PropertyChangeNotification");
            sb.AppendLine("                /*");
            sb.AppendLine("                switch (update.PropertyName)");
            sb.AppendLine("                {");
            foreach (var prop in props)
            {
                sb.AppendLine($"                    case nameof({prop.Name}):");
                sb.AppendLine($"                        _{LowercaseFirst(prop.Name)} = update.NewValue.Unpack<...>().Value; // Unpack appropriately");
                sb.AppendLine($"                        OnPropertyChanged(nameof({prop.Name}));");
                sb.AppendLine("                        break;");
            }
            sb.AppendLine("                }");
            sb.AppendLine("                */");
            sb.AppendLine("            }");
            sb.AppendLine("        }, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public void Dispose()");
            sb.AppendLine("    {");
            sb.AppendLine("        _cts.Cancel();");
            sb.AppendLine("        _cts.Dispose();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
        private string GetProtoWellKnownTypeFor(ITypeSymbol typeSymbol)
        {
            // Handle Nullable<T> by getting the underlying type T
            if (typeSymbol is INamedTypeSymbol namedTypeSymbolNullable &&
                namedTypeSymbolNullable.IsGenericType &&
                namedTypeSymbolNullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                typeSymbol = namedTypeSymbolNullable.TypeArguments[0]; // Get T
            }

            // Handle Enums - they are typically represented as int32 in gRPC/Protobuf by default
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                // You could refine this by checking typeSymbol.EnumUnderlyingType.SpecialType
                // and mapping to Int32Value, Int64Value, etc., accordingly.
                // For simplicity and common practice, mapping to Int32Value.
                return "Int32Value";
            }

            switch (typeSymbol.SpecialType)
            {
                // String
                case SpecialType.System_String: return "StringValue";

                // Boolean
                case SpecialType.System_Boolean: return "BoolValue";

                // Floating point
                case SpecialType.System_Single: return "FloatValue";  // C# float
                case SpecialType.System_Double: return "DoubleValue"; // C# double

                // Standard integral types
                case SpecialType.System_Int32: return "Int32Value";  // C# int
                case SpecialType.System_Int64: return "Int64Value";  // C# long
                case SpecialType.System_UInt32: return "UInt32Value"; // C# uint
                case SpecialType.System_UInt64: return "UInt64Value"; // C# ulong

                // Smaller integral types (promoted to 32-bit WKT wrappers)
                case SpecialType.System_SByte: return "Int32Value";  // C# sbyte
                case SpecialType.System_Byte: return "UInt32Value"; // C# byte (unsigned, 0-255)
                case SpecialType.System_Int16: return "Int32Value";  // C# short
                case SpecialType.System_UInt16: return "UInt32Value"; // C# ushort

                // Char
                case SpecialType.System_Char: return "StringValue"; // Represent char as a single-character StringValue.
                                                                    // Alternatively, could be UInt32Value for its numeric UTF-16 value.

                // DateTime
                case SpecialType.System_DateTime: return "Timestamp"; // Maps to google.protobuf.Timestamp

                // Object (maps to Any)
                case SpecialType.System_Object: return "Any";

                    // Decimal has no direct WKT. Often represented as string or a custom message.
                    // Allowing it to fall through to "Any" or explicitly map to "StringValue" if string serialization is a common fallback.
                    // case SpecialType.System_Decimal: return "StringValue"; // Example if choosing string representation by default
            }

            // Handle byte[] (for sequence of bytes)
            // Needs to be IArrayTypeSymbol with ElementType System.Byte and Rank 1
            if (typeSymbol.TypeKind == TypeKind.Array && typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                if (arrayTypeSymbol.ElementType.SpecialType == SpecialType.System_Byte && arrayTypeSymbol.Rank == 1)
                {
                    return "BytesValue"; // Maps to google.protobuf.BytesValue
                }
            }

            // Handle other specific System types by their full name
            string fullTypeName = typeSymbol.ToDisplayString();

            if (fullTypeName == "System.TimeSpan")
            {
                return "Duration"; // Maps to google.protobuf.Duration
            }

            if (fullTypeName == "System.Guid")
            {
                // GUIDs are commonly represented as strings in Protobuf.
                return "StringValue";
            }

            // Fallback for types not explicitly mapped above.
            // The original code used "Any", which is a reasonable default if the .proto
            // field is designed to hold arbitrary types or if no specific WKT is suitable.
            return "Any";
        }
        private string LowercaseFirst(string str) => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str.Substring(1);

        // Helper record/class for storing extracted info
        internal record PropertyInfo { public string Name; public string Type; public ITypeSymbol FullTypeSymbol; }
        internal record CommandInfo { public string MethodName; public string CommandPropertyName; public List<ParameterInfo> Parameters; public bool IsAsync; }
        internal record ParameterInfo { public string Name; public string Type; }
    }
}