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
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Drawing;");
        }
        
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        
        // Generate PropertyNodeInfo class at namespace level for WinForms
        if (isWinForms)
        {
            sb.AppendLine("    // Property node information class");
            sb.AppendLine("    public class PropertyNodeInfo");
            sb.AppendLine("    {");
            sb.AppendLine("        public string PropertyName { get; set; } = string.Empty;");
            sb.AppendLine("        public object? Object { get; set; }");
            sb.AppendLine("        public bool IsSimpleProperty { get; set; }");
            sb.AppendLine("        public bool IsBooleanProperty { get; set; }");
            sb.AppendLine("        public bool IsEnumProperty { get; set; }");
            sb.AppendLine("        public bool IsCollectionProperty { get; set; }");
            sb.AppendLine("        public bool IsComplexProperty { get; set; }");
            sb.AppendLine("        public bool IsCollectionItem { get; set; }");
            sb.AppendLine("        public int CollectionIndex { get; set; }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
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
            sb.AppendLine($"                var form = new Form");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Text = \"Server GUI - {projectName}\",");
            sb.AppendLine("                    Width = 1150,");
            sb.AppendLine("                    Height = 780,");
            sb.AppendLine("                    StartPosition = FormStartPosition.CenterScreen");
            sb.AppendLine("                };");
            sb.AppendLine();
            sb.AppendLine("                var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400 };");
            sb.AppendLine("                form.Controls.Add(split);");
            sb.AppendLine();
            sb.AppendLine("                var statusStrip = new StatusStrip();");
            sb.AppendLine("                var statusLbl = new ToolStripStatusLabel();");
            sb.AppendLine("                statusStrip.Items.Add(statusLbl);");
            sb.AppendLine("                form.Controls.Add(statusStrip);");
            sb.AppendLine("                statusStrip.Dock = DockStyle.Bottom;");
            sb.AppendLine("                statusLbl.Text = \"Server Status: Running\";");
            sb.AppendLine();
            
            // Generate inline TreeView setup without methods/classes that can't be in try blocks
            sb.AppendLine("                // TreeView setup");
            sb.AppendLine("                var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };");
            sb.AppendLine("                split.Panel1.Controls.Add(tree);");
            sb.AppendLine();
            
            // Generate tree control buttons
            sb.AppendLine("                // Add tree view control buttons");
            sb.AppendLine("                var treeButtonsPanel = new FlowLayoutPanel");
            sb.AppendLine("                {");
            sb.AppendLine("                    Height = 35,");
            sb.AppendLine("                    FlowDirection = FlowDirection.LeftToRight,");
            sb.AppendLine("                    AutoSize = false,");
            sb.AppendLine("                    Dock = DockStyle.Bottom");
            sb.AppendLine("                };");
            sb.AppendLine("                split.Panel1.Controls.Add(treeButtonsPanel);");
            sb.AppendLine();
            sb.AppendLine("                var refreshBtn = new Button { Text = \"Refresh\", Width = 70, Height = 25 };");
            sb.AppendLine("                var expandBtn = new Button { Text = \"Expand All\", Width = 80, Height = 25 };");
            sb.AppendLine("                var collapseBtn = new Button { Text = \"Collapse\", Width = 70, Height = 25 };");
            sb.AppendLine("                treeButtonsPanel.Controls.Add(refreshBtn);");
            sb.AppendLine("                treeButtonsPanel.Controls.Add(expandBtn);");
            sb.AppendLine("                treeButtonsPanel.Controls.Add(collapseBtn);");
            sb.AppendLine();
            
            // Call separate method for tree loading
            sb.AppendLine("                LoadServerTree(tree, vm);");
            sb.AppendLine();
            
            // Wire up events to call the method
            sb.AppendLine("                refreshBtn.Click += (_, __) => LoadServerTree(tree, vm);");
            sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
            sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
            sb.AppendLine();
            
            // Property change monitoring
            sb.AppendLine("                // Property change monitoring");
            sb.AppendLine("                if (vm is INotifyPropertyChanged inpc)");
            sb.AppendLine("                {");
            sb.AppendLine("                    inpc.PropertyChanged += (_, e) =>");
            sb.AppendLine("                    {");
            sb.AppendLine("                        try { LoadServerTree(tree, vm); }");
            sb.AppendLine("                        catch { }");
            sb.AppendLine("                    };");
            sb.AppendLine("                }");
            sb.AppendLine();
            
            // Generate property editor (right panel)
            sb.AppendLine("                // Right panel for property details");
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
            
            sb.AppendLine("                var serverLabel = new Label { Text = \"Server Properties\", Font = new System.Drawing.Font(\"Segoe UI\", 12, System.Drawing.FontStyle.Bold), AutoSize = true };");
            sb.AppendLine("                flow.Controls.Add(serverLabel);");
            sb.AppendLine();
            sb.AppendLine("                var detailGroup = new GroupBox");
            sb.AppendLine("                {");
            sb.AppendLine("                    Text = \"Property Details (Server)\",");
            sb.AppendLine("                    AutoSize = true,");
            sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
            sb.AppendLine("                    Padding = new Padding(10)," );
            sb.AppendLine("                    Width = 350");
            sb.AppendLine("                };");
            sb.AppendLine("                flow.Controls.Add(detailGroup);");
            sb.AppendLine();
            sb.AppendLine("                var detailLayout = new TableLayoutPanel");
            sb.AppendLine("                {");
            sb.AppendLine("                    ColumnCount = 2,");
            sb.AppendLine("                    AutoSize = true,");
            sb.AppendLine("                    Width = 320");
            sb.AppendLine("                };");
            sb.AppendLine("                detailGroup.Controls.Add(detailLayout);");
            sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
            sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));");
            sb.AppendLine();
            
            // Add simple property display controls - using PropertyDiscoveryUtility structured data
            var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
            var displayProps = analysis.SimpleProperties.Concat(analysis.BooleanProperties)
                                      .Concat(analysis.EnumProperties).Take(5);
                                      
            foreach (var prop in displayProps)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}Label = new Label {{ Text = \"{prop.Name}:\", AutoSize = true }};");
                
                // Use metadata for proper value display handling
                if (metadata.IsNonNullableValueType)
                {
                    sb.AppendLine($"                    var {metadata.SafeVariableName}Value = new Label {{ Text = vm.{metadata.SafePropertyAccess}.ToString(), AutoSize = true, ForeColor = Color.Blue }};");
                }
                else
                {
                    sb.AppendLine($"                    var {metadata.SafeVariableName}Value = new Label {{ Text = vm.{metadata.SafePropertyAccess}?.ToString() ?? \"<null>\", AutoSize = true, ForeColor = Color.Blue }};");
                }
                
                sb.AppendLine($"                    flow.Controls.Add({metadata.SafeVariableName}Label);");
                sb.AppendLine($"                    flow.Controls.Add({metadata.SafeVariableName}Value);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
            }
            
            // Tree selection event to call separate method
            sb.AppendLine("                tree.AfterSelect += (_, e) =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    ShowServerPropertyEditor(e.Node?.Tag as PropertyNodeInfo, detailLayout, vm);");
            sb.AppendLine("                };");

            // Generate commands section if any commands exist
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
                foreach (var c in cmds)
                {
                    var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                    sb.AppendLine();
                    sb.AppendLine($"                var btn{cmdIndex} = new Button");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    Text = \"{baseName}\",");
                    sb.AppendLine("                    Width = 140,");
                    sb.AppendLine("                    Height = 30");
                    sb.AppendLine("                };");
                    sb.AppendLine($"                btn{cmdIndex}.Click += (_, __) =>");
                    sb.AppendLine("                {");
                    sb.AppendLine("                    try");
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        vm.{c.CommandPropertyName}?.Execute(null);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                    catch (Exception ex)");
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        System.Windows.Forms.MessageBox.Show($\"Error executing {baseName}: {{ex.Message}}\", \"Server Command Error\", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                };");
                    sb.AppendLine($"                cmdFlow.Controls.Add(btn{cmdIndex});");
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
        
        // Generate separate methods for TreeView operations to avoid try block issues
        if (isWinForms)
        {
            sb.AppendLine();
            GenerateServerTreeLoadingMethod(sb, props, modelName);
            sb.AppendLine();
            GenerateServerPropertyEditorMethod(sb, props);
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
    
    private static void GenerateServerTreeLoadingMethod(StringBuilder sb, List<PropertyInfo> props, string modelName)
    {
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        sb.AppendLine($"        private static void LoadServerTree(TreeView tree, {modelName} vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                tree.BeginUpdate();");
        sb.AppendLine("                tree.Nodes.Clear();");
        sb.AppendLine("                ");
        
        // Only create root node if we have properties to display
        var hasAnyProperties = analysis.SimpleProperties.Any() || 
                              analysis.BooleanProperties.Any() || 
                              analysis.CollectionProperties.Any() || 
                              analysis.ComplexProperties.Any() || 
                              analysis.EnumProperties.Any();
        
        if (hasAnyProperties)
        {
            sb.AppendLine("                var rootNode = new TreeNode(\"Server ViewModel Properties\");");
            sb.AppendLine("                tree.Nodes.Add(rootNode);");
            sb.AppendLine("                ");
        }
        
        // Generate property categories using PropertyDiscoveryUtility analysis
        if (analysis.SimpleProperties.Any())
        {
            sb.AppendLine("                // Simple properties");
            sb.AppendLine("                var simplePropsNode = new TreeNode(\"Simple Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine("                rootNode.Nodes.Add(simplePropsNode);");
            }
            else
            {
                sb.AppendLine("                tree.Nodes.Add(simplePropsNode);");
            }
            
            foreach (var prop in analysis.SimpleProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                
                // Use metadata for proper null handling
                if (metadata.IsNonNullableValueType)
                {
                    sb.AppendLine($"                    var {metadata.SafeVariableName}Value = vm.{metadata.SafePropertyAccess}.ToString();");
                }
                else
                {
                    sb.AppendLine($"                    var {metadata.SafeVariableName}Value = vm.{metadata.SafePropertyAccess}?.ToString() ?? \"<null>\";");
                }
                
                sb.AppendLine($"                    var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name}: \" + {metadata.SafeVariableName}Value);");
                sb.AppendLine($"                    {metadata.SafeVariableName}Node.Tag = new PropertyNodeInfo {{ PropertyName = \"{prop.Name}\", Object = vm, IsSimpleProperty = true }};");
                sb.AppendLine($"                    simplePropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    simplePropsNode.Nodes.Add({metadata.SafeVariableName}ErrorNode);");
                sb.AppendLine("                }");
            }
        }
        
        if (analysis.BooleanProperties.Any())
        {
            sb.AppendLine("                // Boolean properties");
            sb.AppendLine("                var boolPropsNode = new TreeNode(\"Boolean Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine("                rootNode.Nodes.Add(boolPropsNode);");
            }
            else
            {
                sb.AppendLine("                tree.Nodes.Add(boolPropsNode);");
            }
            
            foreach (var prop in analysis.BooleanProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name}: \" + vm.{metadata.SafePropertyAccess}.ToString());");
                sb.AppendLine($"                    {metadata.SafeVariableName}Node.Tag = new PropertyNodeInfo {{ PropertyName = \"{prop.Name}\", Object = vm, IsBooleanProperty = true }};");
                sb.AppendLine($"                    boolPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    boolPropsNode.Nodes.Add({metadata.SafeVariableName}ErrorNode);");
                sb.AppendLine("                }");
            }
        }
        
        if (analysis.CollectionProperties.Any())
        {
            sb.AppendLine("                // Collection properties");
            sb.AppendLine("                var collectionPropsNode = new TreeNode(\"Collections\");");
            if (hasAnyProperties)
            {
                sb.AppendLine("                rootNode.Nodes.Add(collectionPropsNode);");
            }
            else
            {
                sb.AppendLine("                tree.Nodes.Add(collectionPropsNode);");
            }
            
            foreach (var prop in analysis.CollectionProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    if (vm.{metadata.SafePropertyAccess} != null)");
                sb.AppendLine("                    {");
                
                // Use metadata for correct count property
                sb.AppendLine($"                        var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name} [\" + vm.{metadata.SafePropertyAccess}.{metadata.CountProperty} + \" items]\");");
                sb.AppendLine($"                        {metadata.SafeVariableName}Node.Tag = new PropertyNodeInfo {{ PropertyName = \"{prop.Name}\", Object = vm, IsCollectionProperty = true }};");
                sb.AppendLine($"                        collectionPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                    }");
                sb.AppendLine("                    else");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name} [null]\");");
                sb.AppendLine($"                        collectionPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    collectionPropsNode.Nodes.Add({metadata.SafeVariableName}ErrorNode);");
                sb.AppendLine("                }");
            }
        }
        
        if (analysis.ComplexProperties.Any())
        {
            sb.AppendLine("                // Complex properties (nested objects)");
            sb.AppendLine("                var complexPropsNode = new TreeNode(\"Complex Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine("                rootNode.Nodes.Add(complexPropsNode);");
            }
            else
            {
                sb.AppendLine("                tree.Nodes.Add(complexPropsNode);");
            }
            
            foreach (var prop in analysis.ComplexProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    if (vm.{metadata.SafePropertyAccess} != null)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        var {metadata.SafeVariableName}TypeName = vm.{metadata.SafePropertyAccess}.GetType().Name;");
                sb.AppendLine($"                        var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name} (\" + {metadata.SafeVariableName}TypeName + \")\");");
                sb.AppendLine($"                        {metadata.SafeVariableName}Node.Tag = new PropertyNodeInfo {{ PropertyName = \"{prop.Name}\", Object = vm.{metadata.SafePropertyAccess}, IsComplexProperty = true }};");
                sb.AppendLine($"                        complexPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                    }");
                sb.AppendLine("                    else");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name} [null]\");");
                sb.AppendLine($"                        complexPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    complexPropsNode.Nodes.Add({metadata.SafeVariableName}ErrorNode);");
                sb.AppendLine("                }");
            }
        }
        
        if (analysis.EnumProperties.Any())
        {
            sb.AppendLine("                // Enum properties");
            sb.AppendLine("                var enumPropsNode = new TreeNode(\"Enum Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine("                rootNode.Nodes.Add(enumPropsNode);");
            }
            else
            {
                sb.AppendLine("                tree.Nodes.Add(enumPropsNode);");
            }
            
            foreach (var prop in analysis.EnumProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}Node = new TreeNode(\"{prop.Name}: \" + vm.{metadata.SafePropertyAccess}.ToString());");
                sb.AppendLine($"                    {metadata.SafeVariableName}Node.Tag = new PropertyNodeInfo {{ PropertyName = \"{prop.Name}\", Object = vm, IsEnumProperty = true }};");
                sb.AppendLine($"                    enumPropsNode.Nodes.Add({metadata.SafeVariableName}Node);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var {metadata.SafeVariableName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                    enumPropsNode.Nodes.Add({metadata.SafeVariableName}ErrorNode);");
                sb.AppendLine("                }");
            }
        }
        
        if (hasAnyProperties)
        {
            sb.AppendLine("                rootNode.Expand();");
        }
        
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                tree.Nodes.Clear();");
        sb.AppendLine("                tree.Nodes.Add(new TreeNode(\"Error loading properties: \" + ex.Message));");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                tree.EndUpdate();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }
    
    private static void GenerateServerPropertyEditorMethod(StringBuilder sb, List<PropertyInfo> props)
    {
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        sb.AppendLine("        private static void ShowServerPropertyEditor(PropertyNodeInfo? nodeInfo, TableLayoutPanel detailLayout, object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Clear existing controls");
        sb.AppendLine("            foreach (Control control in detailLayout.Controls.OfType<Control>().ToArray())");
        sb.AppendLine("            {");
        sb.AppendLine("                detailLayout.Controls.Remove(control);");
        sb.AppendLine("                control.Dispose();");
        sb.AppendLine("            }");
        sb.AppendLine("            detailLayout.RowCount = 0;");
        sb.AppendLine();
        sb.AppendLine("            if (nodeInfo?.Object == null) return;");
        sb.AppendLine();
        sb.AppendLine("            int row = 0;");
        sb.AppendLine();
        sb.AppendLine("            // Show property name");
        sb.AppendLine("            var nameLabel = new Label { Text = \"Property:\", AutoSize = true, Font = new Font(\"Segoe UI\", 9, FontStyle.Bold) };");
        sb.AppendLine("            var nameValue = new Label { Text = nodeInfo.PropertyName, AutoSize = true };");
        sb.AppendLine("            detailLayout.Controls.Add(nameLabel, 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(nameValue, 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            // Show property info based on type");
        sb.AppendLine("            var infoLabel = new Label { Text = \"Server Property\", AutoSize = true };");
        sb.AppendLine("            detailLayout.Controls.Add(new Label { Text = \"Type:\" }, 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(infoLabel, 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.RowCount = row;");
        sb.AppendLine("        }");
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