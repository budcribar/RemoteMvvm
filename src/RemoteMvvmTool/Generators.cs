using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public static class Generators
{
    public static string GenerateProto(string protoNs, string serviceName, string vmName, List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
    {
        var body = new StringBuilder();
        body.AppendLine($"message {vmName}State {{");
        int field = 1;
        foreach (var p in props)
        {
            body.AppendLine($"  string {ToSnake(p.Name)} = {field++};");
        }
        body.AppendLine("}");
        body.AppendLine();
        body.AppendLine("message UpdatePropertyValueRequest {");
        body.AppendLine("  string property_name = 1;");
        body.AppendLine("  google.protobuf.Any new_value = 2;");
        body.AppendLine("}");
        body.AppendLine();
        body.AppendLine("message PropertyChangeNotification {");
        body.AppendLine("  string property_name = 1;");
        body.AppendLine("  google.protobuf.Any new_value = 2;");
        body.AppendLine("}");
        foreach (var c in cmds)
        {
            body.AppendLine();
            body.AppendLine($"message {c.MethodName}Request {{}}");
            body.AppendLine($"message {c.MethodName}Response {{}}");
        }

        body.AppendLine();
        body.AppendLine("message SubscribeRequest {");
        body.AppendLine("  string client_id = 1;");
        body.AppendLine("}");

        body.AppendLine();
        body.AppendLine("enum ConnectionStatus {");
        body.AppendLine("  UNKNOWN = 0;");
        body.AppendLine("  CONNECTED = 1;");
        body.AppendLine("  DISCONNECTED = 2;");
        body.AppendLine("}");

        body.AppendLine();
        body.AppendLine("message ConnectionStatusResponse {");
        body.AppendLine("  ConnectionStatus status = 1;");
        body.AppendLine("}");
        body.AppendLine();
        body.AppendLine("service " + serviceName + " {");
        body.AppendLine($"  rpc GetState (google.protobuf.Empty) returns ({vmName}State);");
        body.AppendLine($"  rpc UpdatePropertyValue (UpdatePropertyValueRequest) returns (google.protobuf.Empty);");
        body.AppendLine($"  rpc SubscribeToPropertyChanges (SubscribeRequest) returns (stream PropertyChangeNotification);");
        foreach (var c in cmds)
        {
            body.AppendLine($"  rpc {c.MethodName} ({c.MethodName}Request) returns ({c.MethodName}Response);");
        }
        body.AppendLine("  rpc Ping (google.protobuf.Empty) returns (ConnectionStatusResponse);");
        body.AppendLine("}");

        var final = new StringBuilder();
        final.AppendLine("syntax = \"proto3\";");
        final.AppendLine($"option csharp_namespace = \"{protoNs}\";");
        final.AppendLine("import \"google/protobuf/empty.proto\";");
        final.AppendLine("import \"google/protobuf/any.proto\";");
        final.AppendLine();
        final.Append(body.ToString());
        return final.ToString();
    }

    public static string GenerateTypeScriptClient(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Auto-generated TypeScript client for {vmName}");
        sb.AppendLine($"import {{ {serviceName}Client }} from './generated/{serviceName}ServiceClientPb';");
        var requestTypes = string.Join(", ", cmds.Select(c => c.MethodName + "Request").Distinct());
        if (!string.IsNullOrWhiteSpace(requestTypes))
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, {requestTypes} }} from './generated/{serviceName}_pb';");
        }
        else
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus }} from './generated/{serviceName}_pb';");
        }
        sb.AppendLine("import * as grpcWeb from 'grpc-web';");
        sb.AppendLine("import { Empty } from 'google-protobuf/google/protobuf/empty_pb';");
        sb.AppendLine("import { Any } from 'google-protobuf/google/protobuf/any_pb';");
        sb.AppendLine("import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';");
        sb.AppendLine();
        sb.AppendLine($"export class {vmName}RemoteClient {{");
        sb.AppendLine($"    private readonly grpcClient: {serviceName}Client;");
        sb.AppendLine("    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;");
        sb.AppendLine("    private pingIntervalId?: any;");
        sb.AppendLine("    private changeCallbacks: Array<() => void> = [];");
        sb.AppendLine();
        foreach (var p in props)
        {
            sb.AppendLine($"    {ToCamelCase(p.Name)}: any;");
        }
        sb.AppendLine("    connectionStatus: string = 'Unknown';");
        sb.AppendLine();
        sb.AppendLine("    addChangeListener(cb: () => void): void {");
        sb.AppendLine("        this.changeCallbacks.push(cb);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private notifyChange(): void {");
        sb.AppendLine("        this.changeCallbacks.forEach(cb => cb());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    constructor(grpcClient: {serviceName}Client) {{");
        sb.AppendLine("        this.grpcClient = grpcClient;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    async initializeRemote(): Promise<void> {");
        sb.AppendLine("        const state = await this.grpcClient.getState(new Empty());");
        foreach (var p in props)
        {
            sb.AppendLine($"        this.{ToCamelCase(p.Name)} = (state as any).get{p.Name}();");
        }
        sb.AppendLine("        this.connectionStatus = 'Connected';");
        sb.AppendLine("        this.notifyChange();");
        sb.AppendLine("        this.startListeningToPropertyChanges();");
        sb.AppendLine("        this.startPingLoop();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    async refreshState(): Promise<void> {");
        sb.AppendLine("        const state = await this.grpcClient.getState(new Empty());");
        foreach (var p in props)
        {
            sb.AppendLine($"        this.{ToCamelCase(p.Name)} = (state as any).get{p.Name}();");
        }
        sb.AppendLine("        this.notifyChange();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    async updatePropertyValue(propertyName: string, value: any): Promise<void> {");
        sb.AppendLine("        const req = new UpdatePropertyValueRequest();");
        sb.AppendLine("        req.setPropertyName(propertyName);");
        sb.AppendLine("        req.setNewValue(this.createAnyValue(value));");
        sb.AppendLine("        await this.grpcClient.updatePropertyValue(req);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private createAnyValue(value: any): Any {");
        sb.AppendLine("        const anyVal = new Any();");
        sb.AppendLine("        if (typeof value === 'string') {");
        sb.AppendLine("            const wrapper = new StringValue();");
        sb.AppendLine("            wrapper.setValue(value);");
        sb.AppendLine("            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.StringValue');");
        sb.AppendLine("        } else if (typeof value === 'number' && Number.isInteger(value)) {");
        sb.AppendLine("            const wrapper = new Int32Value();");
        sb.AppendLine("            wrapper.setValue(value);");
        sb.AppendLine("            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');");
        sb.AppendLine("        } else if (typeof value === 'boolean') {");
        sb.AppendLine("            const wrapper = new BoolValue();");
        sb.AppendLine("            wrapper.setValue(value);");
        sb.AppendLine("            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.BoolValue');");
        sb.AppendLine("        } else {");
        sb.AppendLine("            throw new Error('Unsupported value type');");
        sb.AppendLine("        }");
        sb.AppendLine("        return anyVal;");
        sb.AppendLine("    }");
        sb.AppendLine();
        foreach (var cmd in cmds)
        {
            var paramList = string.Join(", ", cmd.Parameters.Select(p => ToCamelCase(p.Name) + ": any"));
            var reqType = cmd.MethodName + "Request";
            sb.AppendLine($"    async {ToCamelCase(cmd.MethodName)}({paramList}): Promise<void> {{");
            sb.AppendLine($"        const req = new {reqType}();");
            foreach (var p in cmd.Parameters)
            {
                sb.AppendLine($"        req.set{ToPascalCase(p.Name)}({ToCamelCase(p.Name)});");
            }
            sb.AppendLine($"        await this.grpcClient.{ToCamelCase(cmd.MethodName)}(req);");
            sb.AppendLine("    }");
        }
        sb.AppendLine();
        sb.AppendLine("    private startPingLoop(): void {");
        sb.AppendLine("        if (this.pingIntervalId) return;");
        sb.AppendLine("        this.pingIntervalId = setInterval(async () => {");
        sb.AppendLine("            try {");
        sb.AppendLine("                const resp: ConnectionStatusResponse = await this.grpcClient.ping(new Empty());");
        sb.AppendLine("                if (resp.getStatus() === ConnectionStatus.CONNECTED) {");
        sb.AppendLine("                    if (this.connectionStatus !== 'Connected') {");
        sb.AppendLine("                        await this.refreshState();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    this.connectionStatus = 'Connected';");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    this.connectionStatus = 'Disconnected';");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch {");
        sb.AppendLine("                this.connectionStatus = 'Disconnected';");
        sb.AppendLine("            }");
        sb.AppendLine("            this.notifyChange();");
        sb.AppendLine("        }, 5000);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private startListeningToPropertyChanges(): void {");
        sb.AppendLine("        const req = new SubscribeRequest();");
        sb.AppendLine("        req.setClientId(Math.random().toString());");
        sb.AppendLine("        this.propertyStream = this.grpcClient.subscribeToPropertyChanges(req);");
        sb.AppendLine("        this.propertyStream.on('data', (update: PropertyChangeNotification) => {");
        sb.AppendLine("            const anyVal = update.getNewValue();");
        sb.AppendLine("            switch (update.getPropertyName()) {");
        foreach (var p in props)
        {
            var wrapper = GetWrapperType(p.TypeString);
            if (wrapper != null)
            {
                string unpack = wrapper switch
                {
                    "StringValue" => "StringValue.deserializeBinary",
                    "Int32Value" => "Int32Value.deserializeBinary",
                    "BoolValue" => "BoolValue.deserializeBinary",
                    _ => ""
                };
                sb.AppendLine($"                case '{p.Name}':");
                sb.AppendLine($"                    this.{ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.getValue();");
                sb.AppendLine("                    break;");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            this.notifyChange();");
        sb.AppendLine("        });");
        sb.AppendLine("        this.propertyStream.on('error', () => {");
        sb.AppendLine("            this.propertyStream = undefined;");
        sb.AppendLine("            setTimeout(() => this.startListeningToPropertyChanges(), 1000);");
        sb.AppendLine("        });");
        sb.AppendLine("        this.propertyStream.on('end', () => {");
        sb.AppendLine("            this.propertyStream = undefined;");
        sb.AppendLine("            setTimeout(() => this.startListeningToPropertyChanges(), 1000);");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    dispose(): void {");
        sb.AppendLine("        if (this.propertyStream) {");
        sb.AppendLine("            this.propertyStream.cancel();");
        sb.AppendLine("            this.propertyStream = undefined;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (this.pingIntervalId) {");
        sb.AppendLine("            clearInterval(this.pingIntervalId);");
        sb.AppendLine("            this.pingIntervalId = undefined;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateServer(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Concurrent;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using System.Threading.Channels;");
        sb.AppendLine("using System.Windows.Threading;");
        sb.AppendLine();
        sb.AppendLine($"public partial class {vmName}GrpcServiceImpl : {serviceName}.{serviceName}Base");
        sb.AppendLine("{");
        sb.AppendLine("    public static event System.EventHandler<int>? ClientCountChanged;");
        sb.AppendLine("    private static int _clientCount = -1;");
        sb.AppendLine("    public static int ClientCount");
        sb.AppendLine("    {");
        sb.AppendLine("        get => _clientCount;");
        sb.AppendLine("        private set");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_clientCount != value)");
        sb.AppendLine("            {");
        sb.AppendLine("                _clientCount = value;");
        sb.AppendLine("                ClientCountChanged?.Invoke(null, value);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    static {vmName}GrpcServiceImpl()");
        sb.AppendLine("    {");
        sb.AppendLine("        ClientCount = 0;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    private readonly {vmName} _viewModel;");
        sb.AppendLine($"    private static readonly ConcurrentDictionary<IServerStreamWriter<{protoNs}.PropertyChangeNotification>, Channel<{protoNs}.PropertyChangeNotification>> _subscriberChannels = new ConcurrentDictionary<IServerStreamWriter<{protoNs}.PropertyChangeNotification>, Channel<{protoNs}.PropertyChangeNotification>>();");
        sb.AppendLine("    private readonly Dispatcher _dispatcher;");
        sb.AppendLine();
        sb.AppendLine($"    public {vmName}GrpcServiceImpl({vmName} viewModel, Dispatcher dispatcher)");
        sb.AppendLine("    {");
        sb.AppendLine("        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
        sb.AppendLine("        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));");
        sb.AppendLine("        if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override Task<{vmName}State> GetState(Empty request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var state = new {vmName}State();");
        foreach (var p in props)
        {
            sb.AppendLine($"        // Mapping property: {p.Name} to state.{p.Name}");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine($"            var propValue = _viewModel.{p.Name};");
            if (true)
            {
                sb.AppendLine($"            state.{p.Name} = propValue;");
            }
            sb.AppendLine("        }");
            sb.AppendLine($"        catch (Exception ex) {{ Debug.WriteLine(\"[GrpcService:{vmName}] Error mapping property {p.Name} to state.{p.Name}: \" + ex.Message); }}");
        }
        sb.AppendLine("        return Task.FromResult(state);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override async Task SubscribeToPropertyChanges({protoNs}.SubscribeRequest request, IServerStreamWriter<{protoNs}.PropertyChangeNotification> responseStream, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var channel = Channel.CreateUnbounded<{protoNs}.PropertyChangeNotification>(new UnboundedChannelOptions {{ SingleReader = true, SingleWriter = false }});");
        sb.AppendLine("        _subscriberChannels.TryAdd(responseStream, channel);");
        sb.AppendLine("        ClientCount = _subscriberChannels.Count;");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            await foreach (var notification in channel.Reader.ReadAllAsync(context.CancellationToken))");
        sb.AppendLine("            {");
        sb.AppendLine("                await responseStream.WriteAsync(notification);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            _subscriberChannels.TryRemove(responseStream, out _);");
        sb.AppendLine("            channel.Writer.TryComplete();");
        sb.AppendLine("            ClientCount = _subscriberChannels.Count;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override Task<Empty> UpdatePropertyValue({protoNs}.UpdatePropertyValueRequest request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        _dispatcher.Invoke(() => {");
        sb.AppendLine("            var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);");
        sb.AppendLine("            if (propertyInfo != null && propertyInfo.CanWrite)");
        sb.AppendLine("            {");
        sb.AppendLine("                try {");
        sb.AppendLine("                    if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);");
        sb.AppendLine("                    else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);");
        sb.AppendLine("                    else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);");
        sb.AppendLine("                    else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Unpacking not implemented for property \"" + request.PropertyName + "\" and type \"" + request.NewValue.TypeUrl + "\".\"); }");
        sb.AppendLine("                } catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error setting property \"" + request.PropertyName + "\": " + ex.Message); }");
        sb.AppendLine("            }");
        sb.AppendLine("            else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Property \"" + request.PropertyName + "\" not found or not writable.\"); }");
        sb.AppendLine("        });");
        sb.AppendLine("        return Task.FromResult(new Empty());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override Task<ConnectionStatusResponse> Ping(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.FromResult(new ConnectionStatusResponse { Status = ConnectionStatus.Connected });");
        sb.AppendLine("    }");
        sb.AppendLine();
        foreach (var cmd in cmds)
        {
            sb.AppendLine($"    public override async Task<{protoNs}.{cmd.MethodName}Response> {cmd.MethodName}({protoNs}.{cmd.MethodName}Request request, ServerCallContext context)");
            sb.AppendLine("    {");
            sb.AppendLine("        try { await await _dispatcher.InvokeAsync(async () => {");
            string commandPropAccess = $"_viewModel.{cmd.CommandPropertyName}";
            if (cmd.IsAsync)
            {
                sb.AppendLine($"            var command = {commandPropAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;");
                sb.AppendLine("            if (command != null)");
                sb.AppendLine("            {");
                if (cmd.Parameters.Count == 1)
                {
                    var param = cmd.Parameters[0];
                    sb.AppendLine($"                var typedCommand = {commandPropAccess} as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<{param.TypeString}>;");
                    sb.AppendLine($"                if (typedCommand != null) await typedCommand.ExecuteAsync(request.{ToPascalCase(param.Name)}); else await command.ExecuteAsync(request);");
                }
                else if (cmd.Parameters.Count == 0)
                    sb.AppendLine("                await command.ExecuteAsync(null);");
                else
                    sb.AppendLine("                await command.ExecuteAsync(request);");
                sb.AppendLine("            }");
                sb.AppendLine($"            else {{ Debug.WriteLine(\"[GrpcService:{vmName}] Command {cmd.CommandPropertyName} not found or not IAsyncRelayCommand.\"); }}");
            }
            else
            {
                sb.AppendLine($"            var command = {commandPropAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand;");
                sb.AppendLine("            if (command != null)");
                sb.AppendLine("            {");
                if (cmd.Parameters.Count == 1)
                {
                    var param = cmd.Parameters[0];
                    sb.AppendLine($"                var typedCommand = {commandPropAccess} as CommunityToolkit.Mvvm.Input.IRelayCommand<{param.TypeString}>;");
                    sb.AppendLine($"                if (typedCommand != null) typedCommand.Execute(request.{ToPascalCase(param.Name)}); else command.Execute(request);");
                }
                else if (cmd.Parameters.Count == 0)
                    sb.AppendLine("                command.Execute(null);");
                else
                    sb.AppendLine("                command.Execute(request);");
                sb.AppendLine("            }");
                sb.AppendLine($"            else {{ Debug.WriteLine(\"[GrpcService:{vmName}] Command {cmd.CommandPropertyName} not found or not IRelayCommand.\"); }}");
            }
            sb.AppendLine("        }); } catch (Exception ex) {");
            sb.AppendLine($"        Debug.WriteLine(\"[GrpcService:{vmName}] Exception during command execution for {cmd.MethodName}: \" + ex.ToString());");
            sb.AppendLine("        throw new RpcException(new Status(StatusCode.Internal, \"Error executing command on server: \" + ex.Message));");
            sb.AppendLine("        }");
            sb.AppendLine($"        return new {protoNs}.{cmd.MethodName}Response();");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.AppendLine("    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrEmpty(e.PropertyName)) return;");
        sb.AppendLine("        object? newValue = null;");
        sb.AppendLine("        try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }");
        sb.AppendLine("        catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error getting property value for \\\"" + e.PropertyName + "\\\": " + ex.Message); return; }");
        sb.AppendLine();
        sb.AppendLine($"        var notification = new {protoNs}.PropertyChangeNotification {{ PropertyName = e.PropertyName }};");
        sb.AppendLine("        if (newValue == null) notification.NewValue = Any.Pack(new Empty());");
        sb.AppendLine("        else if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });");
        sb.AppendLine("        else if (newValue is int i) notification.NewValue = Any.Pack(new Int32Value { Value = i });");
        sb.AppendLine("        else if (newValue is bool b) notification.NewValue = Any.Pack(new BoolValue { Value = b });");
        sb.AppendLine("        else if (newValue is double d) notification.NewValue = Any.Pack(new DoubleValue { Value = d });");
        sb.AppendLine("        else if (newValue is float f) notification.NewValue = Any.Pack(new FloatValue { Value = f });");
        sb.AppendLine("        else if (newValue is long l) notification.NewValue = Any.Pack(new Int64Value { Value = l });");
        sb.AppendLine("        else if (newValue is DateTime dt) notification.NewValue = Any.Pack(Timestamp.FromDateTime(dt.ToUniversalTime()));");
        sb.AppendLine($"        else {{ Debug.WriteLine($\"[GrpcService:{vmName}] PropertyChanged: Packing not implemented for type {{(newValue?.GetType().FullName ?? \"null\")}} of property {{e.PropertyName}}.\"); notification.NewValue = Any.Pack(new StringValue {{ Value = newValue.ToString() }}); }}");
        sb.AppendLine();
        sb.AppendLine("        foreach (var channelWriter in _subscriberChannels.Values.Select(c => c.Writer))");
        sb.AppendLine("        {");
        sb.AppendLine("            try { await channelWriter.WriteAsync(notification); }");
        sb.AppendLine("            catch (ChannelClosedException) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Channel closed for a subscriber, cannot write notification for '\" + e.PropertyName + "'. Subscriber likely disconnected.\"); }");
        sb.AppendLine("            catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error writing to subscriber channel for '\" + e.PropertyName + "': \" + ex.Message); }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateClient(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using CommunityToolkit.Mvvm.ComponentModel;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {vmName}RemoteClient : ObservableObject");
        sb.AppendLine("{");
        foreach (var p in props)
        {
            sb.AppendLine($"  public {p.TypeString} {p.Name} {{ get; private set; }}");
        }
        sb.AppendLine("  public " + vmName + "RemoteClient(" + serviceName + "." + serviceName + "Client client) {}" );
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToSnake(string s) => string.Concat(s.Select((c,i) => i>0 && char.IsUpper(c)?"_"+char.ToLower(c).ToString():char.ToLower(c).ToString()));
    private static string ToCamel(string s) => string.IsNullOrEmpty(s)?s:char.ToLowerInvariant(s[0])+s.Substring(1);
    private static string ToCamelCase(string s) => string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    private static string ToPascalCase(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string? GetWrapperType(string typeName) => typeName switch
    {
        "string" => "StringValue",
        "int" => "Int32Value",
        "System.Int32" => "Int32Value",
        "bool" => "BoolValue",
        "System.Boolean" => "BoolValue",
        _ => null
    };
}
