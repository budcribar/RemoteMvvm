using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteMvvmTool.Generators;

public static class ProtoGenerator
{
    public static string Generate(string protoNs, string serviceName, string vmName, List<PropertyInfo> props, List<CommandInfo> cmds, Compilation compilation)
    {
        var body = new StringBuilder();
        body.AppendLine($"// Message representing the full state of the {vmName}");
        body.AppendLine($"message {vmName}State {{");
        int field = 1;
        foreach (var p in props)
        {
            string wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
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
            body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(p.Name)} = {field++}; // Original C#: {p.TypeString} {p.Name}");
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
            if (c.Parameters.Count == 0)
            {
                body.AppendLine($"message {c.MethodName}Request {{}}");
            }
            else
            {
                body.AppendLine($"message {c.MethodName}Request {{");
                int paramField = 1;
                foreach (var p in c.Parameters)
                {
                    string wkt = GeneratorHelpers.GetProtoWellKnownTypeFor(p.FullTypeSymbol!);
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
                    body.AppendLine($"  {protoType} {GeneratorHelpers.ToSnake(p.Name)} = {paramField++}; // Original C#: {p.TypeString} {p.Name}");
                }
                body.AppendLine("}");
            }
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
}
