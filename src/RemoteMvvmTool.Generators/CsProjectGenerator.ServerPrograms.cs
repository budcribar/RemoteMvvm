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
        
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.ViewModels;");
        
        if (isWpf) sb.AppendLine("using System.Windows;");
        if (isWinForms)
        {
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Drawing;");
        }
        
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        [STAThread]");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            int port = 50052;");
        sb.AppendLine("            if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine($\"Starting server with GUI on port {port}...\");");
        sb.AppendLine("                var serverOptions = new ServerOptions { Port = port, UseHttps = true };");
        sb.AppendLine($"                var vm = new {modelName}(serverOptions);");
        sb.AppendLine("                Console.WriteLine($\"Server ready on port {port}\");");
        sb.AppendLine();

        if (isWpf)
        {
            sb.AppendLine("                var app = new Application();");
            sb.AppendLine("                var win = new MainWindow(vm);");
            sb.AppendLine("                win.Title = \"Server GUI - \" + win.Title;");
            sb.AppendLine("                app.Run(win);");
        }
        else if (isWinForms)
        {
            sb.AppendLine("                Application.EnableVisualStyles();");
            sb.AppendLine("                var form = new Form");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Text = \"Server GUI - {projectName}\",");
            sb.AppendLine("                    Width = 1150,");
            sb.AppendLine("                    Height = 780,");
            sb.AppendLine("                    StartPosition = FormStartPosition.CenterScreen");
            sb.AppendLine("                };");
            sb.AppendLine();

            // Create a simple but working WinForms server UI
            sb.AppendLine("                var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400 };");
            sb.AppendLine("                form.Controls.Add(split);");
            sb.AppendLine();
            
            sb.AppendLine("                var statusStrip = new StatusStrip();");
            sb.AppendLine("                var statusLbl = new ToolStripStatusLabel(\"Server Status: Running\");");
            sb.AppendLine("                statusStrip.Items.Add(statusLbl);");
            sb.AppendLine("                form.Controls.Add(statusStrip);");
            sb.AppendLine();
            
            // Left panel - simple tree view for server properties
            sb.AppendLine("                var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };");
            sb.AppendLine("                split.Panel1.Controls.Add(tree);");
            sb.AppendLine();
            
            sb.AppendLine("                var rootNode = new TreeNode(\"Server ViewModel Properties\");");
            sb.AppendLine("                tree.Nodes.Add(rootNode);");
            sb.AppendLine();

            // Add simple property nodes - limit to first few properties to avoid complexity
            int propIndex = 0;
            foreach (var prop in props.Take(8)) // Limit to avoid complexity
            {
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var prop{propIndex}Value = vm.{prop.Name}?.ToString() ?? \"<null>\";");
                sb.AppendLine($"                    var prop{propIndex}Node = new TreeNode(\"{prop.Name}: \" + prop{propIndex}Value);");
                sb.AppendLine($"                    rootNode.Nodes.Add(prop{propIndex}Node);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var prop{propIndex}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    rootNode.Nodes.Add(prop{propIndex}ErrorNode);");
                sb.AppendLine("                }");
                propIndex++;
            }
            
            sb.AppendLine("                rootNode.Expand();");
            sb.AppendLine();

            // Right panel - simple property details
            sb.AppendLine("                var rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };");
            sb.AppendLine("                split.Panel2.Controls.Add(rightPanel);");
            sb.AppendLine();
            
            sb.AppendLine("                var flow = new FlowLayoutPanel");
            sb.AppendLine("                {");
            sb.AppendLine("                    Dock = DockStyle.Top,");
            sb.AppendLine("                    AutoSize = true,");
            sb.AppendLine("                    FlowDirection = FlowDirection.TopDown,");
            sb.AppendLine("                    WrapContents = false");
            sb.AppendLine("                };");
            sb.AppendLine("                rightPanel.Controls.Add(flow);");
            sb.AppendLine();
            
            sb.AppendLine("                var serverLabel = new Label { Text = \"Server Properties\", Font = new Font(\"Segoe UI\", 12, FontStyle.Bold), AutoSize = true };");
            sb.AppendLine("                flow.Controls.Add(serverLabel);");
            sb.AppendLine();

            // Add simple property display controls
            foreach (var prop in props.Take(5))
            {
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {prop.Name.ToLower()}Label = new Label {{ Text = \"{prop.Name}:\", AutoSize = true }};");
                sb.AppendLine($"                    var {prop.Name.ToLower()}Value = new Label {{ Text = vm.{prop.Name}?.ToString() ?? \"<null>\", AutoSize = true, ForeColor = Color.Blue }};");
                sb.AppendLine($"                    flow.Controls.Add({prop.Name.ToLower()}Label);");
                sb.AppendLine($"                    flow.Controls.Add({prop.Name.ToLower()}Value);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
            }

            // Add commands section if any commands exist
            if (cmds.Any())
            {
                sb.AppendLine();
                sb.AppendLine("                var cmdGroup = new GroupBox");
                sb.AppendLine("                {");
                sb.AppendLine("                    Text = \"Server Commands\",");
                sb.AppendLine("                    AutoSize = true,");
                sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
                sb.AppendLine("                    Padding = new Padding(10)");
                sb.AppendLine("                };");
                sb.AppendLine("                flow.Controls.Add(cmdGroup);");
                sb.AppendLine();
                
                sb.AppendLine("                var cmdFlow = new FlowLayoutPanel");
                sb.AppendLine("                {");
                sb.AppendLine("                    Dock = DockStyle.Top,");
                sb.AppendLine("                    AutoSize = true,");
                sb.AppendLine("                    FlowDirection = FlowDirection.LeftToRight,");
                sb.AppendLine("                    WrapContents = true");
                sb.AppendLine("                };");
                sb.AppendLine("                cmdGroup.Controls.Add(cmdFlow);");
                
                int cmdIndex = 0;
                foreach (var c in cmds.Take(5)) // Limit commands to avoid complexity
                {
                    var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                    sb.AppendLine();
                    sb.AppendLine($"                var cmd{cmdIndex}Btn = new Button");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    Text = \"{baseName}\",");
                    sb.AppendLine("                    Width = 140,");
                    sb.AppendLine("                    Height = 30");
                    sb.AppendLine("                };");
                    sb.AppendLine($"                cmd{cmdIndex}Btn.Click += (_, __) =>");
                    sb.AppendLine("                {");
                    sb.AppendLine("                    try");
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        vm.{c.CommandPropertyName}?.Execute(null);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                    catch (Exception ex)");
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        MessageBox.Show($\"Error executing {baseName}: {{ex.Message}}\", \"Server Command Error\", MessageBoxButtons.OK, MessageBoxIcon.Warning);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                };");
                    sb.AppendLine($"                cmdFlow.Controls.Add(cmd{cmdIndex}Btn);");
                    cmdIndex++;
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("                Application.Run(form);");
        }
        else
        {
            sb.AppendLine("                Console.WriteLine(\"Unsupported GUI platform\");");
        }
        
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