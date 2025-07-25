using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class ClientGenerator
{
    public static string Generate(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds, string? clientNamespace = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Client Proxy ViewModel for {vmName}");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
        sb.AppendLine("using CommunityToolkit.Mvvm.Input;");
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
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
        var ns = clientNamespace ?? ($"{protoNs.Substring(0, protoNs.LastIndexOf('.'))}.RemoteClients");
        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {vmName}RemoteClient : ObservableObject, IDisposable");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {protoNs}.{serviceName}.{serviceName}Client _grpcClient;");
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
            string backingFieldName = $"_{GeneratorHelpers.LowercaseFirst(prop.Name)}";
            sb.AppendLine($"        private {prop.TypeString} {backingFieldName} = default!;");
            sb.AppendLine($"        public {prop.TypeString} {prop.Name}");
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
                var paramType = cmd.Parameters[0].TypeString;
                methodGenericTypeArg = $"<{paramType}>";
                commandInterfaceType = cmd.IsAsync ? $"IAsyncRelayCommand{methodGenericTypeArg}" : $"IRelayCommand{methodGenericTypeArg}";
            }
            sb.AppendLine($"        public {commandInterfaceType} {cmd.CommandPropertyName} {{ get; }}");
        }
        sb.AppendLine();

        sb.AppendLine($"        public {vmName}RemoteClient({protoNs}.{serviceName}.{serviceName}Client grpcClient)");
        sb.AppendLine("        {");
        sb.AppendLine("            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));");
        foreach (var cmd in cmds)
        {
            string remoteExecuteMethodName = $"RemoteExecute_{cmd.MethodName}";
            string methodGenericTypeArg = "";
            string commandConcreteType = cmd.IsAsync ? "AsyncRelayCommand" : "RelayCommand";
            if (cmd.Parameters.Any())
            {
                methodGenericTypeArg = $"<{cmd.Parameters[0].TypeString}>";
                commandConcreteType += methodGenericTypeArg;
            }
            if (cmd.IsAsync)
                sb.AppendLine($"            {cmd.CommandPropertyName} = new {commandConcreteType}({remoteExecuteMethodName}Async);");
            else
                sb.AppendLine($"            {cmd.CommandPropertyName} = new {commandConcreteType}({remoteExecuteMethodName});");
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
        sb.AppendLine($"                    if (response.Status == {protoNs}.ConnectionStatus.Connected)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (lastStatus != \"Connected\")");
        sb.AppendLine("                        {");
        sb.AppendLine("                            try");
        sb.AppendLine("                            {");
        sb.AppendLine("                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);");
        foreach (var prop in props)
        {
            string protoStateFieldName = GeneratorHelpers.ToPascalCase(prop.Name);
            sb.AppendLine($"                                this.{prop.Name} = state.{protoStateFieldName};");
        }
        sb.AppendLine("                                Debug.WriteLine(\"[ClientProxy] State re-synced after reconnect.\");");
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
        sb.AppendLine($"            Debug.WriteLine(\"[{vmName}RemoteClient] Initializing...\");");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);");
        sb.AppendLine("                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);");
        sb.AppendLine($"                Debug.WriteLine(\"[{vmName}RemoteClient] Initial state received.\");");
        foreach (var prop in props)
        {
            string protoStateFieldName = GeneratorHelpers.ToPascalCase(prop.Name);
            sb.AppendLine($"                this.{prop.Name} = state.{protoStateFieldName};");
        }
        sb.AppendLine("                _isInitialized = true;");
        sb.AppendLine($"                Debug.WriteLine(\"[{vmName}RemoteClient] Initialized successfully.\");");
        sb.AppendLine("                StartListeningToPropertyChanges(_cts.Token);");
        sb.AppendLine("                _ = StartPingLoopAsync();");
        sb.AppendLine("            }");
        sb.AppendLine($"            catch (RpcException ex) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Failed to initialize: \" + ex.Status.StatusCode + \" - \" + ex.Status.Detail); }}");
        sb.AppendLine($"            catch (OperationCanceledException) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Initialization cancelled.\"); }}");
        sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Unexpected error during initialization: \" + ex.Message); }}");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var cmd in cmds)
        {
            string cmdMethodNameForLog = cmd.MethodName;
            string paramListWithType = string.Join(", ", cmd.Parameters.Select(p => $"{p.TypeString} {GeneratorHelpers.LowercaseFirst(p.Name)}"));
            string requestCreation = $"new {protoNs}.{cmd.MethodName}Request()";
            if (cmd.Parameters.Any())
            {
                var paramAssignments = cmd.Parameters.Select(p => $"{GeneratorHelpers.ToPascalCase(p.Name)} = {GeneratorHelpers.LowercaseFirst(p.Name)}");
                requestCreation = $"new {protoNs}.{cmd.MethodName}Request {{ {string.Join(", ", paramAssignments)} }}";
            }
            string methodSignature = cmd.IsAsync ? $"private async Task RemoteExecute_{cmd.MethodName}Async({paramListWithType})" : $"private void RemoteExecute_{cmd.MethodName}({paramListWithType})";
            sb.AppendLine($"        {methodSignature}");
            sb.AppendLine("        {");
            string earlyExit = cmd.IsAsync ? "return;" : "return;";
            sb.AppendLine($"            if (!_isInitialized || _isDisposed) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Not initialized or disposed, command {cmdMethodNameForLog} skipped.\"); {earlyExit} }}");
            sb.AppendLine($"            Debug.WriteLine(\"[ClientProxy:{vmName}] Executing command {cmdMethodNameForLog} remotely...\");");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            if (cmd.IsAsync)
                sb.AppendLine($"                await _grpcClient.{cmd.MethodName}Async({requestCreation}, cancellationToken: _cts.Token);");
            else
                sb.AppendLine($"                _ = _grpcClient.{cmd.MethodName}Async({requestCreation}, cancellationToken: _cts.Token);");
            sb.AppendLine("            }");
            sb.AppendLine($"            catch (RpcException ex) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Error executing command {cmdMethodNameForLog}: \" + ex.Status.StatusCode + \" - \" + ex.Status.Detail); }}");
            sb.AppendLine($"            catch (OperationCanceledException) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Command {cmdMethodNameForLog} cancelled.\"); }}");
            sb.AppendLine($"            catch (Exception ex) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Unexpected error executing command {cmdMethodNameForLog}: \" + ex.Message); }}");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            _ = Task.Run(async () => ");
        sb.AppendLine("            {");
        sb.AppendLine("                if (_isDisposed) return;");
        sb.AppendLine($"                Debug.WriteLine(\"[{vmName}RemoteClient] Starting property change listener...\");");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var subscribeRequest = new {protoNs}.SubscribeRequest {{ ClientId = Guid.NewGuid().ToString() }};");
        sb.AppendLine($"                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);");
        sb.AppendLine($"                    Debug.WriteLine(\"[{vmName}RemoteClient] Subscribed to property changes. Waiting for updates...\");");
        sb.AppendLine("                    int updateCount = 0;");
        sb.AppendLine("                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        updateCount++;");
        sb.AppendLine("                        if (_isDisposed) { Debug.WriteLine(\"[" + vmName + "RemoteClient] Disposed during update \" + updateCount + \", exiting property update loop.\"); break; }");
        sb.AppendLine("                        Debug.WriteLine($\"[" + vmName + "RemoteClient] RAW UPDATE #\" + updateCount + \" RECEIVED: PropertyName=\\\"\" + update.PropertyName + \"\\\", ValueTypeUrl=\\\"\" + (update.NewValue?.TypeUrl ?? \"null_type_url\") + \"\\\"\");");
        sb.AppendLine("                        Action updateAction = () => {");
        sb.AppendLine("                           try {");
        sb.AppendLine("                               Debug.WriteLine(\"[" + vmName + "RemoteClient] Dispatcher: Attempting to update \\\"\" + update.PropertyName + \"\\\" (Update #\" + updateCount + \").\");");
        sb.AppendLine("                               switch (update.PropertyName)");
        sb.AppendLine("                               {");
        foreach (var prop in props)
        {
            string wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(prop.FullTypeSymbol!);
            string csharpPropName = prop.Name;
            sb.AppendLine($"                                   case nameof({csharpPropName}):");
            if (wkt == "StringValue") sb.AppendLine($"                 if (update.NewValue!.Is(StringValue.Descriptor)) {{ var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($\"Updating {csharpPropName} from \\\"{{this.{csharpPropName}}}\\\" to '\\\"{{val}}\\\".\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is '\\\"{{this.{csharpPropName}}}\\\".\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected StringValue.\"); }} break;");
            else if (wkt == "Int32Value") sb.AppendLine($"                     if (update.NewValue!.Is(Int32Value.Descriptor)) {{ var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($\"Updating {csharpPropName} from {{this.{csharpPropName}}} to {{val}}.\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is {{this.{csharpPropName}}}.\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected Int32Value.\"); }} break;");
            else if (wkt == "BoolValue") sb.AppendLine($"                    if (update.NewValue!.Is(BoolValue.Descriptor)) {{ var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($\"Updating {csharpPropName} from {{this.{csharpPropName}}} to {{val}}.\"); this.{csharpPropName} = val; Debug.WriteLine($\"After update, {csharpPropName} is {{this.{csharpPropName}}}.\"); }} else {{ Debug.WriteLine($\"Mismatched descriptor for {csharpPropName}, expected BoolValue.\"); }} break;");
            else sb.AppendLine($"                                       Debug.WriteLine($\"[ClientProxy:{vmName}] Unpacking for {prop.Name} with WKT {wkt} not fully implemented or is Any.\"); break;");
        }
        sb.AppendLine($"                                   default: Debug.WriteLine($\"[ClientProxy:{vmName}] Unknown property in notification: \\\"{{update.PropertyName}}\\\"\"); break;");
        sb.AppendLine("                               }");
        sb.AppendLine("                           } catch (Exception exInAction) { Debug.WriteLine($\"[ClientProxy:" + vmName + "] EXCEPTION INSIDE updateAction for \\\"{update.PropertyName}\\\": \" + exInAction.ToString()); }");
        sb.AppendLine("                        };");
        sb.AppendLine("                        #if WPF_DISPATCHER");
        sb.AppendLine("                        Application.Current?.Dispatcher.Invoke(updateAction);");
        sb.AppendLine("                        #else");
        sb.AppendLine("                        updateAction();");
        sb.AppendLine("                        #endif");
        sb.AppendLine($"                        Debug.WriteLine(\"[{vmName}RemoteClient] Processed update #\" + updateCount + \" for \\\"\" + update.PropertyName + \"\\\". Still listening...\");");
        sb.AppendLine("                    }");
        sb.AppendLine("                    Debug.WriteLine(\"[" + vmName + "RemoteClient] ReadAllAsync completed or cancelled after \" + updateCount + \" updates.\");");
        sb.AppendLine("                }");
        sb.AppendLine($"                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) {{ Debug.WriteLine(\"[ClientProxy:{vmName}] Property subscription RpcException Cancelled.\"); }}");
        sb.AppendLine($"                catch (OperationCanceledException) {{ Debug.WriteLine($\"[ClientProxy:{vmName}] Property subscription OperationCanceledException.\"); }}");
        sb.AppendLine($"                catch (Exception ex) {{ if (!_isDisposed) Debug.WriteLine($\"[ClientProxy:{vmName}] Error in property listener: \" + ex.GetType().Name + \" - \" + ex.Message + \"\\nStackTrace: \" + ex.StackTrace); }}");
        sb.AppendLine($"                Debug.WriteLine(\"[{vmName}RemoteClient] Property change listener task finished.\");");
        sb.AppendLine("            }, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_isDisposed) return;");
        sb.AppendLine("            _isDisposed = true;");
        sb.AppendLine($"            Debug.WriteLine(\"[{vmName}RemoteClient] Disposing...\");");
        sb.AppendLine("            _cts.Cancel();");
        sb.AppendLine("            _cts.Dispose();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

}
