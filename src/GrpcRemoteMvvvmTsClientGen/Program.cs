using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GrpcRemoteMvvmTsClientGen
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: grpc-remote-mvvm-ts-client <inputViewModel.cs> <outputDir>");
                return;
            }

            var viewModelFile = args[0];
            var outputDir = args[1];

            if (!File.Exists(viewModelFile))
            {
                Console.WriteLine($"File not found: {viewModelFile}");
                return;
            }

            Directory.CreateDirectory(outputDir);

            // Use the shared analyzer to load the ViewModel
            var (vmSymbol, vmName, properties, commands, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { viewModelFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute",
                Array.Empty<string>()
            );

            if (vmSymbol == null)
            {
                Console.WriteLine("No ViewModel with [GenerateGrpcRemote] attribute found.");
                return;
            }

            // Extract attribute information for proto namespace and service name
            var attr = vmSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ==
                "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute");

            if (attr == null || attr.ConstructorArguments.Length < 2)
            {
                Console.WriteLine("GenerateGrpcRemoteAttribute with required arguments not found.");
                return;
            }

            var protoNamespace = attr.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
            var serviceName = attr.ConstructorArguments[1].Value?.ToString() ?? string.Empty;

            // Generate TypeScript client code
            var tsCode = GenerateTypeScriptClient(vmName, protoNamespace, serviceName, properties, commands);
            var outFile = Path.Combine(outputDir, $"{vmName}RemoteClient.ts");
            await File.WriteAllTextAsync(outFile, tsCode);
            Console.WriteLine($"TypeScript client generated: {outFile}");
        }

        static string GenerateTypeScriptClient(string vmName, string protoNamespace, string serviceName, System.Collections.Generic.List<GrpcRemoteMvvmModelUtil.PropertyInfo> properties, System.Collections.Generic.List<GrpcRemoteMvvmModelUtil.CommandInfo> commands)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"// Auto-generated TypeScript client for {vmName}");
            sb.AppendLine($"import {{ {serviceName}Client, {vmName}State, UpdatePropertyValueRequest, SubscribeRequest }} from './protos/{serviceName}';");
            sb.AppendLine("import { Empty } from 'google-protobuf/google/protobuf/empty_pb';");
            sb.AppendLine();
            sb.AppendLine($"export class {vmName}RemoteClient {{");
            sb.AppendLine($"    private readonly grpcClient: {serviceName}Client;");
            sb.AppendLine();
            foreach (var prop in properties)
            {
                sb.AppendLine($"    {ToCamelCase(prop.Name)}: any;");
            }
            sb.AppendLine("    connectionStatus: string = 'Unknown';");
            sb.AppendLine();
            sb.AppendLine($"    constructor(grpcClient: {serviceName}Client) {{");
            sb.AppendLine("        this.grpcClient = grpcClient;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    async initializeRemote(): Promise<void> {");
            sb.AppendLine("        const state = await this.grpcClient.getState(new Empty());");
            foreach (var prop in properties)
            {
                sb.AppendLine($"        this.{ToCamelCase(prop.Name)} = (state as any)['{ToSnakeCase(prop.Name)}'];");
            }
            sb.AppendLine("        this.connectionStatus = 'Connected';");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    async updatePropertyValue(propertyName: string, value: any): Promise<void> {");
            sb.AppendLine("        const req: UpdatePropertyValueRequest = { propertyName, newValue: value }; ");
            sb.AppendLine("        await this.grpcClient.updatePropertyValue(req); ");
            sb.AppendLine("    }");
            sb.AppendLine();
            foreach (var cmd in commands)
            {
                var paramList = string.Join(", ", cmd.Parameters.Select(p => ToCamelCase(p.Name) + ": any"));
                var paramAssignments = string.Join(", ", cmd.Parameters.Select(p => $"{ToSnakeCase(p.Name)}: {ToCamelCase(p.Name)}"));
                sb.AppendLine($"    async {ToCamelCase(cmd.MethodName)}({paramList}): Promise<void> {{");
                sb.AppendLine($"        const req = {{ {paramAssignments} }} as any;");
                sb.AppendLine($"        await this.grpcClient.{ToCamelCase(cmd.MethodName)}(req);");
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0])) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        static string ToSnakeCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return System.Text.RegularExpressions.Regex.Replace(s, @"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z])(?=[a-z])", "_$1$2").ToLowerInvariant();
        }
    }
}