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
        sb.AppendLine("        [STAThread]");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            int port = 50052;");
        sb.AppendLine("            if (args.Length > 0 && int.TryParse(args[0], out var parsed))");
        sb.AppendLine("            {");
        sb.AppendLine("                port = parsed;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine($\"Starting server with GUI on port {port}...\");");
        sb.AppendLine("                var serverOptions = new ServerOptions { Port = port, UseHttps = true };");
        sb.AppendLine($"                var vm = new {ModelName}(serverOptions);");
        sb.AppendLine("                Console.WriteLine($\"Server ready on port {port}\");");
        sb.AppendLine();
        sb.AppendLine("                Application.EnableVisualStyles();");
        sb.AppendLine("                var form = new Form");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Text = \"Server GUI - {ProjectName}\",");
        sb.AppendLine("                    Width = 1150,");
        sb.AppendLine("                    Height = 780,");
        sb.AppendLine("                    StartPosition = FormStartPosition.CenterScreen");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("                var split = new SplitContainer");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dock = DockStyle.Fill,");
        sb.AppendLine("                    SplitterDistance = 480");
        sb.AppendLine("                };");
        sb.AppendLine("                form.Controls.Add(split);");
        sb.AppendLine();
        sb.AppendLine("                var statusStrip = new StatusStrip();");
        sb.AppendLine("                var statusLabel = new ToolStripStatusLabel { Text = \"Server Status: Running\" };");
        sb.AppendLine("                statusStrip.Items.Add(statusLabel);");
        sb.AppendLine("                statusStrip.Dock = DockStyle.Bottom;");
        sb.AppendLine("                form.Controls.Add(statusStrip);");
        sb.AppendLine();
        sb.AppendLine("                var tree = new TreeView { Dock = DockStyle.Fill };");
        sb.AppendLine("                split.Panel1.Controls.Add(tree);");
        sb.AppendLine();
        sb.AppendLine("                var treeToolbar = new FlowLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dock = DockStyle.Top,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
        sb.AppendLine("                    Padding = new Padding(6)");
        sb.AppendLine("                };");
        sb.AppendLine("                var refreshBtn = new Button { Text = \"Refresh\" };");
        sb.AppendLine("                var expandBtn = new Button { Text = \"Expand All\" };");
        sb.AppendLine("                var collapseBtn = new Button { Text = \"Collapse\" };");
        sb.AppendLine("                treeToolbar.Controls.AddRange(new Control[] { refreshBtn, expandBtn, collapseBtn });");
        sb.AppendLine("                split.Panel1.Controls.Add(treeToolbar);");
        sb.AppendLine();
        sb.AppendLine("                var detailLayout = new TableLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dock = DockStyle.Fill,");
        sb.AppendLine("                    ColumnCount = 2,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
        sb.AppendLine("                    Padding = new Padding(6)");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));");
        sb.AppendLine("                split.Panel2.Controls.Add(detailLayout);");
        sb.AppendLine();

        if (Commands.Count > 0)
        {
            sb.AppendLine("                var commandPanel = new FlowLayoutPanel");
            sb.AppendLine("                {");
            sb.AppendLine("                    Dock = DockStyle.Bottom,");
            sb.AppendLine("                    AutoSize = true,");
            sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
            sb.AppendLine("                    FlowDirection = FlowDirection.LeftToRight,");
            sb.AppendLine("                    Padding = new Padding(6)");
            sb.AppendLine("                };");
            int buttonIndex = 0;
            foreach (var command in Commands)
            {
                var baseName = command.MethodName.EndsWith("Async", StringComparison.Ordinal)
                    ? command.MethodName[..^"Async".Length]
                    : command.MethodName;
                sb.AppendLine($"                var btn{buttonIndex} = new Button {{ Text = \"{baseName}\", AutoSize = true }};");
                sb.AppendLine($"                btn{buttonIndex}.Enabled = false; // Server UI is read-only for commands");
                sb.AppendLine($"                commandPanel.Controls.Add(btn{buttonIndex});");
                buttonIndex++;
            }
            sb.AppendLine("                split.Panel2.Controls.Add(commandPanel);");
            sb.AppendLine();
        }

        sb.AppendLine("                refreshBtn.Click += (_, __) => LoadTree();");
        sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
        sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
        sb.AppendLine();
        sb.AppendLine("                tree.AfterSelect += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    ShowServerPropertyEditor(e.Node?.Tag as PropertyNodeInfo, detailLayout, vm);");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("                // Hierarchical property tree loading like WPF");
        sb.AppendLine(Indent(GenerateReflectionBasedTreeLogic("tree", "vm"), "                "));
        sb.AppendLine(Indent("""
TreeNode? CreatePropertyTreeNode(System.Reflection.PropertyInfo prop, object obj, int depth, HashSet<object> visitedObjects)
{
    try
    {
        if (depth > 5) return null;

        var value = prop.GetValue(obj);
        var displayValue = value?.ToString() ?? "<null>";

        if (value != null && !IsSimpleType(prop.PropertyType))
        {
            if (visitedObjects.Contains(value))
            {
                var circularNode = new TreeNode($"{prop.Name}: [Circular Reference]");
                circularNode.Tag = CreatePropertyNodeInfo(prop.Name, value, true, false, false, false, false, false, -1);
                return circularNode;
            }
        }

        bool isCollection = IsCollectionType(prop.PropertyType);
        if (isCollection && value != null)
        {
            var countProp = value.GetType().GetProperty("Count") ?? value.GetType().GetProperty("Length");
            if (countProp != null)
            {
                var count = countProp.GetValue(value);
                displayValue = $"[{count} items]";
            }
        }

        var propNode = new TreeNode($"{prop.Name}: {displayValue}");

        bool isSimple = IsSimpleType(prop.PropertyType);
        bool isBool = prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?);
        bool isEnum = prop.PropertyType.IsEnum;
        bool isComplex = !isSimple && !isCollection && !isBool && !isEnum;

        propNode.Tag = CreatePropertyNodeInfo(prop.Name, value, isSimple, isBool, isEnum, isCollection, isComplex, false, -1);

        if (value != null && !isSimple)
        {
            try
            {
                visitedObjects.Add(value);

                if (isCollection && value is System.Collections.IEnumerable enumerable)
                {
                    int itemIndex = 0;
                    foreach (var item in enumerable)
                    {
                        if (itemIndex >= 3) break;
                        if (item == null) continue;

                        var itemProperties = item.GetType().GetProperties()
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                            .Take(5)
                            .ToList();

                        var itemNode = new TreeNode($"[{itemIndex}] {item.GetType().Name}");
                        itemNode.Tag = CreatePropertyNodeInfo($"[{itemIndex}]", item, false, false, false, false, true, true, itemIndex);

                        foreach (var itemProp in itemProperties)
                        {
                            var childNode = CreatePropertyTreeNode(itemProp, item, depth + 1, visitedObjects);
                            if (childNode != null)
                                itemNode.Nodes.Add(childNode);
                        }

                        propNode.Nodes.Add(itemNode);
                        itemIndex++;
                    }
                }
                else if (!isCollection)
                {
                    var childProperties = value.GetType().GetProperties()
                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                        .Take(10)
                        .ToList();

                    foreach (var childProp in childProperties)
                    {
                        var childNode = CreatePropertyTreeNode(childProp, value, depth + 1, visitedObjects);
                        if (childNode != null)
                            propNode.Nodes.Add(childNode);
                    }
                }

                visitedObjects.Remove(value);
            }
            catch
            {
                if (value != null)
                    visitedObjects.Remove(value);
            }
        }

        return propNode;
    }
    catch
    {
        return null;
    }
}

bool IsSimpleType(Type type)
{
    return type.IsPrimitive ||
           type == typeof(string) ||
           type == typeof(DateTime) ||
           type == typeof(decimal) ||
           type == typeof(Guid) ||
           type.IsEnum ||
           (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
}

bool IsCollectionType(Type type)
{
    return type != typeof(string) &&
           typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
}

PropertyNodeInfo CreatePropertyNodeInfo(string name, object? obj, bool isSimple, bool isBool, bool isEnum, bool isCollection, bool isComplex, bool isCollectionItem, int collectionIndex)
{
    return new PropertyNodeInfo
    {
        PropertyName = name,
        Object = obj,
        IsSimpleProperty = isSimple,
        IsBooleanProperty = isBool,
        IsEnumProperty = isEnum,
        IsCollectionProperty = isCollection,
        IsComplexProperty = isComplex,
        IsCollectionItem = isCollectionItem,
        CollectionIndex = collectionIndex
    };
}
""", "                "));
        sb.AppendLine();
        sb.AppendLine("                // Load initial tree");
        sb.AppendLine("                LoadTree();");
        sb.AppendLine();
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
        sb.AppendLine();
        sb.AppendLine("        private static void ShowServerPropertyEditor(PropertyNodeInfo? nodeInfo, TableLayoutPanel detailLayout, object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            detailLayout.SuspendLayout();");
        sb.AppendLine("            foreach (Control control in detailLayout.Controls.OfType<Control>().ToArray())");
        sb.AppendLine("            {");
        sb.AppendLine("                detailLayout.Controls.Remove(control);");
        sb.AppendLine("                control.Dispose();");
        sb.AppendLine("            }");
        sb.AppendLine("            detailLayout.RowStyles.Clear();");
        sb.AppendLine("            detailLayout.RowCount = 0;");
        sb.AppendLine();
        sb.AppendLine("            if (nodeInfo?.Object == null)");
        sb.AppendLine("            {");
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
        sb.AppendLine("                detailLayout.ResumeLayout();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            int row = 0;");
        sb.AppendLine();
        sb.AppendLine("            void AddRow(Control label, Control value)");
        sb.AppendLine("            {");
        sb.AppendLine("                label.Margin = new Padding(0, 0, 6, 6);");
        sb.AppendLine("                value.Margin = new Padding(0, 0, 0, 6);");
        sb.AppendLine("                detailLayout.Controls.Add(label, 0, row);");
        sb.AppendLine("                detailLayout.Controls.Add(value, 1, row);");
        sb.AppendLine("                detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));");
        sb.AppendLine("                row++;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            AddRow(new Label { Text = \"Property:\", AutoSize = true, Font = new Font(\"Segoe UI\", 9, FontStyle.Bold) },");
        sb.AppendLine("                    new Label { Text = nodeInfo.PropertyName, AutoSize = true });");
        sb.AppendLine();
        sb.AppendLine("            string typeInfo = nodeInfo.IsSimpleProperty ? \"Simple Property\" :");
        sb.AppendLine("                nodeInfo.IsBooleanProperty ? \"Boolean Property\" :");
        sb.AppendLine("                nodeInfo.IsEnumProperty ? \"Enum Property\" :");
        sb.AppendLine("                nodeInfo.IsCollectionProperty ? \"Collection Property\" :");
        sb.AppendLine("                nodeInfo.IsComplexProperty ? \"Complex Property\" : \"Unknown\";");
        sb.AppendLine("            AddRow(new Label { Text = \"Type:\", AutoSize = true },");
        sb.AppendLine("                    new Label { Text = typeInfo, AutoSize = true, ForeColor = Color.Blue });");
        sb.AppendLine();
        sb.AppendLine("            if (nodeInfo.IsCollectionItem)");
        sb.AppendLine("            {");
        sb.AppendLine("                AddRow(new Label { Text = \"Index:\", AutoSize = true },");
        sb.AppendLine("                        new Label { Text = nodeInfo.CollectionIndex.ToString(), AutoSize = true });");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            AddRow(new Label { Text = \"Object Type:\", AutoSize = true },");
        sb.AppendLine("                    new Label { Text = nodeInfo.Object?.GetType().Name ?? \"<null>\", AutoSize = true });");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.RowCount = row;");
        sb.AppendLine("            detailLayout.ResumeLayout();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Indent(string text, string indent)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            // Skip a trailing empty line to avoid indenting an extra blank line
            if (i == lines.Length - 1 && string.IsNullOrEmpty(lines[i]))
            {
                continue;
            }

            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(indent);
            builder.Append(lines[i]);
        }

        return builder.ToString();
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