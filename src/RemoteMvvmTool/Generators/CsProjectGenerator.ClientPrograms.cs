using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static partial class CsProjectGenerator
{
    // ---------------- Client Program Generators ----------------
    public static string GenerateGuiClientProgram(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = runType.Equals("wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = runType.Equals("winforms", StringComparison.OrdinalIgnoreCase);
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var clientClassName = modelName + "RemoteClient";
        
        // Use the new abstraction for consistent UI generation
        if (isWpf)
        {
            var wpfGenerator = new WpfClientUIGenerator(projectName, modelName, props, cmds, clientClassName, clientNs);
            return wpfGenerator.GenerateProgram(protoNs, serviceName);
        }
        else if (isWinForms)
        {
            var winFormsGenerator = new WinFormsClientUIGenerator(projectName, modelName, props, cmds, clientClassName, clientNs);
            return winFormsGenerator.GenerateProgram(protoNs, serviceName);
        }
        else
        {
            // Fallback for unsupported platforms
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace GuiClientApp");
            sb.AppendLine("{");
            sb.AppendLine("    public class Program");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void Main(string[] args)");
            sb.AppendLine("        {");
            sb.AppendLine("            Console.WriteLine(\"Unsupported GUI platform\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}