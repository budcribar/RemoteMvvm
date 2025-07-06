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
        return $"{{\n  \"name\": \"{projectName.ToLowerInvariant()}\",\n  \"version\": \"1.0.0\",\n  \"scripts\": {{\n    \"build\": \"webpack --mode development\",\n    \"dev\": \"webpack serve --mode development --open\"\n  }},\n  \"devDependencies\": {{\n    \"ts-loader\": \"^9.5.2\",\n    \"typescript\": \"^5.0.0\",\n    \"webpack\": \"^5.0.0\",\n    \"webpack-cli\": \"^4.0.0\",\n    \"webpack-dev-server\": \"^4.0.0\"\n  }},\n  \"dependencies\": {{\n    \"grpc-web\": \"^1.5.0\",\n    \"google-protobuf\": \"3.21.4\"\n  }}\n}}";
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
}
