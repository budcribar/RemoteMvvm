using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

public static class Generators
{
    public static string GenerateProto(string protoNs, string serviceName, string vmName, List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
    {
        var body = new StringBuilder();
        body.AppendLine($"// Message representing the full state of the {vmName}");
        body.AppendLine($"message {vmName}State {{");
        int field = 1;
        foreach (var p in props)
        {
            string wkt = GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
            string protoType = wkt switch
            {
                "StringValue" => "string",
                "BoolValue" => "bool",
                "Int32Value" => "int32",
                "Int64Value" => "int64",
                "UInt32Value" => "uint32",
                "UInt64Value" => "uint64",
                "FloatValue" => "float",
                "DoubleValue" => "double",
                "BytesValue" => "bytes",
                "Timestamp" => "google.protobuf.Timestamp",
                "Duration" => "google.protobuf.Duration",
                _ => "string"
            };
            body.AppendLine($"  {protoType} {ToSnake(p.Name)} = {field++}; // Original C#: {p.TypeString} {p.Name}");
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
        final.AppendLine();

        string protoPackageName = Regex.Replace(protoNs.ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(protoPackageName) || !char.IsLetter(protoPackageName[0]))
        {
            protoPackageName = "generated_" + protoPackageName;
        }
        final.AppendLine($"package {protoPackageName};");
        final.AppendLine();

        final.AppendLine($"option csharp_namespace = \"{protoNs}\";");
        final.AppendLine();
        final.AppendLine("import \"google/protobuf/any.proto\";");
        final.AppendLine("import \"google/protobuf/empty.proto\";");
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

    public static string GenerateViewModelPartial(string vmName, string protoNs, string serviceName, string vmNamespace, string clientNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {clientNamespace};");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Windows.Threading;");
        sb.AppendLine($"using PeakSWC.Mvvm.Remote;");
        sb.AppendLine();
        sb.AppendLine($"namespace {vmNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {vmName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private {vmName}GrpcServiceImpl? _grpcService;");
        sb.AppendLine("        private Grpc.Core.Server? _server;");
        sb.AppendLine("        private GrpcChannel? _channel;");
        sb.AppendLine($"        private {clientNamespace}.{vmName}RemoteClient? _remoteClient;");
        sb.AppendLine();
        sb.AppendLine($"        public {vmName}(ServerOptions options) : this()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options == null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine($"            _grpcService = new {vmName}GrpcServiceImpl(this, Dispatcher.CurrentDispatcher);");
        sb.AppendLine("            _server = new Grpc.Core.Server");
        sb.AppendLine("            {");
        sb.AppendLine($"                Services = {{ {serviceName}.BindService(_grpcService) }},");
        sb.AppendLine("                Ports = { new ServerPort(\"localhost\", options.Port, ServerCredentials.Insecure) }" + (!false ? string.Empty : string.Empty));
        sb.AppendLine("            };");
        sb.AppendLine("            _server.Start();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        public {vmName}(ClientOptions options) : this()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options == null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine("            _channel = GrpcChannel.ForAddress(options.Address);");
        sb.AppendLine($"            var client = new {serviceName}.{serviceName}Client(_channel);");
        sb.AppendLine($"            _remoteClient = new {vmName}RemoteClient(client);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        public async Task<{vmName}RemoteClient> GetRemoteModel()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_remoteClient == null) throw new InvalidOperationException(\"Client options not provided\");");
        sb.AppendLine("            await _remoteClient.InitializeRemoteAsync();");
        sb.AppendLine("            return _remoteClient;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateOptions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace PeakSWC.Mvvm.Remote");
        sb.AppendLine("{");
        sb.AppendLine("    public class ServerOptions");
        sb.AppendLine("    {");
        sb.AppendLine("        public int Port { get; set; } = MonsterClicker.NetworkConfig.Port;");
        sb.AppendLine("        public bool UseHttps { get; set; } = true;");
        sb.AppendLine("        public string? CorsPolicyName { get; set; } = \"AllowAll\";");
        sb.AppendLine("        public string[]? AllowedOrigins { get; set; } = null;");
        sb.AppendLine("        public string[]? AllowedHeaders { get; set; } = null;");
        sb.AppendLine("        public string[]? AllowedMethods { get; set; } = null;");
        sb.AppendLine("        public string[]? ExposedHeaders { get; set; } = null;");
        sb.AppendLine("        public string? LogLevel { get; set; } = \"Debug\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public class ClientOptions");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Address { get; set; } = MonsterClicker.NetworkConfig.ServerAddress;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateServer(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds, string viewModelNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {viewModelNamespace};");
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
        sb.AppendLine("using Channel = System.Threading.Channels.Channel;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
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
        sb.AppendLine("    private readonly ILogger? _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {vmName}GrpcServiceImpl({vmName} viewModel, Dispatcher dispatcher, ILogger<{vmName}GrpcServiceImpl>? logger = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
        sb.AppendLine("        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));");
        sb.AppendLine("        _logger = logger;");
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
        sb.AppendLine("                    else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Unpacking not implemented for property \" + request.PropertyName + \" and type \" + request.NewValue.TypeUrl + \".\"); }");
        sb.AppendLine("                } catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error setting property \" + request.PropertyName + \": \" + ex.Message); }");
        sb.AppendLine("            }");
        sb.AppendLine("            else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Property \" + request.PropertyName + \" not found or not writable.\"); }");
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
        sb.AppendLine("        catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error getting property value for \" + e.PropertyName + \": \" + ex.Message); return; }");
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
        sb.AppendLine("            catch (ChannelClosedException) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Channel closed for a subscriber, cannot write notification for '\" + e.PropertyName + \"'. Subscriber likely disconnected.\"); }");
        sb.AppendLine("            catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error writing to subscriber channel for '\" + e.PropertyName + \"': \" + ex.Message); }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateClient(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds, string? clientNamespace = null)
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
            string backingFieldName = $"_{LowercaseFirst(prop.Name)}";
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
            string protoStateFieldName = ToPascalCase(prop.Name);
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
            string protoStateFieldName = ToPascalCase(prop.Name);
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
            string paramListWithType = string.Join(", ", cmd.Parameters.Select(p => $"{p.TypeString} {LowercaseFirst(p.Name)}"));
            string requestCreation = $"new {protoNs}.{cmd.MethodName}Request()";
            if (cmd.Parameters.Any())
            {
                var paramAssignments = cmd.Parameters.Select(p => $"{ToPascalCase(p.Name)} = {LowercaseFirst(p.Name)}");
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
            string wkt = GetProtoWellKnownTypeFor(prop.FullTypeSymbol!);
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
    private static string LowercaseFirst(string str) => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str[1..];
    private static string GetProtoWellKnownTypeFor(ITypeSymbol typeSymbol)
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
                return "BytesValue";
        }
        return "Any";
    }
}
