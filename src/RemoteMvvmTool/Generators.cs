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
        body.AppendLine("service " + serviceName + " {");
        body.AppendLine($"  rpc GetState (google.protobuf.Empty) returns ({vmName}State);");
        body.AppendLine($"  rpc UpdatePropertyValue (UpdatePropertyValueRequest) returns (google.protobuf.Empty);");
        body.AppendLine($"  rpc SubscribeToPropertyChanges (google.protobuf.Empty) returns (stream PropertyChangeNotification);");
        foreach (var c in cmds)
        {
            body.AppendLine($"  rpc {c.MethodName} ({c.MethodName}Request) returns ({c.MethodName}Response);");
        }
        body.AppendLine("  rpc Ping (google.protobuf.Empty) returns (google.protobuf.Empty);");
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
        sb.AppendLine("export class " + vmName + "RemoteClient {");
        foreach (var p in props)
        {
            sb.AppendLine($"  {ToCamel(p.Name)}: any;");
        }
        sb.AppendLine("  constructor(public grpcClient: any) {}");
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
}
