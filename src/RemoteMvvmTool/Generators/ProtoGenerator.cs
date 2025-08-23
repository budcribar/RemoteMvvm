using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteMvvmTool.Generators;

public static class ProtoGenerator
{
    public static string Generate(string protoNs, string serviceName, string vmName, List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
    {
        props ??= new List<PropertyInfo>();

        var body = new StringBuilder();
        var pendingMessages = new Queue<INamedTypeSymbol>();
        var processedMessages = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var mapEntryMessages = new Dictionary<string, (ITypeSymbol Key, ITypeSymbol Value)>();
        bool usesTimestamp = false;
        bool usesDuration = false;

        string MapProtoType(ITypeSymbol type, bool allowMessage)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                if (arrayType.Rank != 1) throw new NotSupportedException("Multi-dimensional arrays are not supported.");
                if (arrayType.ElementType.SpecialType == SpecialType.System_Byte)
                    return "bytes";
                string elementType = MapProtoType(arrayType.ElementType, allowMessage: true);
                return $"repeated {elementType}";
            }

            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                if (GeneratorHelpers.TryGetMemoryElementType(named, out var memElem))
                {
                    string elemProto = MapProtoType(memElem!, allowMessage: true);
                    if (memElem!.SpecialType == SpecialType.System_Byte)
                        return "bytes";
                    return $"repeated {elemProto}";
                }

                if (GeneratorHelpers.TryGetDictionaryTypeArgs(named, out var keyType, out var valueType))
                {
                    if (GeneratorHelpers.CanUseProtoMap(keyType!, valueType!))
                    {
                        string keyProto = MapProtoType(keyType!, allowMessage: false);
                        string valueProto = MapProtoType(valueType!, allowMessage: true);
                        return $"map<{keyProto}, {valueProto}>";
                    }
                    else
                    {
                        string entryName = GeneratorHelpers.GetDictionaryEntryName(keyType!, valueType!);
                        if (!mapEntryMessages.ContainsKey(entryName))
                            mapEntryMessages[entryName] = (keyType!, valueType!);
                        string valueProto = MapProtoType(valueType!, allowMessage: true);
                        _ = MapProtoType(keyType!, allowMessage: true);
                        return $"repeated {entryName}";
                    }
                }

                if (GeneratorHelpers.TryGetEnumerableElementType(named, out var elemType))
                {
                    string elemProto = MapProtoType(elemType!, allowMessage: true);
                    
                    // Check if the element type is a dictionary that would be mapped to a protobuf map
                    if (elemType is INamedTypeSymbol elemNamed && elemNamed.IsGenericType &&
                        GeneratorHelpers.TryGetDictionaryTypeArgs(elemNamed, out var elemKeyType, out var elemValueType))
                    {
                        if (GeneratorHelpers.CanUseProtoMap(elemKeyType!, elemValueType!))
                        {
                            // For collections of dictionaries, create a map-containing message
                            string dictMapName = GeneratorHelpers.GetDictionaryEntryName(elemKeyType!, elemValueType!) + "Map";
                            if (!mapEntryMessages.ContainsKey(dictMapName))
                            {
                                // Store a special marker to create a map-containing message
                                mapEntryMessages[dictMapName] = (elemKeyType!, elemValueType!);
                            }
                            return $"repeated {dictMapName}";
                        }
                        else
                        {
                            // For dictionaries that can't use proto maps, we already handle them with custom entries
                            string entryName = GeneratorHelpers.GetDictionaryEntryName(elemKeyType!, elemValueType!);
                            if (!mapEntryMessages.ContainsKey(entryName))
                                mapEntryMessages[entryName] = (elemKeyType!, elemValueType!);
                            return $"repeated {entryName}";
                        }
                    }
                    
                    return $"repeated {elemProto}";
                }
            }

            string wkt;
            if (type is INamedTypeSymbol nullableType && nullableType.IsGenericType &&
                nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var inner = nullableType.TypeArguments[0];
                string innerWkt = GeneratorHelpers.GetProtoWellKnownTypeFor(inner);
                switch (innerWkt)
                {
                    case "StringValue": return "google.protobuf.StringValue";
                    case "BoolValue": return "google.protobuf.BoolValue";
                    case "Int32Value": return "google.protobuf.Int32Value";
                    case "Int64Value": return "google.protobuf.Int64Value";
                    case "UInt32Value": return "google.protobuf.UInt32Value";
                    case "UInt64Value": return "google.protobuf.UInt64Value";
                    case "FloatValue": return "google.protobuf.FloatValue";
                    case "DoubleValue": return "google.protobuf.DoubleValue";
                }
                type = inner;
                wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(type);
            }
            else
            {
                wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(type);
            }
            switch (wkt)
            {
                case "StringValue": return "string";
                case "BoolValue": return "bool";
                case "Int32Value": return "int32";
                case "Int64Value": return "int64";
                case "UInt32Value": return "uint32";
                case "UInt64Value": return "uint64";
                case "FloatValue": return "float";
                case "DoubleValue": return "double";
                case "BytesValue": return "bytes";
                case "Timestamp":
                    usesTimestamp = true;
                    return "google.protobuf.Timestamp";
                case "Duration":
                    usesDuration = true;
                    return "google.protobuf.Duration";
            }

            if (type.TypeKind == TypeKind.Enum)
                return "int32";

            if (type.TypeKind == TypeKind.Error)
            {
                var enumMatch = compilation.GetSymbolsWithName(type.Name, SymbolFilter.Type)
                    .OfType<INamedTypeSymbol>()
                    .FirstOrDefault(s => s.ToDisplayString() == type.ToDisplayString() && s.TypeKind == TypeKind.Enum);
                if (enumMatch != null)
                    return "int32";
                Console.Error.WriteLine($"Warning: Type '{type.ToDisplayString()}' is not supported. Using 'int32'.");
                return "int32";
            }

            if (type is INamedTypeSymbol taskNamed)
            {
                var constructed = taskNamed.ConstructedFrom?.ToDisplayString();
                if (constructed != null && constructed.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal))
                {
                    if (taskNamed.TypeArguments.Length == 1)
                        return MapProtoType(taskNamed.TypeArguments[0], allowMessage: true);
                    return "google.protobuf.Any";
                }
            }

            if (allowMessage && type is INamedTypeSymbol namedType &&
                (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct || type.TypeKind == TypeKind.Error))
            {
                // Only allow user-defined types to be treated as messages; BCL types are unsupported
                var ns = namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (!ns.StartsWith("System", StringComparison.Ordinal))
                {
                    if (!processedMessages.Contains(namedType))
                    {
                        processedMessages.Add(namedType);
                        pendingMessages.Enqueue(namedType);
                    }
                    return namedType.Name + "State";
                }
            }

            throw new NotSupportedException($"Type '{type.ToDisplayString()}' is not supported.");
        }

        body.AppendLine($"// Message representing the full state of the {vmName}");
        body.AppendLine($"message {vmName}State {{");
        int field = 1;
        foreach (var p in props)
        {
            string protoType = MapProtoType(p.FullTypeSymbol!, allowMessage: true);
            body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(p.Name)} = {field++}; // Original C#: {p.TypeString} {p.Name}");
        }
        body.AppendLine("}");
        body.AppendLine();

        while (pendingMessages.Count > 0)
        {
            var msgType = pendingMessages.Dequeue();

            List<IPropertySymbol> propsForMsg = new();
            if (msgType.TypeKind != TypeKind.Error)
            {
                propsForMsg = Helpers.GetAllMembers(msgType)
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.IsStatic && p.GetMethod != null && p.Parameters.Length == 0)
                    .ToList();
            }

            body.AppendLine($"message {msgType.Name}State {{");
            int msgField = 1;
            foreach (var prop in propsForMsg)
            {
                string protoType = MapProtoType(prop.Type, allowMessage: true);
                body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(prop.Name)} = {msgField++}; // Original C#: {prop.Type.ToDisplayString()} {prop.Name}");
            }
            body.AppendLine("}");
            body.AppendLine();
        }

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
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal)
                ? c.MethodName[..^5]
                : c.MethodName;

            body.AppendLine();
            if (c.Parameters.Count == 0)
            {
                body.AppendLine($"message {baseName}Request {{}}");
            }
            else
            {
                body.AppendLine($"message {baseName}Request {{");
                int paramField = 1;
                foreach (var p in c.Parameters)
                {
                    string protoType = MapProtoType(p.FullTypeSymbol!, allowMessage: true);
                    body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(p.Name)} = {paramField++}; // Original C#: {p.TypeString} {p.Name}");
                }
                body.AppendLine("}");
            }
            body.AppendLine($"message {baseName}Response {{}}");
        }

        while (pendingMessages.Count > 0)
        {
            var msgType = pendingMessages.Dequeue();

            List<IPropertySymbol> propsForMsg = new();
            if (msgType.TypeKind != TypeKind.Error)
            {
                propsForMsg = Helpers.GetAllMembers(msgType)
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.IsStatic && p.GetMethod != null && p.Parameters.Length == 0)
                    .ToList();
            }

            body.AppendLine($"message {msgType.Name}State {{");
            int msgField = 1;
            foreach (var prop in propsForMsg)
            {
                string protoType = MapProtoType(prop.Type, allowMessage: true);
                body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(prop.Name)} = {msgField++}; // Original C#: {prop.Type.ToDisplayString()} {prop.Name}");
            }
            body.AppendLine("}");
            body.AppendLine();
        }

        var processedEntries = new HashSet<string>();
        while (processedEntries.Count < mapEntryMessages.Count)
        {
            foreach (var kvp in mapEntryMessages.ToList())
            {
                if (processedEntries.Contains(kvp.Key))
                    continue;
                processedEntries.Add(kvp.Key);
                
                // Check if this is a map-containing message (for collections of dictionaries)
                if (kvp.Key.EndsWith("Map"))
                {
                    // Create a message that contains a map field
                    string keyProto = MapProtoType(kvp.Value.Key, allowMessage: false);
                    string valueProto = MapProtoType(kvp.Value.Value, allowMessage: true);
                    body.AppendLine($"message {kvp.Key} {{");
                    body.AppendLine($"  map<{keyProto}, {valueProto}> entries = 1;");
                    body.AppendLine("}");
                }
                else
                {
                    // Regular entry message for non-map scenarios
                    string keyProto = MapProtoType(kvp.Value.Key, allowMessage: true);
                    string valueProto = MapProtoType(kvp.Value.Value, allowMessage: true);
                    body.AppendLine($"message {kvp.Key} {{");
                    body.AppendLine($"  {keyProto} key = 1;");
                    body.AppendLine($"  {valueProto} value = 2;");
                    body.AppendLine("}");
                }
                body.AppendLine();
            }
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
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal)
                ? c.MethodName[..^5]
                : c.MethodName;
            body.AppendLine($"  rpc {baseName} ({baseName}Request) returns ({baseName}Response);");
        }
        body.AppendLine("  rpc Ping (google.protobuf.Empty) returns (ConnectionStatusResponse);");
        body.AppendLine("}");
        string protoPackageName = Regex.Replace(protoNs.ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(protoPackageName) || !char.IsLetter(protoPackageName[0]))
        {
            protoPackageName = "generated_" + protoPackageName;
        }

        var headerSb = new StringBuilder();
        GeneratorHelpers.AppendAutoGeneratedHeader(headerSb);

        var optionalImports = new StringBuilder();
        if (usesTimestamp)
            optionalImports.AppendLine("import \"google/protobuf/timestamp.proto\";");
        if (usesDuration)
            optionalImports.AppendLine("import \"google/protobuf/duration.proto\";");
        optionalImports.AppendLine();

        var template = GeneratorHelpers.LoadTemplate("RemoteMvvmTool.Resources.ProtoTemplate.tmpl");
        return GeneratorHelpers.ReplacePlaceholders(template, new Dictionary<string, string>
        {
            ["<<AUTO_GENERATED_HEADER>>"] = headerSb.ToString().TrimEnd(),
            ["<<PACKAGE_NAME>>"] = protoPackageName,
            ["<<PROTO_NS>>"] = protoNs,
            ["<<OPTIONAL_IMPORTS>>"] = optionalImports.ToString(),
            ["<<BODY>>"] = body.ToString(),
        });
    }

}
