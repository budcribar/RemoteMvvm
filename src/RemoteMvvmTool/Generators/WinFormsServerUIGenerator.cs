using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.UIComponents;
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
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("    " + GeneratePropertyNodeInfoClass().Replace("\n", "\n    "));
        sb.AppendLine();
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
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
        
        var uiTranslator = new WinFormsUITranslator();
        sb.Append(uiTranslator.Translate(GenerateTreeViewStructure(), "                ", "split.Panel1"));
        sb.Append(uiTranslator.Translate(GenerateCommandButtons(), "                ", "split.Panel2"));
        sb.Append(uiTranslator.Translate(GeneratePropertyDetailsPanel(), "                ", "split.Panel2"));
        sb.Append(uiTranslator.Translate(GeneratePropertyChangeMonitoring(), "                "));
        sb.AppendLine();
        sb.AppendLine("                detailLayout.AutoScroll = true;");
        sb.AppendLine("                detailLayout.ColumnCount = 2;");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));");
        sb.AppendLine("                detailLayout.RowStyles.Clear();");
        sb.AppendLine("                refreshBtn.Click += (_, __) => RefreshTreeView();");
        sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
        sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
        sb.AppendLine("                saveBtn.Click += (_, __) => SaveViewModel(vm);");
        sb.AppendLine("                loadBtn.Click += (_, __) => { LoadViewModel(vm); RefreshTreeView(); };");
        sb.AppendLine();
        sb.AppendLine("                tree.AfterSelect += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    ShowServerPropertyEditor(e.Node, detailLayout, path => RefreshTreeView(path));");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("                // Populate the tree using compile-time metadata");
        sb.Append(IndentCodeBlock(GenerateHierarchicalTreeLogic("tree", "vm", ModelName), "                "));
        sb.AppendLine();
        sb.AppendLine("                TreeNode? FindNodeByPath(TreeNodeCollection nodes, string path)");
        sb.AppendLine("                {");
        sb.AppendLine("                    foreach (TreeNode node in nodes)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (node.Tag is PropertyNodeInfo info && string.Equals(info.PropertyPath, path, StringComparison.Ordinal))");
        sb.AppendLine("                        {");
        sb.AppendLine("                            return node;");
        sb.AppendLine("                        }");
        sb.AppendLine("                        var child = FindNodeByPath(node.Nodes, path);");
        sb.AppendLine("                        if (child != null) return child;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    return null;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                void RefreshTreeView(string? propertyPath = null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    LoadTree();");
        sb.AppendLine("                    if (!string.IsNullOrEmpty(propertyPath))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var target = FindNodeByPath(tree.Nodes, propertyPath);");
        sb.AppendLine("                        if (target != null)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            tree.SelectedNode = target;");
        sb.AppendLine("                            target.EnsureVisible();");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    UpdateServerStatus(vm, statusLbl);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Load initial tree");
        sb.AppendLine("                RefreshTreeView();");
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
        sb.AppendLine("        private static void ShowServerPropertyEditor(TreeNode? selectedNode, TableLayoutPanel detailLayout, Action<string?> refreshTree)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (Control control in detailLayout.Controls.OfType<Control>().ToArray())");
        sb.AppendLine("            {");
        sb.AppendLine("                detailLayout.Controls.Remove(control);");
        sb.AppendLine("                control.Dispose();");
            sb.AppendLine("            }");
        sb.AppendLine("            detailLayout.RowCount = 0;");
        sb.AppendLine();
        sb.AppendLine("            if (selectedNode?.Tag is not PropertyNodeInfo nodeInfo || nodeInfo.Object == null)");
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
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var owner = nodeInfo.Object;");
        sb.AppendLine("            var propertyPath = string.IsNullOrWhiteSpace(nodeInfo.PropertyPath) ? nodeInfo.PropertyName : nodeInfo.PropertyPath;");
        sb.AppendLine("            PropertyInfo? propertyInfo = null;");
        sb.AppendLine("            object? propertyValue = null;");
        sb.AppendLine("            string typeDisplay = \"Unknown\";");
        sb.AppendLine("            string valueDisplay = \"<null>\";");
        sb.AppendLine();
        sb.AppendLine("            if (!nodeInfo.IsCollectionItem && !string.IsNullOrEmpty(nodeInfo.PropertyName))");
        sb.AppendLine("            {");
        sb.AppendLine("                propertyInfo = owner.GetType().GetProperty(nodeInfo.PropertyName, BindingFlags.Public | BindingFlags.Instance);");
        sb.AppendLine("                if (propertyInfo != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        propertyValue = propertyInfo.GetValue(owner);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch");
        sb.AppendLine("                    {");
        sb.AppendLine("                        propertyValue = null;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    typeDisplay = propertyInfo.PropertyType.Name;");
        sb.AppendLine("                    valueDisplay = GetDisplayValue(propertyValue, propertyInfo.PropertyType);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                propertyValue = owner;");
        sb.AppendLine("                typeDisplay = owner.GetType().Name;");
        sb.AppendLine("                valueDisplay = owner?.ToString() ?? \"<null>\";");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            int row = 0;");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.Controls.Add(CreateHeaderLabel(\"Property:\"), 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(CreateValueLabel(nodeInfo.PropertyName), 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.Controls.Add(CreateHeaderLabel(\"Type:\"), 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(CreateValueLabel(typeDisplay), 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.Controls.Add(CreateHeaderLabel(\"Value:\"), 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(CreateValueLabel(valueDisplay), 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            bool isReadOnly = propertyInfo == null || IsEffectivelyReadOnly(propertyInfo) || nodeInfo.IsCollectionProperty || nodeInfo.IsComplexProperty || nodeInfo.IsCollectionItem;");
        sb.AppendLine("            detailLayout.Controls.Add(CreateHeaderLabel(\"Read-Only:\"), 0, row);");
        sb.AppendLine("            detailLayout.Controls.Add(CreateValueLabel(isReadOnly ? \"True\" : \"False\"), 1, row);");
        sb.AppendLine("            row++;");
        sb.AppendLine();
        sb.AppendLine("            if (!isReadOnly && propertyInfo != null && IsEditableType(propertyInfo.PropertyType))");
        sb.AppendLine("            {");
        sb.AppendLine("                var editor = CreateEditorControl(propertyInfo, owner, propertyPath, refreshTree);");
        sb.AppendLine("                if (editor != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    detailLayout.Controls.Add(editor, 0, row);");
        sb.AppendLine("                    detailLayout.SetColumnSpan(editor, 2);");
        sb.AppendLine("                    row++;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (nodeInfo.IsCollectionProperty)");
        sb.AppendLine("            {");
        sb.AppendLine("                var infoLabel = new Label");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Collection editing is not supported from the server UI.\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    ForeColor = Color.DarkSlateGray,");
        sb.AppendLine("                    Padding = new Padding(5, 8, 5, 0)");
        sb.AppendLine("                };");
        sb.AppendLine("                detailLayout.Controls.Add(infoLabel, 0, row);");
        sb.AppendLine("                detailLayout.SetColumnSpan(infoLabel, 2);");
        sb.AppendLine("                row++;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            detailLayout.RowCount = row;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static Label CreateHeaderLabel(string text)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Label");
        sb.AppendLine("            {");
        sb.AppendLine("                Text = text,");
        sb.AppendLine("                AutoSize = true,");
        sb.AppendLine("                Font = new Font(\"Segoe UI\", 9, FontStyle.Bold),");
        sb.AppendLine("                ForeColor = Color.Black,");
        sb.AppendLine("                Padding = new Padding(0, 4, 0, 0)");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static Label CreateValueLabel(string text)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Label");
        sb.AppendLine("            {");
        sb.AppendLine("                Text = text,");
        sb.AppendLine("                AutoSize = true,");
        sb.AppendLine("                Font = new Font(\"Segoe UI\", 9, FontStyle.Regular),");
        sb.AppendLine("                ForeColor = Color.DarkBlue,");
        sb.AppendLine("                Padding = new Padding(0, 4, 0, 0)");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static Control? CreateEditorControl(PropertyInfo propertyInfo, object owner, string propertyPath, Action<string?> refreshTree)");
        sb.AppendLine("        {");
        sb.AppendLine("            var underlying = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;");
        sb.AppendLine();
        sb.AppendLine("            if (underlying == typeof(bool))");
        sb.AppendLine("            {");
        sb.AppendLine("                bool? current = propertyInfo.GetValue(owner) as bool?;");
        sb.AppendLine("                var check = new CheckBox");
        sb.AppendLine("                {");
        sb.AppendLine("                    Checked = current ?? false,");
        sb.AppendLine("                    ThreeState = Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null,");
        sb.AppendLine("                    AutoSize = true");
        sb.AppendLine("                };");
        sb.AppendLine("                if (check.ThreeState && current == null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    check.CheckState = CheckState.Indeterminate;");
        sb.AppendLine("                }");
        sb.AppendLine("                check.CheckStateChanged += (_, __) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (check.ThreeState && check.CheckState == CheckState.Indeterminate)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            propertyInfo.SetValue(owner, null);");
        sb.AppendLine("                        }");
        sb.AppendLine("                        else");
        sb.AppendLine("                        {");
        sb.AppendLine("                            propertyInfo.SetValue(owner, check.Checked);");
        sb.AppendLine("                        }");
        sb.AppendLine("                        refreshTree(propertyPath);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        MessageBox.Show($\"Error updating property: {ex.Message}\", \"Update Error\", MessageBoxButtons.OK, MessageBoxIcon.Error);");
        sb.AppendLine("                    }");
        sb.AppendLine("                };");
        sb.AppendLine("                return check;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (underlying.IsEnum)");
        sb.AppendLine("            {");
        sb.AppendLine("                var combo = new ComboBox");
        sb.AppendLine("                {");
        sb.AppendLine("                    DropDownStyle = ComboBoxStyle.DropDownList,");
        sb.AppendLine("                    Width = 220");
        sb.AppendLine("                };");
        sb.AppendLine("                var names = Enum.GetNames(underlying);");
        sb.AppendLine("                combo.Items.AddRange(names);");
        sb.AppendLine("                var current = propertyInfo.GetValue(owner);");
        sb.AppendLine("                if (current != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var currentName = Enum.Format(underlying, current, \"G\");");
        sb.AppendLine("                    combo.SelectedItem = currentName;");
        sb.AppendLine("                }");
        sb.AppendLine("                combo.SelectedIndexChanged += (_, __) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (combo.SelectedItem is string selected)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            var parsed = Enum.Parse(underlying, selected);");
        sb.AppendLine("                            propertyInfo.SetValue(owner, parsed);");
        sb.AppendLine("                            refreshTree(propertyPath);");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        MessageBox.Show($\"Error updating property: {ex.Message}\", \"Update Error\", MessageBoxButtons.OK, MessageBoxIcon.Error);");
        sb.AppendLine("                    }");
        sb.AppendLine("                };");
        sb.AppendLine("                return combo;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var text = new TextBox");
        sb.AppendLine("            {");
        sb.AppendLine("                Text = GetDisplayValue(propertyInfo.GetValue(owner), propertyInfo.PropertyType),");
        sb.AppendLine("                Width = 240");
        sb.AppendLine("            };");
        sb.AppendLine("            void Commit()");
        sb.AppendLine("            {");
        sb.AppendLine("                if (TrySetValueFromText(propertyInfo, owner, text.Text))");
        sb.AppendLine("                {");
        sb.AppendLine("                    refreshTree(propertyPath);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    MessageBox.Show($\"Unable to convert value to {underlying.Name}.\", \"Update Error\", MessageBoxButtons.OK, MessageBoxIcon.Warning);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            text.Leave += (_, __) => Commit();");
        sb.AppendLine("            text.KeyDown += (_, e) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                if (e.KeyCode == Keys.Enter)");
        sb.AppendLine("                {");
        sb.AppendLine("                    e.SuppressKeyPress = true;");
        sb.AppendLine("                    Commit();");
        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("            return text;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static bool TrySetValueFromText(PropertyInfo propertyInfo, object owner, string text)");
        sb.AppendLine("        {");
        sb.AppendLine("            var targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                object? value;");
        sb.AppendLine("                if (string.IsNullOrWhiteSpace(text) && (Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null || !propertyInfo.PropertyType.IsValueType))");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = null;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (targetType == typeof(string)) value = text;");
        sb.AppendLine("                else if (targetType == typeof(int)) value = int.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(double)) value = double.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(float)) value = float.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(decimal)) value = decimal.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(long)) value = long.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(uint)) value = uint.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(ulong)) value = ulong.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(short)) value = short.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(byte)) value = byte.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else if (targetType == typeof(Guid)) value = Guid.Parse(text);");
        sb.AppendLine("                else if (targetType == typeof(DateTime)) value = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);");
        sb.AppendLine("                else if (targetType == typeof(TimeSpan)) value = TimeSpan.Parse(text, CultureInfo.InvariantCulture);");
        sb.AppendLine("                else value = Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);");
        sb.AppendLine();
        sb.AppendLine("                propertyInfo.SetValue(owner, value);");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static string GetDisplayValue(object? value, Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (value == null) return \"<null>\";");
        sb.AppendLine("            var runtimeType = value.GetType();");
        sb.AppendLine("            var targetType = runtimeType == typeof(object) ? (Nullable.GetUnderlyingType(type) ?? type) : runtimeType;");
        sb.AppendLine("            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;");
        sb.AppendLine("            if (underlying == typeof(DateTime) && value is DateTime dt)");
        sb.AppendLine("            {");
        sb.AppendLine("                return dt.ToString(\"o\", CultureInfo.InvariantCulture);");
        sb.AppendLine("            }");
        sb.AppendLine("            if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))");
        sb.AppendLine("            {");
        sb.AppendLine("                return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);");
        sb.AppendLine("            }");
        sb.AppendLine("            return value.ToString() ?? \"<null>\";");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static string GetDisplayValue(object? value)");
        sb.AppendLine("        {");
        sb.AppendLine("            return GetDisplayValue(value, value?.GetType() ?? typeof(object));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static int? GetCollectionCount(object? value)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (value is ICollection collection) return collection.Count;");
        sb.AppendLine("            var countProp = value?.GetType().GetProperty(\"Count\", BindingFlags.Public | BindingFlags.Instance);");
        sb.AppendLine("            if (countProp != null && countProp.PropertyType == typeof(int) && countProp.GetIndexParameters().Length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                return countProp.GetValue(value) as int?;");
        sb.AppendLine("            }");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static bool IsEditableType(Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            var underlying = Nullable.GetUnderlyingType(type) ?? type;");
        sb.AppendLine("            return underlying.IsPrimitive ||");
        sb.AppendLine("                   underlying == typeof(string) ||");
        sb.AppendLine("                   underlying == typeof(decimal) ||");
        sb.AppendLine("                   underlying == typeof(DateTime) ||");
        sb.AppendLine("                   underlying == typeof(Guid) ||");
        sb.AppendLine("                   underlying == typeof(TimeSpan) ||");
        sb.AppendLine("                   underlying.IsEnum;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static bool IsEffectivelyReadOnly(PropertyInfo property)");
        sb.AppendLine("        {");
        sb.AppendLine("            var setter = property.GetSetMethod(true);");
        sb.AppendLine("            return setter == null || setter.IsPrivate;");
        sb.AppendLine("        }");

        sb.AppendLine();
        sb.AppendLine("        private static void SaveViewModel(object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var dlg = new SaveFileDialog { Filter = \"ViewModel State (*.bin)|*.bin|All Files (*.*)|*.*\" };");
        sb.AppendLine("            if (dlg.ShowDialog() == DialogResult.OK)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var method = vm.GetType().GetMethod(\"SaveToFile\");");
        sb.AppendLine("                    if (method != null)");
        sb.AppendLine("                        method.Invoke(vm, new object[] { dlg.FileName });");
        sb.AppendLine("                    else");
        sb.AppendLine("                        MessageBox.Show(\"SaveToFile method not found on view model\", \"Save Error\", MessageBoxButtons.OK, MessageBoxIcon.Warning);");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine("                    MessageBox.Show($\"Error saving view model: {ex.Message}\", \"Save Error\", MessageBoxButtons.OK, MessageBoxIcon.Error);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void LoadViewModel(object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var dlg = new OpenFileDialog { Filter = \"ViewModel State (*.bin)|*.bin|All Files (*.*)|*.*\" };");
        sb.AppendLine("            if (dlg.ShowDialog() == DialogResult.OK)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var method = vm.GetType().GetMethod(\"LoadFromFile\");");
        sb.AppendLine("                    if (method != null)");
        sb.AppendLine("                        method.Invoke(vm, new object[] { dlg.FileName });");
        sb.AppendLine("                    else");
        sb.AppendLine("                        MessageBox.Show(\"LoadFromFile method not found on view model\", \"Load Error\", MessageBoxButtons.OK, MessageBoxIcon.Warning);");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine("                    MessageBox.Show($\"Error loading view model: {ex.Message}\", \"Load Error\", MessageBoxButtons.OK, MessageBoxIcon.Error);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void UpdateServerStatus(object vm, ToolStripStatusLabel statusLbl)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                statusLbl.Text = \"Server Status: Running\";");
        sb.AppendLine("                statusLbl.ForeColor = Color.Green;");
        sb.AppendLine();
        sb.AppendLine("                var serverOptionsProperty = vm.GetType().GetProperty(\"ServerOptions\");");
        sb.AppendLine("                if (serverOptionsProperty?.GetValue(vm) is object serverOptions)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var portProperty = serverOptions.GetType().GetProperty(\"Port\");");
        sb.AppendLine("                    if (portProperty?.GetValue(serverOptions) is int port)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        statusLbl.Text = $\"Server Status: Running on Port {port}\";");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch");
        sb.AppendLine("            {");
        sb.AppendLine("                statusLbl.Text = \"Server Status: Unknown\";");
        sb.AppendLine("                statusLbl.ForeColor = Color.Orange;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    protected override UIComponent GenerateTreeViewStructure()
    {
        var root = new ContainerComponent("StackPanel");
        root.Children.Add(new TreeViewComponent("tree"));
        root.Children.Add(new ButtonComponent("refreshBtn", "Refresh"));
        root.Children.Add(new ButtonComponent("expandBtn", "Expand All"));
        root.Children.Add(new ButtonComponent("collapseBtn", "Collapse"));
        root.Children.Add(new ButtonComponent("saveBtn", "Save"));
        root.Children.Add(new ButtonComponent("loadBtn", "Load"));
        return root;
    }

    protected override UIComponent GeneratePropertyDetailsPanel()
    {
        return new ContainerComponent("TableLayoutPanel", "detailLayout");
    }

    protected override UIComponent GenerateCommandButtons()
    {
        var root = new ContainerComponent("StackPanel");
        int cmdIndex = 0;
        foreach (var c in Commands)
        {
            var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
            root.Children.Add(new ButtonComponent($"btn{cmdIndex}", baseName));
            cmdIndex++;
        }
        return root;
    }

    protected override UIComponent GeneratePropertyChangeMonitoring()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Property change monitoring (like WPF)");
        sb.AppendLine("                if (vm is INotifyPropertyChanged inpc)");
        sb.AppendLine("                {");
        sb.AppendLine("                    inpc.PropertyChanged += (_, e) =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        try { RefreshTreeView(); }");
        sb.AppendLine("                        catch { }");
        sb.AppendLine("                    };");
        sb.AppendLine("                }");
        return new CodeBlockComponent(sb.ToString());
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