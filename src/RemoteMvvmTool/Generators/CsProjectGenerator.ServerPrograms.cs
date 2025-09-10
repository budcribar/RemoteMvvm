using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static partial class CsProjectGenerator
{
    // ---------------- Server Program Generators ----------------
    public static string GenerateServerProgram(string projectName, string protoNs, string serviceName, string platform)
    {
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.ViewModels;");
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (args.Length < 1 || !int.TryParse(args[0], out var port))");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine(\"Port argument required\");");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine($\"Starting server on port {port}...\");");
        sb.AppendLine("                var serverOptions = new ServerOptions { Port = port, UseHttps = true };");
        sb.AppendLine($"                var vm = new {modelName}(serverOptions);");
        sb.AppendLine("                Console.WriteLine($\"Server ready on port {port}\");");
        sb.AppendLine("                Thread.Sleep(Timeout.Infinite);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine(\"SERVER_ERROR_START\");");
        sb.AppendLine("                Console.WriteLine(ex);");
        sb.AppendLine("                Console.WriteLine(\"SERVER_ERROR_END\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateServerGuiProgram(string projectName, string runType, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = runType.Equals("wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = runType.Equals("winforms", StringComparison.OrdinalIgnoreCase);
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        
        // Use the new abstraction for consistent UI generation
        if (isWpf)
        {
            var wpfGenerator = new WpfServerUIGenerator(projectName, modelName, props, cmds);
            return wpfGenerator.GenerateProgram(protoNs, serviceName);
        }
        else if (isWinForms)
        {
            var winFormsGenerator = new WinFormsServerUIGenerator(projectName, modelName, props, cmds);
            return winFormsGenerator.GenerateProgram(protoNs, serviceName);
        }
        else
        {
            // Fallback for unsupported platforms
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace ServerApp");
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
    
    public static string GenerateProgramCs(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        // legacy simplified
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}");
        sb.AppendLine("{");
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            Console.WriteLine(\"Legacy combined harness omitted\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}