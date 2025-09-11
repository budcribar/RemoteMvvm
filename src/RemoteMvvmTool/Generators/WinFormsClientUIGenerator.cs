using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// WinForms-specific client UI generator that produces hierarchical property display
/// </summary>
public class WinFormsClientUIGenerator : UIGeneratorBase
{
    private readonly string ClientClassName;
    private readonly string ClientNamespace;

    public WinFormsClientUIGenerator(string projectName, string modelName, List<GrpcRemoteMvvmModelUtil.PropertyInfo> properties, List<CommandInfo> commands, string clientClassName, string clientNamespace)
        : base(projectName, modelName, properties, commands, "Client")
    {
        ClientClassName = clientClassName;
        ClientNamespace = clientNamespace;
    }

    public override string GenerateProgram(string protoNs, string serviceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.Clients;");
        sb.AppendLine("using Generated.ViewModels;");
        sb.AppendLine("using System.Windows.Forms;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine();
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
        sb.AppendLine("    " + GeneratePropertyNodeInfoClass().Replace("\n", "\n    "));
        sb.AppendLine();
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        [STAThread]");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                int port = 50052;");
        sb.AppendLine("                if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;");
        sb.AppendLine();
        sb.AppendLine("                var handler = new HttpClientHandler();");
        sb.AppendLine("                handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;");
        sb.AppendLine("                var channel = GrpcChannel.ForAddress(new Uri(\"https://localhost:\" + port + \"/\"), new GrpcChannelOptions { HttpHandler = handler });");
        sb.AppendLine($"                var grpcClient = new {serviceName}.{serviceName}Client(channel);");
        sb.AppendLine($"                var vm = new {ClientClassName}(grpcClient);");
        sb.AppendLine("                vm.InitializeRemoteAsync().GetAwaiter().GetResult();");
        sb.AppendLine();
        sb.AppendLine("                Application.EnableVisualStyles();");
        sb.AppendLine($"                var form = new Form");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Text = \"{GetWindowTitle()}\",");
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
        sb.AppendLine($"                statusLbl.Text = \"{GetStatusText()}\";");
        sb.AppendLine();
        
        var uiTranslator = new WinFormsUITranslator();

        // Generate TreeView structure
        sb.Append(uiTranslator.Translate(GenerateTreeViewStructure(), "                "));
        sb.AppendLine();
        sb.AppendLine("                refreshBtn.Click += (_, __) => LoadTree();");
        sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
        sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
        sb.AppendLine();

        // Generate property details panel
        sb.Append(uiTranslator.Translate(GeneratePropertyDetailsPanel(), "                "));
        sb.AppendLine();
        sb.AppendLine("                tree.AfterSelect += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    ShowClientPropertyEditor(e.Node?.Tag as PropertyNodeInfo, detailLayout, vm);");
        sb.AppendLine("                };");
        sb.AppendLine();

        // Generate command buttons
        sb.Append(uiTranslator.Translate(GenerateCommandButtons(), "                "));
        sb.AppendLine();
        
        // Generate hierarchical tree loading using reflection-based approach like WPF
        sb.AppendLine("                // Hierarchical property tree loading like WPF");
        sb.AppendLine("                void LoadTree()");
        sb.AppendLine("                {");
        sb.AppendLine("                    var visitedObjects = new HashSet<object>();");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.BeginUpdate();");
        sb.AppendLine("                        tree.Nodes.Clear();");
        sb.AppendLine();
        sb.AppendLine("                        var rootNode = new TreeNode(\"Client ViewModel Properties\");");
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
        sb.AppendLine("                                var propNode = CreatePropertyTreeNode(prop, vm, 0, visitedObjects);");
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
        sb.AppendLine("                TreeNode? CreatePropertyTreeNode(System.Reflection.PropertyInfo prop, object obj, int depth, HashSet<object> visitedObjects)");
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
        sb.AppendLine("                                                var childNode = CreatePropertyTreeNode(itemProp, item, depth + 1, visitedObjects);");
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
        sb.AppendLine("                                        var childNode = CreatePropertyTreeNode(childProp, value, depth + 1, visitedObjects);");
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
        
        // Generate try..catch for initial tree load
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    LoadTree();");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine("                    // Handle any errors during initial load");
        sb.AppendLine("                    MessageBox.Show($\"Error loading tree: {ex.Message}\", \"Tree Load Error\", MessageBoxButtons.OK, MessageBoxIcon.Error);");
        sb.AppendLine("                }");
        sb.AppendLine();
        
        // Generate property change monitoring
        sb.Append(GeneratePropertyChangeMonitoring());
        sb.AppendLine();
        
        sb.AppendLine("                Application.Run(form);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine(\"CLIENT_ERROR_START\");");
        sb.AppendLine("                Console.WriteLine(ex);");
        sb.AppendLine("                Console.WriteLine(\"CLIENT_ERROR_END\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // Generate property editor method
        sb.AppendLine();
        sb.AppendLine("        private static void ShowClientPropertyEditor(PropertyNodeInfo? nodeInfo, TableLayoutPanel detailLayout, object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Clear existing controls");
        sb.AppendLine("            foreach (Control control in detailLayout.Controls.OfType<Control>().ToArray())");
        sb.AppendLine("            {");
        sb.AppendLine("                detailLayout.Controls.Remove(control);");
        sb.AppendLine("                control.Dispose();");
        sb.AppendLine("            }");
        sb.AppendLine("            detailLayout.RowCount = 0;");
        sb.AppendLine();
        sb.AppendLine("            if (nodeInfo?.Object == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                // Show default \"select property\" message");
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
        sb.AppendLine("                detailLayout.RowCount = 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            int row = 0;");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                // Property name");
        sb.AppendLine("                var nameLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Property:\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold),");
        sb.AppendLine("                    ForeColor = Color.Black");
        sb.AppendLine("                };");
        sb.AppendLine("                var nameValue = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = nodeInfo.PropertyName,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Regular),");
        sb.AppendLine("                    ForeColor = Color.DarkBlue");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.Controls.Add(nameLabel, 0, row);");
        sb.AppendLine("                detailLayout.Controls.Add(nameValue, 1, row);");
        sb.AppendLine("                row++;");
        sb.AppendLine();
        sb.AppendLine("                // Property type info");
        sb.AppendLine("                var typeLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Type:\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Bold),");
        sb.AppendLine("                    ForeColor = Color.Black");
        sb.AppendLine("                };");
        sb.AppendLine("                var typeInfo = \"Unknown\";");
        sb.AppendLine("                var typeColor = Color.Gray;");
        sb.AppendLine();
        sb.AppendLine("                if (nodeInfo.IsSimpleProperty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = \"Simple Property\";");
        sb.AppendLine("                    typeColor = Color.Green;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (nodeInfo.IsBooleanProperty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = \"Boolean Property\";");
        sb.AppendLine("                    typeColor = Color.Blue;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (nodeInfo.IsEnumProperty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = \"Enum Property\";");
        sb.AppendLine("                    typeColor = Color.Purple;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (nodeInfo.IsCollectionProperty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = \"Collection Property\";");
        sb.AppendLine("                    typeColor = Color.Orange;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (nodeInfo.IsComplexProperty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = \"Complex Property\";");
        sb.AppendLine("                    typeColor = Color.Red;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (nodeInfo.IsCollectionItem)");
        sb.AppendLine("                {");
        sb.AppendLine("                    typeInfo = $\"Collection Item [{{nodeInfo.CollectionIndex}}]\";");
        sb.AppendLine("                    typeColor = Color.DarkOrange;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                var typeValue = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = typeInfo,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Regular),");
        sb.AppendLine("                    ForeColor = typeColor");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.Controls.Add(typeLabel, 0, row);");
        sb.AppendLine("                detailLayout.Controls.Add(typeValue, 1, row);");
        sb.AppendLine("                row++;");
        sb.AppendLine();
        sb.AppendLine("                detailLayout.RowCount = row;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                // Error displaying details");
        sb.AppendLine("                var errorLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = $\"Error displaying details: {{ex.Message}}\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Font = new Font(\"Segoe UI\", 9, FontStyle.Regular),");
        sb.AppendLine("                    ForeColor = Color.Red,");
        sb.AppendLine("                    MaximumSize = new Size(320, 0)");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.Controls.Add(errorLabel, 0, 0);");
        sb.AppendLine("                detailLayout.SetColumnSpan(errorLabel, 2);");
        sb.AppendLine("                detailLayout.RowCount = 1;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    protected override UIComponent GenerateTreeViewStructure()
    {
        var root = new UIComponent("StackPanel");
        root.Children.Add(new UIComponent("TreeView", "tree"));
        root.Children.Add(new UIComponent("Button", "refreshBtn", "Refresh"));
        root.Children.Add(new UIComponent("Button", "expandBtn", "Expand All"));
        root.Children.Add(new UIComponent("Button", "collapseBtn", "Collapse"));
        return root;
    }

    protected override UIComponent GeneratePropertyDetailsPanel()
    {
        return new UIComponent("TableLayoutPanel", "detailLayout");
    }

    protected override UIComponent GenerateCommandButtons()
    {
        var root = new UIComponent("StackPanel");
        int cmdIndex = 0;
        foreach (var c in Commands)
        {
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
            root.Children.Add(new UIComponent("Button", $"btn{cmdIndex}", baseName));
            cmdIndex++;
        }
        return root;
    }

    protected override string GeneratePropertyChangeMonitoring()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Property change monitoring");
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