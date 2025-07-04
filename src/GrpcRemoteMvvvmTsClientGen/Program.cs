using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace GrpcRemoteMvvmTsClientGen
{
    class Program
    {
        const string AttributeDefinitionResourceName = "GrpcRemoteMvvmTsClientGen.Resources.GenerateGrpcRemoteAttribute.cs";
        const string AttributeDefinitionPlaceholderPath = "embedded://PeakSWC/Mvvm/Remote/GenerateGrpcRemoteAttribute.cs";
        const string CommunityToolkitMvvmResourceName = "GrpcRemoteMvvmTsClientGen.Resources.CommunityToolkit.Mvvm.dll";
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

            // Load embedded attribute definition and CommunityToolkit.Mvvm.dll similar to ProtoGeneratorUtil
            string? attributeSource = null;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using Stream? stream = asm.GetManifestResourceStream(AttributeDefinitionResourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    attributeSource = await reader.ReadToEndAsync();
                }
                else
                {
                    Console.WriteLine($"Embedded attribute resource '{AttributeDefinitionResourceName}' not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading embedded attribute: {ex.Message}");
            }

            string? mvvmDllPath = null;
            try
            {
                mvvmDllPath = ExtractResourceToTempFile(CommunityToolkitMvvmResourceName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting CommunityToolkit.Mvvm.dll: {ex.Message}");
            }

            var referencePaths = LoadDefaultReferencePaths();
            if (!string.IsNullOrEmpty(mvvmDllPath))
                referencePaths.Add(mvvmDllPath);

            // Use the shared analyzer to load the ViewModel
            var (vmSymbol, vmName, properties, commands, compilation) = await ViewModelAnalyzer.AnalyzeAsync(
                new[] { viewModelFile },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                "PeakSWC.Mvvm.Remote.GenerateGrpcRemoteAttribute",
                referencePaths,
                attributeSource,
                AttributeDefinitionPlaceholderPath
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

            string protoNamespace = string.Empty;
            string serviceName = string.Empty;

            if (attr != null && attr.ConstructorArguments.Length >= 2)
            {
                protoNamespace = attr.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                serviceName = attr.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
            }
            else if (attr?.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attrSyntax &&
                     attrSyntax.ArgumentList?.Arguments.Count >= 2)
            {
                var model = compilation.GetSemanticModel(attrSyntax.SyntaxTree);
                var arg0 = model.GetConstantValue(attrSyntax.ArgumentList.Arguments[0].Expression);
                var arg1 = model.GetConstantValue(attrSyntax.ArgumentList.Arguments[1].Expression);
                protoNamespace = arg0.HasValue ? arg0.Value?.ToString() ?? string.Empty : string.Empty;
                serviceName = arg1.HasValue ? arg1.Value?.ToString() ?? string.Empty : string.Empty;
            }
            else
            {
                Console.WriteLine("GenerateGrpcRemoteAttribute with required arguments not found.");
                return;
            }

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
            sb.AppendLine($"import {{ {serviceName}Client }} from './generated/{serviceName}ServiceClientPb';");
            var requestTypes = string.Join(", ", commands.Select(c => c.MethodName + "Request").Distinct());
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
            foreach (var prop in properties)
            {
                sb.AppendLine($"    {ToCamelCase(prop.Name)}: any;");
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
            sb.AppendLine($"        const state = await this.grpcClient.getState(new Empty());");
            foreach (var prop in properties)
            {
                sb.AppendLine($"        this.{ToCamelCase(prop.Name)} = (state as any).get{prop.Name}();");
            }
            sb.AppendLine("        this.connectionStatus = 'Connected';");
            sb.AppendLine("        this.notifyChange();");
            sb.AppendLine("        this.startListeningToPropertyChanges();");
            sb.AppendLine("        this.startPingLoop();");
            sb.AppendLine("    }");
            sb.AppendLine();
            // Method to refresh state without altering connection status
            sb.AppendLine("    async refreshState(): Promise<void> {");
            sb.AppendLine($"        const state = await this.grpcClient.getState(new Empty());");
            foreach (var prop in properties)
            {
                sb.AppendLine($"        this.{ToCamelCase(prop.Name)} = (state as any).get{prop.Name}();");
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
            foreach (var cmd in commands)
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
            foreach (var prop in properties)
            {
                var wrapper = GetWrapperType(prop.TypeString);
                if (wrapper != null)
                {
                    sb.AppendLine($"                case '{prop.Name}':");
                    string unpack = wrapper switch
                    {
                        "StringValue" => "StringValue.deserializeBinary",
                        "Int32Value" => "Int32Value.deserializeBinary",
                        "BoolValue" => "BoolValue.deserializeBinary",
                        _ => ""
                    };
                    sb.AppendLine($"                    this.{ToCamelCase(prop.Name)} = anyVal?.unpack({unpack}, 'google.protobuf.{wrapper}')?.getValue();");
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

        static string ExtractResourceToTempFile(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Resource '{resourceName}' not found in assembly.");
            var tempFile = Path.Combine(Path.GetTempPath(), $"TsClientGen_{Guid.NewGuid()}_{Path.GetFileName(resourceName)}");
            using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(fs);
            return tempFile;
        }

        static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0])) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        static string ToPascalCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        static string? GetWrapperType(string typeName)
        {
            return typeName switch
            {
                "string" => "StringValue",
                "int" => "Int32Value",
                "System.Int32" => "Int32Value",
                "bool" => "BoolValue",
                "System.Boolean" => "BoolValue",
                _ => null
            };
        }

        static string ToSnakeCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return System.Text.RegularExpressions.Regex.Replace(s, @"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z])(?=[a-z])", "_$1$2").ToLowerInvariant();
        }

        static System.Collections.Generic.List<string> LoadDefaultReferencePaths()
        {
            var refs = new System.Collections.Generic.List<string>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(tpa))
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        refs.Add(p);
                    }
                }
            }
            return refs;
        }
    }
}