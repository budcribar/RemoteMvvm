using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class TypeScriptClientGenerator
{
    public static string Generate(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Auto-generated TypeScript client for {vmName}");
        sb.AppendLine($"import {{ {serviceName}Client }} from './generated/{serviceName}ServiceClientPb';");
        var requestTypes = string.Join(", ", cmds.Select(c => c.MethodName + "Request").Distinct());
        if (!string.IsNullOrWhiteSpace(requestTypes))
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, {requestTypes} }} from './generated/{serviceName}_pb.js';");
        }
        else
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus }} from './generated/{serviceName}_pb.js';");
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
            sb.AppendLine($"    {GeneratorHelpers.ToCamelCase(p.Name)}: any;");
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
            sb.AppendLine($"        this.{GeneratorHelpers.ToCamelCase(p.Name)} = (state as any).get{p.Name}();");
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
            sb.AppendLine($"        this.{GeneratorHelpers.ToCamelCase(p.Name)} = (state as any).get{p.Name}();");
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
            var paramList = string.Join(", ", cmd.Parameters.Select(p => GeneratorHelpers.ToCamelCase(p.Name) + ": any"));
            var reqType = cmd.MethodName + "Request";
            sb.AppendLine($"    async {GeneratorHelpers.ToCamelCase(cmd.MethodName)}({paramList}): Promise<void> {{");
            sb.AppendLine($"        const req = new {reqType}();");
            foreach (var p in cmd.Parameters)
            {
                sb.AppendLine($"        req.set{GeneratorHelpers.ToPascalCase(p.Name)}({GeneratorHelpers.ToCamelCase(p.Name)});");
            }
            sb.AppendLine($"        await this.grpcClient.{GeneratorHelpers.ToCamelCase(cmd.MethodName)}(req);");
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
            var wrapper = GeneratorHelpers.GetWrapperType(p.TypeString);
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
                sb.AppendLine($"                    this.{GeneratorHelpers.ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.getValue();");
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
}
