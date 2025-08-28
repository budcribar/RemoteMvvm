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

        // Collect all enum types used in properties and command parameters
        var enumTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        CollectEnumTypes(props, cmds, enumTypes);

        // Only import wrappers that actually exist in the protobuf library
        var availableWrappers = new HashSet<string> { "StringValue", "Int32Value", "Int64Value", "UInt32Value", "UInt64Value", "BoolValue", "FloatValue", "DoubleValue" };
        var wrapperImports = new HashSet<string>();
        
        foreach (var p in props)
        {
            var w = GeneratorHelpers.GetWrapperType(p.TypeString);
            if (w != null && w != "Timestamp" && availableWrappers.Contains(w)) 
                wrapperImports.Add(w);
        }

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

            string result = string.Empty;

            if (type is IArrayTypeSymbol arr)
            {
                result = MapTsType(arr.ElementType) + "[]";
            }
            else if (type is INamedTypeSymbol named)
            {
                if (named.IsGenericType)
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
                }

                if (string.IsNullOrEmpty(result) && GeneratorHelpers.TryGetEnumerableElementType(named, out var elem))
                {
                    result = MapTsType(elem!) + "[]";
                }

                if (string.IsNullOrEmpty(result))
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

        string BuildToObjectWithDates(string varName, INamedTypeSymbol type)
        {
            var members = Helpers.GetAllMembers(type)
                .OfType<IPropertySymbol>()
                .Where(m => m.GetMethod != null && m.Parameters.Length == 0 &&
                            GeneratorHelpers.GetProtoWellKnownTypeFor(m.Type) == "Timestamp")
                .ToList();
            if (members.Count == 0)
                return $"{varName}.toObject()";
            var sbLocal = new StringBuilder();
            sbLocal.Append("{ const obj = " + varName + ".toObject();");
            foreach (var m in members)
            {
                sbLocal.Append($" obj.{GeneratorHelpers.ToCamelCase(m.Name)} = {varName}.get{m.Name}()?.toDate();");
            }
            sbLocal.Append(" return obj; }");
            return sbLocal.ToString();
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

        string GenerateEnumMappings()
        {
            var enumSb = new StringBuilder();
            
            foreach (var enumType in enumTypes.OrderBy(e => e.ToDisplayString()))
            {
                var enumName = enumType.Name;
                enumSb.AppendLine($"// Enum mapping for {enumType.ToDisplayString()}");
                enumSb.AppendLine($"export const {enumName}Map: Record<number, string> = {{");
                
                var members = enumType.GetMembers().OfType<IFieldSymbol>()
                    .Where(f => f.HasConstantValue && f.IsStatic && f.DeclaredAccessibility == Accessibility.Public)
                    .ToList();
                
                for (int i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    var value = member.ConstantValue ?? 0;
                    var comma = i < members.Count - 1 ? "," : "";
                    enumSb.AppendLine($"  {value}: '{member.Name}'{comma}");
                }
                
                enumSb.AppendLine("};");
                enumSb.AppendLine();
                
                // Also generate a helper function to get enum display string
                enumSb.AppendLine($"export function get{enumName}Display(value: number): string {{");
                enumSb.AppendLine($"  return {enumName}Map[value] || value.toString();");
                enumSb.AppendLine("}");
                enumSb.AppendLine();
            }
            
            return enumSb.ToString();
        }

        var sb = new StringBuilder();
        GeneratorHelpers.AppendAutoGeneratedHeader(sb);
        
        var clientImportPath = serviceName + "ServiceClientPb";
            
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
        
        if (wrapperImports.Count > 0)
        {
            sb.AppendLine($"import {{ {string.Join(", ", wrapperImports.OrderBy(s => s))} }} from 'google-protobuf/google/protobuf/wrappers_pb';");
        }
        sb.AppendLine("import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';");
        sb.AppendLine();

        // Generate enum mappings
        var enumMappings = GenerateEnumMappings();
        if (!string.IsNullOrEmpty(enumMappings))
        {
            sb.AppendLine("// Enum Mappings");
            sb.Append(enumMappings);
        }

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
        bool hasReadOnly = props.Any(p => p.IsReadOnly);
        if (hasReadOnly)
        {
            var roList = string.Join(", ", props.Where(p => p.IsReadOnly).Select(p => $"'{p.Name}'"));
            sb.AppendLine($"    private readonly readOnlyProps = new Set<string>([{roList}]);");
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
                {
                    var mapExpr = BuildToObjectWithDates("v", elemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
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
                {
                    var mapExpr = BuildToObjectWithDates("v", arrElemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
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
                {
                    var mapExpr = BuildToObjectWithDates("v", memElemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!) == "Timestamp")
                expr = $"(state as any).get{p.Name}()?.toDate()";
            else if (p.FullTypeSymbol is INamedTypeSymbol nt &&
                     (nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Struct) &&
                     !GeneratorHelpers.IsWellKnownType(p.FullTypeSymbol!) &&
                     p.FullTypeSymbol!.TypeKind != TypeKind.Enum)
            {
                var objExpr = BuildToObjectWithDates("v", nt);
                expr = $"(() => {{ const v = (state as any).get{p.Name}(); return v ? {objExpr} : undefined; }})()";
            }
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
                {
                    var mapExpr = BuildToObjectWithDates("v", elemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
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
                {
                    var mapExpr = BuildToObjectWithDates("v", arrElemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
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
                {
                    var mapExpr = BuildToObjectWithDates("v", memElemNt);
                    expr = $"(state as any).get{p.Name}List().map((v:any) => {mapExpr})";
                }
                else
                    expr = $"(state as any).get{p.Name}List()";
            }
            else if (GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!) == "Timestamp")
                expr = $"(state as any).get{p.Name}()?.toDate()";
            else if (p.FullTypeSymbol is INamedTypeSymbol nt &&
                     (nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Struct) &&
                     !GeneratorHelpers.IsWellKnownType(p.FullTypeSymbol!) &&
                     p.FullTypeSymbol!.TypeKind != TypeKind.Enum)
            {
                var objExpr = BuildToObjectWithDates("v", nt);
                expr = $"(() => {{ const v = (state as any).get{p.Name}(); return v ? {objExpr} : undefined; }})()";
            }
            else
                expr = $"(state as any).get{p.Name}()";
            sb.AppendLine($"        this.{GeneratorHelpers.ToCamelCase(p.Name)} = {expr};");
        }
        sb.AppendLine("        this.notifyChange();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    async updatePropertyValue(propertyName: string, value: any): Promise<UpdatePropertyValueResponse> {");
        if (hasReadOnly)
        {
            sb.AppendLine("        if (this.readOnlyProps?.has(propertyName)) {");
            sb.AppendLine("            const res = new UpdatePropertyValueResponse();");
            sb.AppendLine("            res.setSuccess(false);");
            sb.AppendLine("            res.setErrorMessage(`Property ${propertyName} is read-only`);");
            sb.AppendLine("            return res;");
            sb.AppendLine("        }");
        }
        sb.AppendLine("        const req = new UpdatePropertyValueRequest();");
        sb.AppendLine("        req.setPropertyName(propertyName);");
        sb.AppendLine("        req.setArrayIndex(-1); // Default to -1 for non-array properties");
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
        if (hasReadOnly)
            sb.AppendLine("        if (this.readOnlyProps?.has(propertyName)) return;");
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
        sb.AppendLine("            const path = update.getPropertyPath();");
        sb.AppendLine("            if (path) {");
        sb.AppendLine("                const value = this.unpackAny(anyVal);");
        sb.AppendLine("                this.setByPath(this, path, value);");
        sb.AppendLine("            } else {");
        sb.AppendLine("            switch (update.getPropertyName()) {");
        foreach (var p in props)
        {
            var wrapper = GeneratorHelpers.GetWrapperType(p.TypeString);
            if (wrapper != null && availableWrappers.Contains(wrapper))
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
                if (!string.IsNullOrEmpty(unpack))
                {
                    sb.AppendLine($"                case '{p.Name}':");
                    if (wrapper == "Timestamp")
                        sb.AppendLine($"                    this.{GeneratorHelpers.ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.toDate();");
                    else
                        sb.AppendLine($"                    this.{GeneratorHelpers.ToCamelCase(p.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.getValue();");
                    sb.AppendLine("                    break;");
                }
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            }");
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
        sb.AppendLine("    private setByPath(target: any, path: string, value: any): void {");
        sb.AppendLine("        const parts = path.split('.');");
        sb.AppendLine("        let obj: any = target;");
        sb.AppendLine("        for (let i = 0; i < parts.length; i++) {");
        sb.AppendLine("            const m = /(\\w+)(?:\\[(\\d+)\\])?/.exec(parts[i]);");
        sb.AppendLine("            if (!m) return;");
        sb.AppendLine("            const key = m[1].charAt(0).toLowerCase() + m[1].slice(1);");
        sb.AppendLine("            const idx = m[2] !== undefined ? parseInt(m[2], 10) : undefined;");
        sb.AppendLine("            if (i === parts.length - 1) {");
        sb.AppendLine("                if (idx !== undefined) {");
        sb.AppendLine("                    if (Array.isArray(obj[key])) obj[key][idx] = value;");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    obj[key] = value;");
        sb.AppendLine("                }");
        sb.AppendLine("            } else {");
        sb.AppendLine("                obj = idx !== undefined ? obj[key][idx] : obj[key];");
        sb.AppendLine("                if (obj === undefined) return;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private unpackAny(anyVal: Any | undefined): any {");
        sb.AppendLine("        if (!anyVal) return undefined;");
        sb.AppendLine("        const typeUrl = anyVal.getTypeUrl();");
        sb.AppendLine("        switch (typeUrl) {");
        
        // Only add cases for available wrappers
        if (wrapperImports.Contains("StringValue"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.StringValue':");
            sb.AppendLine("                return anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();");
        }
        if (wrapperImports.Contains("Int32Value"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.Int32Value':");
            sb.AppendLine("                return anyVal.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();");
        }
        if (wrapperImports.Contains("Int64Value"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.Int64Value':");
            sb.AppendLine("                return Number(anyVal.unpack(Int64Value.deserializeBinary, 'google.protobuf.Int64Value')?.getValue());");
        }
        if (wrapperImports.Contains("UInt32Value"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.UInt32Value':");
            sb.AppendLine("                return anyVal.unpack(UInt32Value.deserializeBinary, 'google.protobuf.UInt32Value')?.getValue();");
        }
        if (wrapperImports.Contains("UInt64Value"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.UInt64Value':");
            sb.AppendLine("                return Number(anyVal.unpack(UInt64Value.deserializeBinary, 'google.protobuf.UInt64Value')?.getValue());");
        }
        if (wrapperImports.Contains("BoolValue"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.BoolValue':");
            sb.AppendLine("                return anyVal.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();");
        }
        if (wrapperImports.Contains("FloatValue"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.FloatValue':");
            sb.AppendLine("                return anyVal.unpack(FloatValue.deserializeBinary, 'google.protobuf.FloatValue')?.getValue();");
        }
        if (wrapperImports.Contains("DoubleValue"))
        {
            sb.AppendLine("            case 'type.googleapis.com/google.protobuf.DoubleValue':");
            sb.AppendLine("                return anyVal.unpack(DoubleValue.deserializeBinary, 'google.protobuf.DoubleValue')?.getValue();");
        }
        
        // Timestamp is always available
        sb.AppendLine("            case 'type.googleapis.com/google.protobuf.Timestamp':");
        sb.AppendLine("                return anyVal.unpack(Timestamp.deserializeBinary, 'google.protobuf.Timestamp')?.toDate();");
        
        sb.AppendLine("            default:");
        sb.AppendLine("                return undefined;");
        sb.AppendLine("        }");
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
        
        if (wrapperImports.Contains("StringValue"))
        {
            sb.AppendLine("            case 'string': {");
            sb.AppendLine("                const str = new StringValue();");
            sb.AppendLine("                str.setValue(value);");
            sb.AppendLine("                anyValue.pack(str.serializeBinary(), 'google.protobuf.StringValue');");
            sb.AppendLine("                return anyValue;");
            sb.AppendLine("            }");
        }
        
        if (wrapperImports.Contains("Int32Value") || wrapperImports.Contains("DoubleValue") || wrapperImports.Contains("FloatValue"))
        {
            sb.AppendLine("            case 'number': {");
            if (wrapperImports.Contains("Int32Value"))
            {
                sb.AppendLine("                if (Number.isInteger(value)) {");
                sb.AppendLine("                    const int32 = new Int32Value();");
                sb.AppendLine("                    int32.setValue(value);");
                sb.AppendLine("                    anyValue.pack(int32.serializeBinary(), 'google.protobuf.Int32Value');");
                sb.AppendLine("                    return anyValue;");
                sb.AppendLine("                }");
            }
            if (wrapperImports.Contains("FloatValue"))
            {
                if (wrapperImports.Contains("Int32Value"))
                    sb.AppendLine("                else if (Math.abs(value) <= 3.4028235e+38) { // Float range check");
                else
                    sb.AppendLine("                if (Math.abs(value) <= 3.4028235e+38) { // Float range check");
                sb.AppendLine("                    const float = new FloatValue();");
                sb.AppendLine("                    float.setValue(value);");
                sb.AppendLine("                    anyValue.pack(float.serializeBinary(), 'google.protobuf.FloatValue');");
                sb.AppendLine("                    return anyValue;");
                sb.AppendLine("                }");
            }
            if (wrapperImports.Contains("DoubleValue"))
            {
                var elsePart = (wrapperImports.Contains("Int32Value") || wrapperImports.Contains("FloatValue")) ? "else {" : "{";
                sb.AppendLine($"                {elsePart}");
                sb.AppendLine("                    const double = new DoubleValue();");
                sb.AppendLine("                    double.setValue(value);");
                sb.AppendLine("                    anyValue.pack(double.serializeBinary(), 'google.protobuf.DoubleValue');");
                sb.AppendLine("                    return anyValue;");
                sb.AppendLine("                }");
            }
            sb.AppendLine("            }");
        }
        
        if (wrapperImports.Contains("BoolValue"))
        {
            sb.AppendLine("            case 'boolean': {");
            sb.AppendLine("                const bool = new BoolValue();");
            sb.AppendLine("                bool.setValue(value);");
            sb.AppendLine("                anyValue.pack(bool.serializeBinary(), 'google.protobuf.BoolValue');");
            sb.AppendLine("                return anyValue;");
            sb.AppendLine("            }");
        }
        
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

    private static void CollectEnumTypes(List<PropertyInfo> props, List<CommandInfo> cmds, HashSet<INamedTypeSymbol> enumTypes)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        void AddTypes(ITypeSymbol type)
        {
            if (!visited.Add(type))
                return;

            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
            {
                enumTypes.Add(enumType);
                return;
            }

            if (type is IArrayTypeSymbol arr)
            {
                AddTypes(arr.ElementType);
                return;
            }

            if (type is INamedTypeSymbol named)
            {
                // Nullable<T>
                if (named.IsGenericType && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    AddTypes(named.TypeArguments[0]);
                    return;
                }

                // Dictionary<,>
                if (GeneratorHelpers.TryGetDictionaryTypeArgs(named, out var keyType, out var valType))
                {
                    if (keyType != null) AddTypes(keyType);
                    if (valType != null) AddTypes(valType);
                    return;
                }

                // Collections
                if (GeneratorHelpers.TryGetEnumerableElementType(named, out var elemType) && elemType != null)
                {
                    AddTypes(elemType);
                    return;
                }

                // Recurse into properties of non-system classes/structs
                if ((named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct) &&
                    !(named.ContainingNamespace?.ToDisplayString() ?? string.Empty).StartsWith("System"))
                {
                    foreach (var m in Helpers.GetAllMembers(named).OfType<IPropertySymbol>()
                                               .Where(p => p.GetMethod != null && p.Parameters.Length == 0))
                    {
                        AddTypes(m.Type);
                    }
                }
            }
        }

        foreach (var prop in props)
            AddTypes(prop.FullTypeSymbol!);

        foreach (var cmd in cmds)
            foreach (var param in cmd.Parameters)
                AddTypes(param.FullTypeSymbol!);
    }
}
