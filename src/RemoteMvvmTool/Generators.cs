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
        sb.AppendLine();
        sb.AppendLine($"public class {vmName}GrpcServiceImpl : {serviceName}.{serviceName}Base");
        sb.AppendLine("{");
        sb.AppendLine($"  private readonly {vmName} _vm;");
        sb.AppendLine($"  public {vmName}GrpcServiceImpl({vmName} vm) => _vm = vm;");
        sb.AppendLine($"  public override Task<{vmName}State> GetState(Empty request, ServerCallContext context)");
        sb.AppendLine("  {");
        sb.AppendLine($"    var state = new {vmName}State();");
        foreach (var p in props)
        {
            sb.AppendLine($"    state.{p.Name} = _vm.{p.Name};");
        }
        sb.AppendLine("    return Task.FromResult(state);");
        sb.AppendLine("  }");
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
