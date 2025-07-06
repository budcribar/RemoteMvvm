using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class TsProjectGenerator
{
    public static string GenerateAppTs(string vmName, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"import {{ {serviceName}Client }} from './generated/{serviceName}ServiceClientPb.js';");
        sb.AppendLine($"import {{ {vmName}RemoteClient }} from './{vmName}RemoteClient';");
        sb.AppendLine();
        sb.AppendLine("const grpcHost = 'http://localhost:50052';");
        sb.AppendLine($"const grpcClient = new {serviceName}Client(grpcHost);");
        sb.AppendLine($"const vm = new {vmName}RemoteClient(grpcClient);");
        sb.AppendLine();
        sb.AppendLine("async function render() {");
        foreach (var p in props)
        {
            string camel = GeneratorHelpers.ToCamelCase(p.Name);
            sb.AppendLine($"    (document.getElementById('{camel}') as HTMLInputElement).value = vm.{camel};");
        }
        sb.AppendLine("    (document.getElementById('connection-status') as HTMLElement).textContent = vm.connectionStatus;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function init() {");
        sb.AppendLine("    await vm.initializeRemote();");
        sb.AppendLine("    vm.addChangeListener(render);");
        sb.AppendLine("    await render();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("document.addEventListener('DOMContentLoaded', () => {");
        sb.AppendLine("    init();");
        foreach (var p in props)
        {
            string camel = GeneratorHelpers.ToCamelCase(p.Name);
            sb.AppendLine($"    (document.getElementById('{camel}') as HTMLInputElement).addEventListener('change', async () => {{");
            sb.AppendLine($"        await vm.updatePropertyValue('{p.Name}', (document.getElementById('{camel}') as HTMLInputElement).value);");
            sb.AppendLine("    });");
        }
        foreach (var cmd in cmds)
        {
            string camel = GeneratorHelpers.ToCamelCase(cmd.MethodName);
            sb.AppendLine($"    (document.getElementById('{camel}-btn') as HTMLButtonElement).addEventListener('click', async () => {{");
            sb.AppendLine($"        await vm.{camel}();");
            sb.AppendLine("    });");
        }
        sb.AppendLine("});");
        return sb.ToString();
    }

    public static string GenerateIndexHtml(string vmName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">\n<head>");
        sb.AppendLine("    <meta charset=\"utf-8\" />");
        sb.AppendLine($"    <title>{vmName} Client</title>");
        sb.AppendLine("</head>\n<body>");
        sb.AppendLine("    <h3>Remote ViewModel</h3>");
        foreach (var p in props)
        {
            string camel = GeneratorHelpers.ToCamelCase(p.Name);
            sb.AppendLine($"    <div><label>{p.Name}: <input id='{camel}'/></label></div>");
        }
        foreach (var cmd in cmds)
        {
            string camel = GeneratorHelpers.ToCamelCase(cmd.MethodName);
            sb.AppendLine($"    <button id='{camel}-btn'>{cmd.MethodName}</button>");
        }
        sb.AppendLine("    <div id='connection-status'></div>");
        sb.AppendLine("    <script src='bundle.js'></script>");
        sb.AppendLine("</body>\n</html>");
        return sb.ToString();
    }

    public static string GeneratePackageJson(string projectName)
    {
        return $"{{\n  \"name\": \"{projectName.ToLowerInvariant()}\",\n  \"version\": \"1.0.0\",\n  \"scripts\": {{\n    \"protoc\": \"protoc --plugin=protoc-gen-ts=\\\".\\\\node_modules\\\\.bin\\\\protoc-gen-ts.cmd\\\" --plugin=protoc-gen-grpc-web=\\\".\\\\node_modules\\\\protoc-gen-grpc-web\\\\bin\\\\protoc-gen-grpc-web.exe\\\" --js_out=\\\"import_style=commonjs,binary:./src/generated\\\" --grpc-web_out=\\\"import_style=typescript,mode=grpcwebtext:./src/generated\\\" -Iprotos -Inode_modules/protoc/protoc/include {projectName}Service.proto\",\n    \"build\": \"webpack --mode development\",\n    \"dev\": \"webpack serve --mode development --open\"\n  }},\n  \"devDependencies\": {{\n    \"ts-loader\": \"^9.5.2\",\n    \"typescript\": \"^5.0.0\",\n    \"webpack\": \"^5.0.0\",\n    \"webpack-cli\": \"^4.0.0\",\n    \"webpack-dev-server\": \"^4.0.0\",\n    \"ts-protoc-gen\": \"0.15.0\"\n  }},\n  \"dependencies\": {{\n    \"grpc-web\": \"^1.5.0\",\n    \"google-protobuf\": \"3.21.4\",\n    \"protoc\": \"^1.1.3\",\n    \"protoc-gen-grpc-web\": \"^1.5.0\"\n  }}\n}}";
    }

    public static string GenerateTsConfig()
    {
        return "{\n  \"compilerOptions\": {\n    \"target\": \"es2020\",\n    \"module\": \"es2020\",\n    \"moduleResolution\": \"node\",\n    \"sourceMap\": true\n  }\n}";
    }

    public static string GenerateWebpackConfig()
    {
        var sb = new StringBuilder();
        sb.AppendLine("const path = require('path');");
        sb.AppendLine("module.exports = {");
        sb.AppendLine("  entry: './src/app.ts',");
        sb.AppendLine("  output: {");
        sb.AppendLine("    filename: 'bundle.js',");
        sb.AppendLine("    path: path.resolve(__dirname, 'wwwroot'),");
        sb.AppendLine("    clean: false,");
        sb.AppendLine("  },");
        sb.AppendLine("  resolve: { extensions: ['.ts', '.js'] },");
        sb.AppendLine("  module: { rules: [{ test: /\\.ts$/, use: 'ts-loader', exclude: /node_modules/ }] },");
        sb.AppendLine("  devtool: 'source-map',");
        sb.AppendLine("  devServer: { static: { directory: path.join(__dirname, 'wwwroot') }, hot: true, open: true, port: 3000 },");
        sb.AppendLine("  mode: 'development'");
        sb.AppendLine("};");
        return sb.ToString();
    }

    public static string GenerateLaunchJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"version\": \"0.2.0\",");
        sb.AppendLine("  \"configurations\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"type\": \"node\",");
        sb.AppendLine("      \"request\": \"launch\",");
        sb.AppendLine("      \"name\": \"Launch Program\",");
        sb.AppendLine("      \"skipFiles\": [ \"<node_internals>/**\" ],");
        sb.AppendLine("      \"program\": \"${workspaceFolder}/src/app.js\",");
        sb.AppendLine("      \"cwd\": \"${workspaceFolder}\",");
        sb.AppendLine("      \"console\": \"externalTerminal\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateReadme(string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {projectName} TypeScript Client");
        sb.AppendLine();
        sb.AppendLine("This project was generated by RemoteMvvmTool and uses gRPC-Web to communicate with the server.");
        sb.AppendLine();
        sb.AppendLine("## Setup");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("npm install");
        sb.AppendLine("npm run protoc");
        sb.AppendLine("npm run build");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("To regenerate the gRPC-Web stubs separately:");
        sb.AppendLine("```bash");
        sb.AppendLine("npm run protoc");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Start the development server:");
        sb.AppendLine("```bash");
        sb.AppendLine("npm run dev");
        sb.AppendLine("```");
        return sb.ToString();
    }
}
