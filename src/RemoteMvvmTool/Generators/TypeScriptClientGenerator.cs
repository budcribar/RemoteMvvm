using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class TypeScriptClientGenerator
{
    public static string Generate(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        foreach (var p in props)
        {
            var wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
            if (wkt == "Duration")
                throw new NotSupportedException($"Property '{p.Name}' with type '{p.TypeString}' is not supported by the TypeScript client generator.");
        }
        foreach (var cmd in cmds)
        {
            foreach (var p in cmd.Parameters)
            {
                var wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
                if (wkt == "Duration")
                    throw new NotSupportedException($"Parameter '{p.Name}' of command '{cmd.MethodName}' uses unsupported type '{p.TypeString}'.");
            }
        }

        var processed = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();
        var ifaceSb = new StringBuilder();

        string GetStateName(INamedTypeSymbol t)
        {
            var name = t.Name;
            if (name.EndsWith("ComponentViewModel"))
                name = name[..^"ComponentViewModel".Length];
            else if (name.EndsWith("ViewModel"))
                name = name[..^"ViewModel".Length];
            else if (name.EndsWith("Model"))
                name = name[..^"Model".Length];
            return name + "State";
        }

        string MapTsType(ITypeSymbol type)
        {
            bool isNullable = false;
            if (type is INamedTypeSymbol nullable && nullable.IsGenericType &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                isNullable = true;
                type = nullable.TypeArguments[0];
            }

            string result;

            if (type is IArrayTypeSymbol arr)
            {
                result = MapTsType(arr.ElementType) + "[]";
            }
            else if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                if (named.ConstructedFrom.ToDisplayString() == "System.Collections.ObjectModel.ObservableCollection<T>")
                {
                    result = MapTsType(named.TypeArguments[0]) + "[]";
                }
                else if (GeneratorHelpers.TryGetDictionaryTypeArgs(named, out var key, out var val))
                {
                    var keyTs = MapKeyType(key!);
                    var valTs = MapTsType(val!);
                    result = $"Record<{keyTs}, {valTs}>";
                }
                else if (GeneratorHelpers.TryGetMemoryElementType(named, out var memElem))
                {
                    result = MapTsType(memElem!) + "[]";
                }
                else if (GeneratorHelpers.TryGetEnumerableElementType(named, out var elem))
                {
                    result = MapTsType(elem!) + "[]";
                }
                else
                {
                    var wktNamed = GeneratorHelpers.GetProtoWellKnownTypeFor(named);
                    result = wktNamed switch
                    {
                        "StringValue" => "string",
                        "BoolValue" => "boolean",
                        "Int32Value" or "Int64Value" or "UInt32Value" or "UInt64Value" or "FloatValue" or "DoubleValue" => "number",
                        "Timestamp" => "Date",
                        "Duration" => "number",
                        _ => null
                    } ?? string.Empty;
                    if (string.IsNullOrEmpty(result))
                    {
                        if (named.TypeKind == TypeKind.Enum)
                        {
                            result = "number";
                        }
                        else if ((named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct) &&
                                 !(named.ContainingNamespace?.ToDisplayString() ?? string.Empty).StartsWith("System"))
                        {
                            if (processed.Add(named)) queue.Enqueue(named);
                            result = GetStateName(named);
                        }
                        else
                        {
                            result = "any";
                        }
                    }
                }
            }
            else
            {
                var wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(type);
                result = wkt switch
                {
                    "StringValue" => "string",
                    "BoolValue" => "boolean",
                    "Int32Value" or "Int64Value" or "UInt32Value" or "UInt64Value" or "FloatValue" or "DoubleValue" => "number",
                    "Timestamp" => "Date",
                    "Duration" => "number",
                    _ => null
                } ?? string.Empty;

                if (string.IsNullOrEmpty(result))
                {
                    if (type.TypeKind == TypeKind.Enum)
                    {
                        result = "number";
                    }
                    else if (type is INamedTypeSymbol nt &&
                             (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
                             !(nt.ContainingNamespace?.ToDisplayString() ?? string.Empty).StartsWith("System"))
                    {
                        if (processed.Add(nt)) queue.Enqueue(nt);
                        result = GetStateName(nt);
                    }
                    else
                    {
                        result = "any";
                    }
                }
            }

            if (isNullable)
                result += " | undefined";
            return result;
        }

        string MapKeyType(ITypeSymbol type)
        {
            var wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(type);
            return wkt == "StringValue" ? "string" : "number";
        }

        void EnqueuePropertyTypes()
        {
            foreach (var p in props)
            {
                MapTsType(p.FullTypeSymbol); // populates queue via side-effects
            }
        }

        void GenerateInterfaces()
        {
            EnqueuePropertyTypes();
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                var name = GetStateName(t);
                ifaceSb.AppendLine($"export interface {name} {{");
                var members = Helpers.GetAllMembers(t)
                    .OfType<IPropertySymbol>()
                    .Where(p => p.GetMethod != null && p.Parameters.Length == 0);
                foreach (var m in members)
                {
                    ifaceSb.AppendLine($"  {GeneratorHelpers.ToCamelCase(m.Name)}: {MapTsType(m.Type)};");
                }
                ifaceSb.AppendLine("}");
                ifaceSb.AppendLine();
            }
        }

        var sb = new StringBuilder();
        GeneratorHelpers.AppendAutoGeneratedHeader(sb);
        
        // Fix the duplicate "Service" issue in the import path
        var clientImportPath = serviceName.EndsWith("Service") 
            ? serviceName.Substring(0, serviceName.Length - "Service".Length) + "ServiceClientPb"
            : serviceName + "ServiceClientPb";
            
        sb.AppendLine($"import {{ {serviceName}Client }} from './generated/{clientImportPath}';");
        var requestTypes = string.Join(", ", cmds.Select(c =>
        {
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal)
                ? c.MethodName[..^5]
                : c.MethodName;
            return baseName + "Request";
        }).Distinct());
        if (!string.IsNullOrWhiteSpace(requestTypes))
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, UpdatePropertyValueResponse, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, {requestTypes} }} from './generated/{serviceName}_pb.js';");
        }
        else
        {
            sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, UpdatePropertyValueResponse, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus }} from './generated/{serviceName}_pb.js';");
        }
        sb.AppendLine("import * as grpcWeb from 'grpc-web';");
        sb.AppendLine("import { Empty } from 'google-protobuf/google/protobuf/empty_pb';");
        sb.AppendLine("import { Any } from 'google-protobuf/google/protobuf/any_pb';");
        var wrapperImports = new HashSet<string> { "StringValue", "Int32Value", "Int64Value", "BoolValue", "DoubleValue" };
        foreach (var p in props)
        {
            var w = GeneratorHelpers.GetWrapperType(p.TypeString);
            if (w != null && w != "Timestamp") wrapperImports.Add(w);
        }
        sb.AppendLine($"import {{ {string.Join(", ", wrapperImports.OrderBy(s => s))} }} from 'google-protobuf/google/protobuf/wrappers_pb';");
        sb.AppendLine("import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';");
        sb.AppendLine();

        // generate state interfaces for complex types
        GenerateInterfaces();
        sb.Append(ifaceSb.ToString());

        sb.AppendLine($"export class {vmName}RemoteClient {{");
        sb.AppendLine($"    private readonly grpcClient: {serviceName}Client;");
        sb.AppendLine("    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;");
        sb.AppendLine("    private pingIntervalId?: any;");
        sb.AppendLine("    private changeCallbacks: Array<() => void> = [];");
        sb.AppendLine("    private updateDebounceMap = new Map<string, any>(); // Internal debouncing (any for cross-platform compatibility)");
        sb.AppendLine();
        foreach (var p in props)
        {
            sb.AppendLine($"    {GeneratorHelpers.ToCamelCase(p.Name)}: {MapTsType(p.FullTypeSymbol)};");
        }
        sb.AppendLine("    connectionStatus: string = 'Unknown';");
        sb.AppendLine();
        sb.AppendLine("    addChangeListener(cb: ((isFromServer?: boolean) => void) | (() => void)): void {");
        sb.AppendLine("        this.changeCallbacks.push(cb as () => void);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private notifyChange(isFromServer: boolean = false): void {");
        sb.AppendLine("        this.changeCallbacks.forEach(cb => {");
        sb.AppendLine("            try {");
        sb.AppendLine("                // Pass the server flag to callbacks that support it");
        sb.AppendLine("                if (cb.length > 0) {");
        sb.AppendLine("                    (cb as any)(isFromServer);");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    cb();");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch (err) {");
        sb.AppendLine("                console.warn('Error in change callback:', err);");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
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
            string expr;
            if (GeneratorHelpers.TryGetDictionaryTypeArgs(p.FullTypeSymbol!, out _, out _))
                expr = $"(state as any).get{p.Name}Map().toObject()";
            else if (GeneratorHelpers.TryGetEnumerableElementType(p.FullTypeSymbol!, out var elem))
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(elem!) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (elem is INamedTypeSymbol elemNt &&
                         (elemNt.TypeKind == TypeKind.Class || elemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(elem!) &&
                         elem!.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (p.FullTypeSymbol is IArrayTypeSymbol arr)
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(arr.ElementType) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (arr.ElementType is INamedTypeSymbol arrElemNt &&
                         (arrElemNt.TypeKind == TypeKind.Class || arrElemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(arr.ElementType) &&
                         arr.ElementType.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.TryGetMemoryElementType(p.FullTypeSymbol!, out var memElem))
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(memElem!) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (memElem is INamedTypeSymbol memElemNt &&
                         (memElemNt.TypeKind == TypeKind.Class || memElemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(memElem!) &&
                         memElem!.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!) == "Timestamp")
                expr = $"(state as any).get{p.Name}()?.toDate()";
            else if (p.FullTypeSymbol is INamedTypeSymbol nt &&
                     (nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Struct) &&
                     !GeneratorHelpers.IsWellKnownType(p.FullTypeSymbol!) &&
                     p.FullTypeSymbol!.TypeKind != TypeKind.Enum)
                expr = $"(state as any).get{p.Name}()?.toObject()";
            else
                expr = $"(state as any).get{p.Name}()";
            sb.AppendLine($"        this.{GeneratorHelpers.ToCamelCase(p.Name)} = {expr};");
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
            string expr;
            if (GeneratorHelpers.TryGetDictionaryTypeArgs(p.FullTypeSymbol!, out _, out _))
                expr = $"(state as any).get{p.Name}Map().toObject()";
            else if (GeneratorHelpers.TryGetEnumerableElementType(p.FullTypeSymbol!, out var elem))
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(elem!) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (elem is INamedTypeSymbol elemNt &&
                         (elemNt.TypeKind == TypeKind.Class || elemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(elem!) &&
                         elem!.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (p.FullTypeSymbol is IArrayTypeSymbol arr)
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(arr.ElementType) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (arr.ElementType is INamedTypeSymbol arrElemNt &&
                         (arrElemNt.TypeKind == TypeKind.Class || arrElemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(arr.ElementType) &&
                         arr.ElementType.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.TryGetMemoryElementType(p.FullTypeSymbol!, out var memElem))
            {
                if (GeneratorHelpers.GetProtoWellKnownTypeFor(memElem!) == "Timestamp")
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toDate())";
                else if (memElem is INamedTypeSymbol memElemNt &&
                         (memElemNt.TypeKind == TypeKind.Class || memElemNt.TypeKind == TypeKind.Struct) &&
                         !GeneratorHelpers.IsWellKnownType(memElem!) &&
                         memElem!.TypeKind != TypeKind.Enum)
                    expr = $"(state as any).get{p.Name}List().map((v:any) => v.toObject())";
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!) == "Timestamp")
                expr = $"(state as any).get{p.Name}()?.toDate()";
            else if (p.FullTypeSymbol is INamedTypeSymbol nt &&
                     (nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Struct) &&
                     !GeneratorHelpers.IsWellKnownType(p.FullTypeSymbol!) &&
                     p.FullTypeSymbol!.TypeKind != TypeKind.Enum)
                expr = $"(state as any).get{p.Name}()?.toObject()";
            else
                expr = $"(state as any).get{p.Name}()";
            sb.AppendLine($"        this.{GeneratorHelpers.ToCamelCase(p.Name)} = {expr};");
        }
        sb.AppendLine("        this.notifyChange();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    async updatePropertyValue(propertyName: string, value: any): Promise<UpdatePropertyValueResponse> {");
        sb.AppendLine("        const req = new UpdatePropertyValueRequest();");
        sb.AppendLine("        req.setPropertyName(propertyName);");
        sb.AppendLine("        req.setNewValue(this.createAnyValue(value));");
        sb.AppendLine("        const response = await this.grpcClient.updatePropertyValue(req);");
        sb.AppendLine("        ");
        sb.AppendLine("        // If the response indicates success, update the local property value");
        sb.AppendLine("        if (typeof response.getSuccess === 'function' && response.getSuccess()) {");
        sb.AppendLine("            this.updateLocalProperty(propertyName, value);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        return response;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Debounced property update to prevent rapid-fire server calls");
        sb.AppendLine("    updatePropertyValueDebounced(propertyName: string, value: any, delayMs: number = 200): void {");
        sb.AppendLine("        // Clear existing timeout for this property");
        sb.AppendLine("        const existingTimeout = this.updateDebounceMap.get(propertyName);");
        sb.AppendLine("        if (existingTimeout) {");
        sb.AppendLine("            clearTimeout(existingTimeout);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        // Set new timeout");
        sb.AppendLine("        const timeout = setTimeout(async () => {");
        sb.AppendLine("            try {");
        sb.AppendLine("                console.log(`Sending debounced update: ${propertyName} = ${value}`);");
        sb.AppendLine("                await this.updatePropertyValue(propertyName, value);");
        sb.AppendLine("            } catch (err) {");
        sb.AppendLine("                console.error(`Error updating ${propertyName}:`, err);");
        sb.AppendLine("            } finally {");
        sb.AppendLine("                this.updateDebounceMap.delete(propertyName);");
        sb.AppendLine("            }");
        sb.AppendLine("        }, delayMs);");
        sb.AppendLine("        ");
        sb.AppendLine("        this.updateDebounceMap.set(propertyName, timeout);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Enhanced updatePropertyValue with support for complex scenarios");
        sb.AppendLine("    async updatePropertyValueAdvanced(");
        sb.AppendLine("        propertyName: string, ");
        sb.AppendLine("        value: any, ");
        sb.AppendLine("        options?: {");
        sb.AppendLine("            propertyPath?: string;");
        sb.AppendLine("            collectionKey?: string;");
        sb.AppendLine("            arrayIndex?: number;");
        sb.AppendLine("            operationType?: 'set' | 'add' | 'remove' | 'clear' | 'insert';");
        sb.AppendLine("        }");
        sb.AppendLine("    ): Promise<UpdatePropertyValueResponse> {");
        sb.AppendLine("        const req = new UpdatePropertyValueRequest();");
        sb.AppendLine("        req.setPropertyName(propertyName);");
        sb.AppendLine("        req.setNewValue(this.createAnyValue(value));");
        sb.AppendLine("        ");
        sb.AppendLine("        if (options?.propertyPath) req.setPropertyPath(options.propertyPath);");
        sb.AppendLine("        if (options?.collectionKey) req.setCollectionKey(options.collectionKey);");
        sb.AppendLine("        if (options?.arrayIndex !== undefined) req.setArrayIndex(options.arrayIndex);");
        sb.AppendLine("        if (options?.operationType) req.setOperationType(options.operationType);");
        sb.AppendLine("        ");
        sb.AppendLine("        const response = await this.grpcClient.updatePropertyValue(req);");
        sb.AppendLine("        ");
        sb.AppendLine("        // If the response indicates success, update the local property value");
        sb.AppendLine("        if (typeof response.getSuccess === 'function' && response.getSuccess()) {");
        sb.AppendLine("            this.updateLocalProperty(propertyName, value);");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        return response;");
        sb.AppendLine("    }");
        sb.AppendLine();
        foreach (var cmd in cmds)
        {
            var paramList = string.Join(", ", cmd.Parameters.Select(p => GeneratorHelpers.ToCamelCase(p.Name) + ": any"));
            var baseName = cmd.MethodName.EndsWith("Async", StringComparison.Ordinal)
                ? cmd.MethodName[..^5]
                : cmd.MethodName;
            var reqType = baseName + "Request";
            sb.AppendLine($"    async {GeneratorHelpers.ToCamelCase(cmd.MethodName)}({paramList}): Promise<void> {{");
            sb.AppendLine($"        const req = new {reqType}();");
            foreach (var p in cmd.Parameters)
            {
                var wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
                if (wkt == "Timestamp")
                    sb.AppendLine($"        req.set{GeneratorHelpers.ToPascalCase(p.Name)}(Timestamp.fromDate({GeneratorHelpers.ToCamelCase(p.Name)}));");
                else
                    sb.AppendLine($"        req.set{GeneratorHelpers.ToPascalCase(p.Name)}({GeneratorHelpers.ToCamelCase(p.Name)});");
            }
            sb.AppendLine($"        await this.grpcClient.{GeneratorHelpers.ToCamelCase(baseName)}(req);");
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
                    "Int64Value" => "Int64Value.deserializeBinary",
                    "UInt32Value" => "UInt32Value.deserializeBinary",
                    "UInt64Value" => "UInt64Value.deserializeBinary",
                    "BoolValue" => "BoolValue.deserializeBinary",
                    "FloatValue" => "FloatValue.deserializeBinary",
                    "DoubleValue" => "DoubleValue.deserializeBinary",
                    "Timestamp" => "Timestamp.deserializeBinary",
                    _ => ""
                };
                sb.AppendLine($"                case '{p.Name}':");
                if (wrapper == "Timestamp")
                    sb.AppendLine($"                    this.{GeneratorHelpers.ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.toDate();");
                else
                    sb.AppendLine($"                    this.{GeneratorHelpers.ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.getValue();");
                sb.AppendLine("                    break;");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            // Notify with server flag - UI can update but won't send back to server");
        sb.AppendLine("            this.notifyChange(true);");
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
        sb.AppendLine("    private createAnyValue(value: any): Any {");
        sb.AppendLine("        if (value == null) {");
        sb.AppendLine("            const empty = new Empty();");
        sb.AppendLine("            const anyValue = new Any();");
        sb.AppendLine("            anyValue.pack(empty.serializeBinary(), 'google.protobuf.Empty');");
        sb.AppendLine("            return anyValue;");
        sb.AppendLine("        }");
        sb.AppendLine("        ");
        sb.AppendLine("        const anyValue = new Any();");
        sb.AppendLine("        ");
        sb.AppendLine("        switch (typeof value) {");
        sb.AppendLine("            case 'string': {");
        sb.AppendLine("                const str = new StringValue();");
        sb.AppendLine("                str.setValue(value);");
        sb.AppendLine("                anyValue.pack(str.serializeBinary(), 'google.protobuf.StringValue');");
        sb.AppendLine("                return anyValue;");
        sb.AppendLine("            }");
        sb.AppendLine("            case 'number': {");
        sb.AppendLine("                if (Number.isInteger(value)) {");
        sb.AppendLine("                    const int32 = new Int32Value();");
        sb.AppendLine("                    int32.setValue(value);");
        sb.AppendLine("                    anyValue.pack(int32.serializeBinary(), 'google.protobuf.Int32Value');");
        sb.AppendLine("                    return anyValue;");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    const double = new DoubleValue();");
        sb.AppendLine("                    double.setValue(value);");
        sb.AppendLine("                    anyValue.pack(double.serializeBinary(), 'google.protobuf.DoubleValue');");
        sb.AppendLine("                    return anyValue;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            case 'boolean': {");
        sb.AppendLine("                const bool = new BoolValue();");
        sb.AppendLine("                bool.setValue(value);");
        sb.AppendLine("                anyValue.pack(bool.serializeBinary(), 'google.protobuf.BoolValue');");
        sb.AppendLine("                return anyValue;");
        sb.AppendLine("            }");
        sb.AppendLine("            default: {");
        sb.AppendLine("                const empty = new Empty();");
        sb.AppendLine("                anyValue.pack(empty.serializeBinary(), 'google.protobuf.Empty');");
        sb.AppendLine("                return anyValue;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
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
        sb.AppendLine("        // Clear any pending debounced updates");
        sb.AppendLine("        this.updateDebounceMap.forEach((timeout) => clearTimeout(timeout));");
        sb.AppendLine("        this.updateDebounceMap.clear();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private updateLocalProperty(propertyName: string, value: any): void {");
        sb.AppendLine("        const camelCasePropertyName = this.toCamelCase(propertyName);");
        sb.AppendLine("        ");
        sb.AppendLine("        // Update the local property if it exists");
        sb.AppendLine("        if (camelCasePropertyName in this) {");
        sb.AppendLine("            (this as any)[camelCasePropertyName] = value;");
        sb.AppendLine("            this.notifyChange();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private toCamelCase(str: string): string {");
        sb.AppendLine("        return str.charAt(0).toLowerCase() + str.slice(1);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
