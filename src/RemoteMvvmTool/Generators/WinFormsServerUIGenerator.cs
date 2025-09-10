using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using System.Reflection;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// WinForms-specific server UI generator that produces hierarchical property display like WPF
/// </summary>
public class WinFormsServerUIGenerator : UIGeneratorBase
{
    public WinFormsServerUIGenerator(string projectName, string modelName, List<GrpcRemoteMvvmModelUtil.PropertyInfo> properties, List<CommandInfo> commands)
        : base(projectName, modelName, properties, commands, "Server")
    {
    }

    public override string GenerateProgram(string protoNs, string serviceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.ViewModels;");
        sb.AppendLine("using System.Windows.Forms;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("    " + GeneratePropertyNodeInfoClass().Replace("\n", "\n    "));
        sb.AppendLine();
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        private static HashSet<object> visitedObjects = new HashSet<object>();");
        sb.AppendLine();
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
        sb.AppendLine($"                var vm = new {ModelName}(serverOptions);");
        sb.AppendLine("                Console.WriteLine($\"Server ready on port {port}\");");
        sb.AppendLine();
        sb.AppendLine("                Application.EnableVisualStyles();");
        sb.AppendLine($"                var form = new Form");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Text = \"Server GUI - {ProjectName}\",");
        sb.AppendLine("                    Width = 1150,");
        sb.AppendLine("                    Height = 780,");
        sb.AppendLine("                    StartPosition = FormStartPosition.CenterScreen");
        sb.AppendLine("                };");
        sb.AppendLine();
        
        // Generate the two-panel layout like WPF
        sb.AppendLine("                var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 500 };");
        sb.AppendLine("                form.Controls.Add(split);");
        sb.AppendLine();
        
        // Generate status strip
        sb.AppendLine("                var statusStrip = new StatusStrip();");
        sb.AppendLine("                var statusLbl = new ToolStripStatusLabel();");
        sb.AppendLine("                statusStrip.Items.Add(statusLbl);");
        sb.AppendLine("                form.Controls.Add(statusStrip);");
        sb.AppendLine("                statusStrip.Dock = DockStyle.Bottom;");
        sb.AppendLine("                statusLbl.Text = \"Server Status: Running\";");
        sb.AppendLine();
        
        // Generate TreeView structure
        sb.Append(GenerateTreeViewStructure());
        sb.AppendLine();
        
        // Generate property details panel
        sb.Append(GeneratePropertyDetailsPanel());
        sb.AppendLine();
        
        // Generate command buttons
        sb.Append(GenerateCommandButtons());
        sb.AppendLine();
        
        // Generate hierarchical tree loading using reflection-based approach like WPF
        sb.AppendLine("                // Hierarchical property tree loading like WPF");
        sb.AppendLine();
        sb.AppendLine("                void LoadTree()");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.BeginUpdate();");
        sb.AppendLine("                        tree.Nodes.Clear();");
        sb.AppendLine("                        visitedObjects.Clear();");
        sb.AppendLine();
        sb.AppendLine("                        var rootNode = new TreeNode(\"Server ViewModel Properties\");");
        sb.AppendLine("                        tree.Nodes.Add(rootNode);");
        sb.AppendLine();
        sb.AppendLine("                        // Use reflection to discover properties dynamically");
        sb.AppendLine("                        var properties = vm.GetType().GetProperties()");
        sb.AppendLine("                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)");
        sb.AppendLine("                            .ToList();");
        sb.AppendLine();
        sb.AppendLine("                        foreach (var prop in properties)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            try");
        sb.AppendLine("                            {");
        sb.AppendLine("                                var propNode = CreatePropertyTreeNode(prop, vm, 0);");
        sb.AppendLine("                                if (propNode != null)");
        sb.AppendLine("                                    rootNode.Nodes.Add(propNode);");
        sb.AppendLine("                            }");
        sb.AppendLine("                            catch");
        sb.AppendLine("                            {");
        sb.AppendLine("                                var errorNode = new TreeNode($\"{prop.Name}: <error>\");");
        sb.AppendLine("                                rootNode.Nodes.Add(errorNode);");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine("                        rootNode.Expand();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.Nodes.Clear();");
        sb.AppendLine("                        var errorNode = new TreeNode($\"Error loading properties: {ex.Message}\");");
        sb.AppendLine("                        tree.Nodes.Add(errorNode);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    finally");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.EndUpdate();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                TreeNode? CreatePropertyTreeNode(System.Reflection.PropertyInfo prop, object obj, int depth)");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        // Prevent infinite recursion with depth limit and cycle detection");
        sb.AppendLine("                        if (depth > 5) return null;");
        sb.AppendLine();
        sb.AppendLine("                        var value = prop.GetValue(obj);");
        sb.AppendLine("                        var displayValue = value?.ToString() ?? \"<null>\";");
        sb.AppendLine();
        sb.AppendLine("                        // Cycle detection - prevent infinite recursion");
        sb.AppendLine("                        if (value != null && !IsSimpleType(prop.PropertyType))");
        sb.AppendLine("                        {");
        sb.AppendLine("                            if (visitedObjects.Contains(value))");
        sb.AppendLine("                            {");
        sb.AppendLine("                                var circularNode = new TreeNode($\"{prop.Name}: [Circular Reference]\");");
        sb.AppendLine("                                circularNode.Tag = CreatePropertyNodeInfo(prop.Name, value, true, false, false, false, false, false, -1);");
        sb.AppendLine("                                return circularNode;");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine("                        // For collections, show count information");
        sb.AppendLine("                        bool isCollection = IsCollectionType(prop.PropertyType);");
        sb.AppendLine("                        if (isCollection && value != null)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            var countProp = value.GetType().GetProperty(\"Count\") ?? value.GetType().GetProperty(\"Length\");");
        sb.AppendLine("                            if (countProp != null)");
        sb.AppendLine("                            {");
        sb.AppendLine("                                var count = countProp.GetValue(value);");
        sb.AppendLine("                                displayValue = $\"[{count} items]\";");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine("                        var propNode = new TreeNode($\"{prop.Name}: {displayValue}\");");
        sb.AppendLine();
        sb.AppendLine("                        // Create PropertyNodeInfo with appropriate flags");
        sb.AppendLine("                        bool isSimple = IsSimpleType(prop.PropertyType);");
        sb.AppendLine("                        bool isBool = prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?);");
        sb.AppendLine("                        bool isEnum = prop.PropertyType.IsEnum;");
        sb.AppendLine("                        bool isComplex = !isSimple && !isCollection && !isBool && !isEnum;");
        sb.AppendLine();
        sb.AppendLine("                        propNode.Tag = CreatePropertyNodeInfo(prop.Name, value, isSimple, isBool, isEnum, isCollection, isComplex, false, -1);");
        sb.AppendLine();
        sb.AppendLine("                        // For complex objects, try to expand their properties");
        sb.AppendLine("                        if (value != null && !isSimple)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            try");
        sb.AppendLine("                            {");
        sb.AppendLine("                                // Add to visited objects to prevent cycles");
        sb.AppendLine("                                visitedObjects.Add(value);");
        sb.AppendLine();
        sb.AppendLine("                                if (isCollection)");
        sb.AppendLine("                                {");
        sb.AppendLine("                                    // For collections, show first few items");
        sb.AppendLine("                                    if (value is System.Collections.IEnumerable enumerable)");
        sb.AppendLine("                                    {");
        sb.AppendLine("                                        int itemIndex = 0;");
        sb.AppendLine("                                        foreach (var item in enumerable)");
        sb.AppendLine("                                        {");
        sb.AppendLine("                                            if (itemIndex >= 3) break; // Limit to first 3 items");
        sb.AppendLine("                                            if (item == null) continue;");
        sb.AppendLine();
        sb.AppendLine("                                            var itemProperties = item.GetType().GetProperties()");
        sb.AppendLine("                                                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)");
        sb.AppendLine("                                                .Take(5)");
        sb.AppendLine("                                                .ToList();");
        sb.AppendLine();
        sb.AppendLine("                                            var itemNode = new TreeNode($\"[{itemIndex}] {item.GetType().Name}\");");
        sb.AppendLine("                                            itemNode.Tag = CreatePropertyNodeInfo($\"[{itemIndex}]\", item, false, false, false, false, true, true, itemIndex);");
        sb.AppendLine();
        sb.AppendLine("                                            foreach (var itemProp in itemProperties)");
        sb.AppendLine("                                            {");
        sb.AppendLine("                                                var childNode = CreatePropertyTreeNode(itemProp, item, depth + 1);");
        sb.AppendLine("                                                if (childNode != null)");
        sb.AppendLine("                                                    itemNode.Nodes.Add(childNode);");
        sb.AppendLine("                                            }");
        sb.AppendLine();
        sb.AppendLine("                                            propNode.Nodes.Add(itemNode);");
        sb.AppendLine("                                            itemIndex++;");
        sb.AppendLine("                                        }");
        sb.AppendLine("                                    }");
        sb.AppendLine("                                }");
        sb.AppendLine("                                else");
        sb.AppendLine("                                {");
        sb.AppendLine("                                    // For other complex objects, show their properties");
        sb.AppendLine("                                    var childProperties = value.GetType().GetProperties()");
        sb.AppendLine("                                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)");
        sb.AppendLine("                                        .Take(10) // Limit depth to prevent UI overload");
        sb.AppendLine("                                        .ToList();");
        sb.AppendLine();
        sb.AppendLine("                                    foreach (var childProp in childProperties)");
        sb.AppendLine("                                    {");
        sb.AppendLine("                                        var childNode = CreatePropertyTreeNode(childProp, value, depth + 1);");
        sb.AppendLine("                                        if (childNode != null)");
        sb.AppendLine("                                            propNode.Nodes.Add(childNode);");
        sb.AppendLine("                                    }");
        sb.AppendLine("                                }");
        sb.AppendLine();
        sb.AppendLine("                                // Remove from visited objects when done");
        sb.AppendLine("                                visitedObjects.Remove(value);");
        sb.AppendLine("                            }");
        sb.AppendLine("                            catch");
        sb.AppendLine("                            {");
        sb.AppendLine("                                // Ignore child property errors and remove from visited set");
        sb.AppendLine("                                if (value != null)");
        sb.AppendLine("                                    visitedObjects.Remove(value);");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine("                        return propNode;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch");
        sb.AppendLine("                    {");
        sb.AppendLine("                        return null;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Helper methods for type checking");
        sb.AppendLine("                bool IsSimpleType(Type type)");
        sb.AppendLine("                {");
        sb.AppendLine("                    return type.IsPrimitive ||");
        sb.AppendLine("                           type == typeof(string) ||");
        sb.AppendLine("                           type == typeof(DateTime) ||");
        sb.AppendLine("                           type == typeof(decimal) ||");
        sb.AppendLine("                           type == typeof(Guid) ||");
        sb.AppendLine("                           type.IsEnum ||");
        sb.AppendLine("                           (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                bool IsCollectionType(Type type)");
        sb.AppendLine("                {");
        sb.AppendLine("                    return type != typeof(string) &&");
        sb.AppendLine("                           typeof(System.Collections.IEnumerable).IsAssignableFrom(type);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                PropertyNodeInfo CreatePropertyNodeInfo(string name, object? obj, bool isSimple, bool isBool, bool isEnum, bool isCollection, bool isComplex, bool isCollectionItem, int collectionIndex)");
        sb.AppendLine("                {");
        sb.AppendLine("                    return new PropertyNodeInfo");
        sb.AppendLine("                    {");
        sb.AppendLine("                        PropertyName = name,");
        sb.AppendLine("                        Object = obj,");
        sb.AppendLine("                        IsSimpleProperty = isSimple,");
        sb.AppendLine("                        IsBooleanProperty = isBool,");
        sb.AppendLine("                        IsEnumProperty = isEnum,");
        sb.AppendLine("                        IsCollectionProperty = isCollection,");
        sb.AppendLine("                        IsComplexProperty = isComplex,");
        sb.AppendLine("                        IsCollectionItem = isCollectionItem,");
        sb.AppendLine("                        CollectionIndex = collectionIndex");
        sb.AppendLine("                    };");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Load initial tree");
        sb.AppendLine("                LoadTree();");
        sb.AppendLine();
        
        // Generate property change monitoring
        sb.Append(GeneratePropertyChangeMonitoring());
        sb.AppendLine();
        
        sb.AppendLine("                Application.Run(form);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine(\"SERVER_ERROR_START\");");
        sb.AppendLine("                Console.WriteLine(ex);");
        sb.AppendLine("                Console.WriteLine(\"SERVER_ERROR_END\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // Generate property editor method
        sb.AppendLine();
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
        sb.AppendLine("            // Show property type info");
        sb.AppendLine("            var typeLabel = new Label { Text = \"Type:\", AutoSize = true };");
        sb.AppendLine("            var typeInfo = \"Unknown\";");
        sb.AppendLine("            if (nodeInfo.IsSimpleProperty) typeInfo = \"Simple Property\";");
        sb.AppendLine("            else if (nodeInfo.IsBooleanProperty) typeInfo = \"Boolean Property\";");
        sb.AppendLine("            else if (nodeInfo.IsEnumProperty) typeInfo = \"Enum Property\";");
        sb.AppendLine("            else if (nodeInfo.IsCollectionProperty) typeInfo = \"Collection Property\";");
        sb.AppendLine("            else if (nodeInfo.IsComplexProperty) typeInfo = \"Complex Property\";");
        sb.AppendLine("            var typeValue = new Label { Text = typeInfo, AutoSize = true, ForeColor = Color.Blue };");
        sb.AppendLine("            detailLayout.Controls.Add(typeLabel, 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(typeValue, 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.RowCount = row;");
        sb.AppendLine("        }");
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    protected override string GenerateTreeViewStructure()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Left panel - TreeView with hierarchical property structure (like WPF)");
        sb.AppendLine("                var leftPanel = new Panel { Dock = DockStyle.Fill };");
        sb.AppendLine("                split.Panel1.Controls.Add(leftPanel);");
        sb.AppendLine();
        sb.AppendLine("                var treeLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Server ViewModel Properties\",");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 12, FontStyle.Bold),");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Dock = DockStyle.Top,");
        sb.AppendLine("                    Padding = new Padding(10, 10, 10, 5)");
        sb.AppendLine("                };");
        sb.AppendLine("                leftPanel.Controls.Add(treeLabel);");
        sb.AppendLine();
        sb.AppendLine("                // Tree control buttons (like WPF)");
        sb.AppendLine("                var treeButtonsPanel = new FlowLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    Height = 35,");
        sb.AppendLine("                    FlowDirection = FlowDirection.LeftToRight,");
        sb.AppendLine("                    AutoSize = false,");
        sb.AppendLine("                    Dock = DockStyle.Bottom,");
        sb.AppendLine("                    Padding = new Padding(10, 5, 10, 5)");
        sb.AppendLine("                };");
        sb.AppendLine("                leftPanel.Controls.Add(treeButtonsPanel);");
        sb.AppendLine();
        sb.AppendLine("                var refreshBtn = new Button { Text = \"Refresh\", Width = 70, Height = 25 };");
        sb.AppendLine("                var expandBtn = new Button { Text = \"Expand All\", Width = 80, Height = 25 };");
        sb.AppendLine("                var collapseBtn = new Button { Text = \"Collapse\", Width = 70, Height = 25 };");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(refreshBtn);");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(expandBtn);");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(collapseBtn);");
        sb.AppendLine();
        sb.AppendLine("                // TreeView with hierarchical display");
        sb.AppendLine("                var tree = new TreeView");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dock = DockStyle.Fill,");
        sb.AppendLine("                    HideSelection = false,");
        sb.AppendLine("                    ShowLines = true,");
        sb.AppendLine("                    ShowPlusMinus = true,");
        sb.AppendLine("                    ShowRootLines = true");
        sb.AppendLine("                };");
        sb.AppendLine("                leftPanel.Controls.Add(tree);");
        sb.AppendLine();
        sb.AppendLine("                // Wire up button events");
        sb.AppendLine("                refreshBtn.Click += (_, __) => LoadTree();");
        sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
        sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
        return sb.ToString();
    }

    protected override string GeneratePropertyDetailsPanel()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Right panel - Property details and server status");
        sb.AppendLine("                var rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };");
        sb.AppendLine("                split.Panel2.Controls.Add(rightPanel);");
        sb.AppendLine();
        sb.AppendLine("                var flow = new FlowLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dock = DockStyle.Top,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    FlowDirection = FlowDirection.TopDown,");
        sb.AppendLine("                    WrapContents = false,");
        sb.AppendLine("                    Padding = new Padding(10)");
        sb.AppendLine("                };");
        sb.AppendLine("                rightPanel.Controls.Add(flow);");
        sb.AppendLine();
        
        // Server information section
        sb.AppendLine("                var serverInfoGroup = new GroupBox");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Server Information\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
        sb.AppendLine("                    Padding = new Padding(15),");
        sb.AppendLine("                    Width = 380,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold)");
        sb.AppendLine("                };");
        sb.AppendLine("                flow.Controls.Add(serverInfoGroup);");
        sb.AppendLine();
        sb.AppendLine("                var serverInfoLayout = new TableLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    ColumnCount = 2,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Width = 350,");
        sb.AppendLine("                    CellBorderStyle = TableLayoutPanelCellBorderStyle.None,");
        sb.AppendLine("                    Padding = new Padding(5)");
        sb.AppendLine("                };");
        sb.AppendLine("                serverInfoGroup.Controls.Add(serverInfoLayout);");
        sb.AppendLine("                serverInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));");
        sb.AppendLine("                serverInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));");
        sb.AppendLine();
        sb.AppendLine("                // Server status");
        sb.AppendLine("                var statusLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Status:\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold),");
        sb.AppendLine("                    ForeColor = Color.Black");
        sb.AppendLine("                };");
        sb.AppendLine("                var statusValue = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Running\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 10, FontStyle.Bold),");
        sb.AppendLine("                    ForeColor = Color.Green");
        sb.AppendLine("                };");
        sb.AppendLine("                serverInfoLayout.Controls.Add(statusLabel, 0, 0);");
        sb.AppendLine("                serverInfoLayout.Controls.Add(statusValue, 1, 0);");
        sb.AppendLine();
        sb.AppendLine("                // Server port");
        sb.AppendLine("                var portLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Port:\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold),");
        sb.AppendLine("                    ForeColor = Color.Black");
        sb.AppendLine("                };");
        sb.AppendLine("                var portValue = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = port.ToString(),");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Regular),");
        sb.AppendLine("                    ForeColor = Color.Blue");
        sb.AppendLine("                };");
        sb.AppendLine("                serverInfoLayout.Controls.Add(portLabel, 0, 1);");
        sb.AppendLine("                serverInfoLayout.Controls.Add(portValue, 1, 1);");
        sb.AppendLine("                serverInfoLayout.RowCount = 2;");
        sb.AppendLine();
        
        // Enhanced property details section like WPF
        sb.AppendLine("                var detailGroup = new GroupBox");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Property Details (Server)\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
        sb.AppendLine("                    Padding = new Padding(15),");
        sb.AppendLine("                    Width = 380,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold)");
        sb.AppendLine("                };");
        sb.AppendLine("                flow.Controls.Add(detailGroup);");
        sb.AppendLine();
        sb.AppendLine("                var detailLayout = new TableLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    ColumnCount = 2,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Width = 350,");
        sb.AppendLine("                    CellBorderStyle = TableLayoutPanelCellBorderStyle.None,");
        sb.AppendLine("                    Padding = new Padding(5)");
        sb.AppendLine("                };");
        sb.AppendLine("                detailGroup.Controls.Add(detailLayout);");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));");
        sb.AppendLine();
        
        // Add initial "select property" message
        sb.AppendLine("                var selectPrompt = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Select a property in the tree to view details\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Italic),");
        sb.AppendLine("                    ForeColor = Color.Gray,");
        sb.AppendLine("                    Padding = new Padding(5)");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.Controls.Add(selectPrompt, 0, 0);");
        sb.AppendLine("                detailLayout.SetColumnSpan(selectPrompt, 2);");
        sb.AppendLine();
        
        sb.AppendLine("                // Tree selection event to update property details");
        sb.AppendLine("                tree.AfterSelect += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    ShowServerPropertyEditor(e.Node?.Tag as PropertyNodeInfo, detailLayout, vm);");
        sb.AppendLine("                };");
        return sb.ToString();
    }

    protected override string GenerateCommandButtons()
    {
        if (!Commands.Any()) return "";
        
        var sb = new StringBuilder();
        sb.AppendLine("                // Server Commands section (like WPF)");
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
        foreach (var c in Commands)
        {
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
            sb.AppendLine();
            sb.AppendLine($"                var btn{cmdIndex} = new Button");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Text = \"{baseName}\",");
            sb.AppendLine("                    Width = 120,");
            sb.AppendLine("                    Height = 30,");
            sb.AppendLine("                    Margin = new Padding(0, 0, 10, 10)");
            sb.AppendLine("                };");
            sb.AppendLine($"                btn{cmdIndex}.Click += (_, __) =>");
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
            sb.AppendLine($"                cmdFlow.Controls.Add(btn{cmdIndex});");
            cmdIndex++;
        }
        
        return sb.ToString();
    }

    protected override string GeneratePropertyChangeMonitoring()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Property change monitoring (like WPF)");
        sb.AppendLine("                if (vm is INotifyPropertyChanged inpc)");
        sb.AppendLine("                {");
        sb.AppendLine("                    inpc.PropertyChanged += (_, e) =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        try { LoadTree(); }");
        sb.AppendLine("                        catch { }");
        sb.AppendLine("                    };");
        sb.AppendLine("                }");
        return sb.ToString();
    }

    /// <summary>
    /// Convert framework-agnostic tree commands to WinForms-specific C# code
    /// </summary>
    protected override string ConvertTreeCommandsToFrameworkCode(List<TreeCommand> commands)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        foreach (var command in commands)
        {
            var indent = new string(' ', indentLevel * 4);
            
            switch (command.Type)
            {
                case TreeCommandType.BeginFunction:
                    sb.AppendLine($"{indent}void {command.Parameters[0]}()");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.EndFunction:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.Comment:
                    sb.AppendLine($"{indent}// {command.Parameters[0]}");
                    break;
                    
                case TreeCommandType.TryBegin:
                    sb.AppendLine($"{indent}try");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.TryEnd:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.CatchBegin:
                    sb.AppendLine($"{indent}catch (Exception ex)");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.CatchEnd:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.FinallyBegin:
                    sb.AppendLine($"{indent}finally");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.FinallyEnd:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.BeginUpdate:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.BeginUpdate();");
                    break;
                    
                case TreeCommandType.EndUpdate:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.EndUpdate();");
                    break;
                    
                case TreeCommandType.Clear:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Nodes.Clear();");
                    break;
                    
                case TreeCommandType.CreateNode:
                    sb.AppendLine($"{indent}var {command.Parameters[0]} = new TreeNode({command.Parameters[1]});");
                    break;
                    
                case TreeCommandType.AddToTree:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Nodes.Add({command.Parameters[1]});");
                    break;
                    
                case TreeCommandType.AddChildNode:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Nodes.Add({command.Parameters[1]});");
                    break;
                    
                case TreeCommandType.ExpandNode:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Expand();");
                    break;
                    
                case TreeCommandType.SetNodeTag:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Tag = new PropertyNodeInfo {{ PropertyName = {command.Parameters[1]}, Object = {command.Parameters[2]}, {command.Parameters[3]} }};");
                    break;
                    
                case TreeCommandType.AssignValue:
                    sb.AppendLine($"{indent}var {command.Parameters[0]} = {command.Parameters[1]};");
                    break;
                    
                case TreeCommandType.IfNotNull:
                    sb.AppendLine($"{indent}if ({command.Parameters[0]} != null)");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.Else:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.EndIf:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.ForEach:
                    sb.AppendLine($"{indent}foreach (var {command.Parameters[0]} in {command.Parameters[1]})");
                    sb.AppendLine($"{indent}{{");
                    indentLevel++;
                    break;
                    
                case TreeCommandType.EndForEach:
                    indentLevel--;
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case TreeCommandType.IfBreak:
                    sb.AppendLine($"{indent}if ({command.Parameters[0]}) break; // {command.Parameters[1]}");
                    break;
                    
                case TreeCommandType.Increment:
                    sb.AppendLine($"{indent}{command.Parameters[0]}++;");
                    break;
            }
        }
        
        return sb.ToString();
    }

    // WinForms-specific tree operations
    protected override string GenerateTreeBeginUpdate(string treeVariableName) => $"{treeVariableName}.BeginUpdate();";
    protected override string GenerateTreeEndUpdate(string treeVariableName) => $"{treeVariableName}.EndUpdate();";
    protected override string GenerateTreeClear(string treeVariableName) => $"{treeVariableName}.Nodes.Clear();";
    protected override string GenerateCreateTreeNode(string text) => $"new TreeNode({text})";
    protected override string GenerateAddTreeNode(string treeVariableName, string nodeVariableName) => $"{treeVariableName}.Nodes.Add({nodeVariableName});";
    protected override string GenerateAddChildTreeNode(string parentNodeVariableName, string childNodeVariableName) => $"{parentNodeVariableName}.Nodes.Add({childNodeVariableName});";
    protected override string GenerateExpandTreeNode(string nodeVariableName) => $"{nodeVariableName}.Expand();";
    protected override string GenerateSetTreeNodeTag(string nodeVariableName, string propertyName, string objectReference, string additionalProperties) =>
        $"{nodeVariableName}.Tag = new PropertyNodeInfo {{ PropertyName = {propertyName}, Object = {objectReference}, {additionalProperties} }};";
}