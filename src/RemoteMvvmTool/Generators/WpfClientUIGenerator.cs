using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// WPF-specific client UI generator that maintains the current XAML-based approach
/// </summary>
public class WpfClientUIGenerator : UIGeneratorBase
{
    private readonly string ClientClassName;
    private readonly string ClientNamespace;

    public WpfClientUIGenerator(string projectName, string modelName, List<PropertyInfo> properties, List<CommandInfo> commands, string clientClassName, string clientNamespace)
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
        sb.AppendLine("using System.Windows;");
        sb.AppendLine();
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
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
        sb.AppendLine("                var app = new Application();");
        sb.AppendLine("                var win = new MainWindow(vm);");
        sb.AppendLine($"                win.Title = \"{GetWindowTitle()}\";");
        sb.AppendLine("                app.Run(win);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                Console.WriteLine(\"CLIENT_ERROR_START\");");
        sb.AppendLine("                Console.WriteLine(ex);");
        sb.AppendLine("                Console.WriteLine(\"CLIENT_ERROR_END\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generate enhanced WPF code-behind that uses UIGeneratorBase tree loading logic
    /// </summary>
    public string GenerateEnhancedCodeBehind()
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Windows;");
        sb.AppendLine("using System.Windows.Controls;");
        sb.AppendLine("using Generated.Clients;");
        sb.AppendLine();
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
        
        // Add PropertyNodeInfo class
        sb.AppendLine("    " + GeneratePropertyNodeInfoClass().Replace("\n", "\n    "));
        sb.AppendLine();
        
        sb.AppendLine("    public partial class MainWindow : Window");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly object _viewModel;");
        sb.AppendLine("        private readonly System.Threading.Timer? _updateTimer;");
        sb.AppendLine();
        sb.AppendLine("        public MainWindow(object vm)");
        sb.AppendLine("        {");
        sb.AppendLine("            InitializeComponent();");
        sb.AppendLine("            DataContext = vm;");
        sb.AppendLine("            _viewModel = vm;");
        sb.AppendLine();
        sb.AppendLine("            // Wire up button events");
        sb.AppendLine("            RefreshBtn.Click += (_, __) => LoadTree();");
        sb.AppendLine("            ExpandAllBtn.Click += (_, __) => ExpandAll(PropertyTreeView);");
        sb.AppendLine("            CollapseAllBtn.Click += (_, __) => CollapseAll(PropertyTreeView);");
        sb.AppendLine();
        sb.AppendLine("            // Set up property change monitoring");
        sb.AppendLine("            if (_viewModel is INotifyPropertyChanged inpc)");
        sb.AppendLine("            {");
        sb.AppendLine("                inpc.PropertyChanged += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    Dispatcher.BeginInvoke(() => LoadTree());");
        sb.AppendLine("                };");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Set up periodic refresh");
        sb.AppendLine("            _updateTimer = new System.Threading.Timer(_ =>");
        sb.AppendLine("            {");
        sb.AppendLine("                Dispatcher.BeginInvoke(() => LoadTree());");
        sb.AppendLine("            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));");
        sb.AppendLine();
        sb.AppendLine("            // Wire up tree selection");
        sb.AppendLine("            PropertyTreeView.SelectedItemChanged += (_, e) => UpdatePropertyDetails();");
        sb.AppendLine();
        sb.AppendLine("            // Initial load");
        sb.AppendLine("            Loaded += (_, __) => LoadTree();");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate the enhanced tree loading logic using framework-agnostic approach
        sb.Append("        " + GenerateFrameworkAgnosticTreeLogic("PropertyTreeView", "_viewModel").Replace("\n", "\n        "));
        sb.AppendLine();
        
        // Property details method
        sb.AppendLine("        private void UpdatePropertyDetails()");
        sb.AppendLine("        {");
        sb.AppendLine("            PropertyDetailsPanel.Children.Clear();");
        sb.AppendLine();
        sb.AppendLine("            if (PropertyTreeView.SelectedItem is TreeViewItem selectedItem &&");
        sb.AppendLine("                selectedItem.Tag is PropertyNodeInfo nodeInfo)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    PropertyDetailsPanel.Children.Add(new TextBlock");
        sb.AppendLine("                    {");
        sb.AppendLine("                        Text = $\"Property: {nodeInfo.PropertyName}\",");
        sb.AppendLine("                        FontWeight = FontWeights.Bold,");
        sb.AppendLine("                        Margin = new Thickness(0, 0, 0, 5)");
        sb.AppendLine("                    });");
        sb.AppendLine();
        sb.AppendLine("                    var typeInfo = \"Unknown\";");
        sb.AppendLine("                    if (nodeInfo.IsSimpleProperty) typeInfo = \"Simple Property\";");
        sb.AppendLine("                    else if (nodeInfo.IsBooleanProperty) typeInfo = \"Boolean Property\";");
        sb.AppendLine("                    else if (nodeInfo.IsEnumProperty) typeInfo = \"Enum Property\";");
        sb.AppendLine("                    else if (nodeInfo.IsCollectionProperty) typeInfo = \"Collection Property\";");
        sb.AppendLine("                    else if (nodeInfo.IsComplexProperty) typeInfo = \"Complex Property\";");
        sb.AppendLine();
        sb.AppendLine("                    PropertyDetailsPanel.Children.Add(new TextBlock");
        sb.AppendLine("                    {");
        sb.AppendLine("                        Text = $\"Type: {typeInfo}\",");
        sb.AppendLine("                        Foreground = System.Windows.Media.Brushes.Blue,");
        sb.AppendLine("                        Margin = new Thickness(0, 0, 0, 5)");
        sb.AppendLine("                    });");
        sb.AppendLine();
        sb.AppendLine("                    if (nodeInfo.Object != null)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        PropertyDetailsPanel.Children.Add(new TextBlock");
        sb.AppendLine("                        {");
        sb.AppendLine("                            Text = $\"Value: {nodeInfo.Object}\",");
        sb.AppendLine("                            Margin = new Thickness(0, 0, 0, 5)");
        sb.AppendLine("                        });");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                catch");
        sb.AppendLine("                {");
        sb.AppendLine("                    PropertyDetailsPanel.Children.Add(new TextBlock");
        sb.AppendLine("                    {");
        sb.AppendLine("                        Text = \"Error displaying property details\",");
        sb.AppendLine("                        Foreground = System.Windows.Media.Brushes.Red");
        sb.AppendLine("                    });");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                PropertyDetailsPanel.Children.Add(new TextBlock");
        sb.AppendLine("                {");
        sb.AppendLine("                    Text = \"Select a property in the tree to view details\",");
        sb.AppendLine("                    FontStyle = FontStyles.Italic,");
        sb.AppendLine("                    Foreground = System.Windows.Media.Brushes.Gray");
        sb.AppendLine("                });");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Helper methods
        sb.AppendLine("        private void ExpandAll(ItemsControl control)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var item in control.Items.OfType<TreeViewItem>())");
        sb.AppendLine("            {");
        sb.AppendLine("                item.IsExpanded = true;");
        sb.AppendLine("                ExpandAll(item);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private void CollapseAll(ItemsControl control)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var item in control.Items.OfType<TreeViewItem>())");
        sb.AppendLine("            {");
        sb.AppendLine("                item.IsExpanded = false;");
        sb.AppendLine("                CollapseAll(item);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        protected override void OnClosed(EventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            _updateTimer?.Dispose();");
        sb.AppendLine("            base.OnClosed(e);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Convert framework-agnostic tree commands to WPF-specific C# code
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
                    sb.AppendLine($"{indent}// WPF doesn't need BeginUpdate");
                    break;
                    
                case TreeCommandType.EndUpdate:
                    sb.AppendLine($"{indent}// WPF doesn't need EndUpdate");
                    break;
                    
                case TreeCommandType.Clear:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Items.Clear();");
                    break;
                    
                case TreeCommandType.CreateNode:
                    sb.AppendLine($"{indent}var {command.Parameters[0]} = new TreeViewItem {{ Header = {command.Parameters[1]} }};");
                    break;
                    
                case TreeCommandType.AddToTree:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Items.Add({command.Parameters[1]});");
                    break;
                    
                case TreeCommandType.AddChildNode:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.Items.Add({command.Parameters[1]});");
                    break;
                    
                case TreeCommandType.ExpandNode:
                    sb.AppendLine($"{indent}{command.Parameters[0]}.IsExpanded = true;");
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

    // For WPF, these are handled by XAML and code-behind, so we return empty implementations
    protected override UIComponent GenerateTreeViewStructure() => new ContainerComponent("Placeholder");
    protected override UIComponent GeneratePropertyDetailsPanel() => new ContainerComponent("Placeholder");
    protected override UIComponent GenerateCommandButtons() => new ContainerComponent("Placeholder");
    protected override UIComponent GeneratePropertyChangeMonitoring() => new CodeBlockComponent(string.Empty);

    // WPF-specific tree operations (for potential future use in code-behind)
    protected override string GenerateTreeBeginUpdate(string treeVariableName) => $"{treeVariableName}.Items.Clear(); // WPF doesn't have BeginUpdate";
    protected override string GenerateTreeEndUpdate(string treeVariableName) => $"// WPF doesn't have EndUpdate";
    protected override string GenerateTreeClear(string treeVariableName) => $"{treeVariableName}.Items.Clear();";
    protected override string GenerateCreateTreeNode(string text) => $"new TreeViewItem {{ Header = {text} }}";
    protected override string GenerateAddTreeNode(string treeVariableName, string nodeVariableName) => $"{treeVariableName}.Items.Add({nodeVariableName});";
    protected override string GenerateAddChildTreeNode(string parentNodeVariableName, string childNodeVariableName) => $"{parentNodeVariableName}.Items.Add({childNodeVariableName});";
    protected override string GenerateExpandTreeNode(string nodeVariableName) => $"{nodeVariableName}.IsExpanded = true;";
    protected override string GenerateSetTreeNodeTag(string nodeVariableName, string propertyName, string objectReference, string additionalProperties) =>
        $"{nodeVariableName}.Tag = new {{ PropertyName = {propertyName}, Object = {objectReference}, {additionalProperties} }};";
}