using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static partial class CsProjectGenerator
{
    // ---------------- WPF Generators ----------------
    public static string GenerateWpfAppXaml() => """
<Application x:Class="GuiClientApp.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" ShutdownMode="OnMainWindowClose"></Application>
""";

    public static string GenerateWpfAppCodeBehind(string serviceName, string clientClassName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine("using System.Windows;");
        sb.AppendLine("using Generated.Clients;");
        sb.AppendLine("using Test.Protos;");
        sb.AppendLine();
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
        sb.AppendLine("    public partial class App : Application");
        sb.AppendLine("    {");
        sb.AppendLine("        protected override async void OnStartup(StartupEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.OnStartup(e);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                int port = 50052;");
        sb.AppendLine("                if (e.Args.Length > 0 && int.TryParse(e.Args[0], out var p)) port = p;");
        sb.AppendLine();
        sb.AppendLine("                var handler = new HttpClientHandler();");
        sb.AppendLine("                handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;");
        sb.AppendLine("                var channel = GrpcChannel.ForAddress(new Uri(\"https://localhost:\" + port + \"/\"), new GrpcChannelOptions { HttpHandler = handler });");
        sb.AppendLine();
        sb.AppendLine($"                var grpcClient = new {serviceName}.{serviceName}Client(channel);");
        sb.AppendLine($"                var vm = new {clientClassName}(grpcClient);");
        sb.AppendLine("                await vm.InitializeRemoteAsync();");
        sb.AppendLine();
        sb.AppendLine("                var win = new MainWindow(vm);");
        sb.AppendLine("                win.Show();");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                MessageBox.Show(\"Remote initialization failed:\\n\" + ex, \"Remote MVVM Error\", MessageBoxButton.OK, MessageBoxImage.Error);");
        sb.AppendLine("                Shutdown(-1);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateWpfMainWindowXaml(string projectName, string clientClassName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        // Use PropertyDiscoveryUtility for property analysis (legacy approach)
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        var sb = new StringBuilder();
        sb.AppendLine($"<Window x:Class=\"GuiClientApp.MainWindow\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Title=\"{projectName} GUI Client\" Height=\"750\" Width=\"1100\">");
        sb.AppendLine("  <Grid>");
        sb.AppendLine("    <Grid.ColumnDefinitions>");
        sb.AppendLine("      <ColumnDefinition Width=\"*\"/>");
        sb.AppendLine("      <ColumnDefinition Width=\"*\"/>");
        sb.AppendLine("    </Grid.ColumnDefinitions>");
        sb.AppendLine();
        
        // Left panel - TreeView with hierarchical property structure
        sb.AppendLine("    <!-- Client Properties TreeView -->");
        sb.AppendLine("    <DockPanel Grid.Column=\"0\" Margin=\"10\">");
        sb.AppendLine("      <TextBlock DockPanel.Dock=\"Top\" Text=\"Client ViewModel Properties\" FontWeight=\"Bold\" FontSize=\"14\" Margin=\"0,0,0,10\"/>");
        sb.AppendLine("      <StackPanel DockPanel.Dock=\"Bottom\" Orientation=\"Horizontal\" HorizontalAlignment=\"Right\" Margin=\"0,10,0,0\">");
        sb.AppendLine("        <Button Name=\"RefreshBtn\" Content=\"Refresh\" Width=\"70\" Height=\"25\" Margin=\"0,0,5,0\"/>");
        sb.AppendLine("        <Button Name=\"ExpandAllBtn\" Content=\"Expand All\" Width=\"80\" Height=\"25\" Margin=\"0,0,5,0\"/>");
        sb.AppendLine("        <Button Name=\"CollapseAllBtn\" Content=\"Collapse\" Width=\"70\" Height=\"25\"/>");
        sb.AppendLine("      </StackPanel>");
        sb.AppendLine("      <TreeView Name=\"PropertyTreeView\" />");
        sb.AppendLine("    </DockPanel>");
        sb.AppendLine();
        
        // Right panel - Connection status, property details and commands
        sb.AppendLine("    <!-- Client Details Panel -->");
        sb.AppendLine("    <ScrollViewer Grid.Column=\"1\" Margin=\"10\" VerticalScrollBarVisibility=\"Auto\">");
        sb.AppendLine("      <StackPanel>");
        sb.AppendLine("        <TextBlock Text=\"{Binding ConnectionStatus, Mode=OneWay}\" FontWeight=\"Bold\" Margin=\"0,0,0,8\"/>");
        sb.AppendLine();
        
        // Property details section
        sb.AppendLine("        <GroupBox Header=\"Property Details\" Margin=\"0,0,0,15\">");
        sb.AppendLine("          <StackPanel Name=\"PropertyDetailsPanel\">");
        sb.AppendLine("            <TextBlock Text=\"Select a property in the tree to view details\" FontStyle=\"Italic\" Foreground=\"Gray\"/>");
        sb.AppendLine("          </StackPanel>");
        sb.AppendLine("        </GroupBox>");
        sb.AppendLine();
        
        // Generate key properties display using legacy property analysis
        var keyProperties = analysis.SimpleProperties.Concat(analysis.BooleanProperties)
            .Take(6);
            
        if (keyProperties.Any())
        {
            sb.AppendLine("        <GroupBox Header=\"Key Properties\" Margin=\"0,0,0,15\">");
            sb.AppendLine("          <StackPanel>");
            
            foreach (var prop in keyProperties)
            {
                var metadata = analysis.GetMetadata(prop);
                
                if (analysis.BooleanProperties.Contains(prop))
                {
                    if (prop.IsReadOnly)
                    {
                        sb.AppendLine($"          <TextBlock Text=\"{prop.Name}: {{Binding {metadata.SafePropertyAccess}, Mode=OneWay}}\" Margin=\"0,2,0,2\"/>");
                    }
                    else
                    {
                        sb.AppendLine($"          <CheckBox Content=\"{prop.Name}\" IsChecked=\"{{Binding {metadata.SafePropertyAccess}, Mode=TwoWay}}\" Margin=\"0,2,0,2\"/>");
                    }
                }
                else
                {
                    if (prop.IsReadOnly)
                    {
                        sb.AppendLine($"          <TextBlock Text=\"{prop.Name}\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                        sb.AppendLine($"          <TextBlock Text=\"{{Binding {metadata.SafePropertyAccess}, Mode=OneWay}}\" Margin=\"0,0,0,4\" TextWrapping=\"Wrap\"/>");
                    }
                    else
                    {
                        sb.AppendLine($"          <TextBlock Text=\"{prop.Name}\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                        sb.AppendLine($"          <TextBox Text=\"{{Binding {metadata.SafePropertyAccess}, Mode=TwoWay}}\" Margin=\"0,0,0,4\"/>");
                    }
                }
            }
            
            sb.AppendLine("          </StackPanel>");
            sb.AppendLine("        </GroupBox>");
        }
        
        // Generate collections display using legacy property analysis
        var collectionProperties = analysis.CollectionProperties.Take(3);
            
        foreach (var prop in collectionProperties)
        {
            var metadata = analysis.GetMetadata(prop);
            
            sb.AppendLine($"        <GroupBox Header=\"{prop.Name}\" Margin=\"0,0,0,15\">");
            sb.AppendLine($"          <ListBox ItemsSource=\"{{Binding {metadata.SafePropertyAccess}}}\" MaxHeight=\"200\">");
            sb.AppendLine("            <ListBox.ItemTemplate>");
            sb.AppendLine("              <DataTemplate>");
            sb.AppendLine("                <StackPanel Orientation=\"Horizontal\" Margin=\"2\">");
            
            // Generate generic property displays for collection items
            sb.AppendLine("                  <TextBlock Text=\"Item: \" FontWeight=\"SemiBold\"/>");
            sb.AppendLine("                  <TextBlock Text=\"{Binding}\" Margin=\"0,0,8,0\"/>");
            
            sb.AppendLine("                </StackPanel>");
            sb.AppendLine("              </DataTemplate>");
            sb.AppendLine("            </ListBox.ItemTemplate>");
            sb.AppendLine("          </ListBox>");
            sb.AppendLine("        </GroupBox>");
        }
        
        // Commands section if commands exist
        if (cmds.Any())
        {
            sb.AppendLine("        <GroupBox Header=\"Commands\" Margin=\"0,0,0,15\">");
            sb.AppendLine("          <WrapPanel>");
            foreach (var c in cmds)
            {
                var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                sb.AppendLine($"            <Button Content=\"{baseName}\" Command=\"{{Binding {c.CommandPropertyName}}}\" Width=\"120\" Height=\"30\" Margin=\"0,0,10,10\"/>");
            }
            sb.AppendLine("          </WrapPanel>");
            sb.AppendLine("        </GroupBox>");
        }
        
        sb.AppendLine("      </StackPanel>");
        sb.AppendLine("    </ScrollViewer>");
        sb.AppendLine("  </Grid>");
        sb.AppendLine("</Window>");
        return sb.ToString();
    }

    public static string GenerateWpfMainWindowCodeBehind() 
    {
        return """
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Globalization;
using Generated.Clients;

namespace GuiClientApp
{
    public partial class MainWindow : Window
    {
        private readonly object _viewModel;
        private readonly HashSet<object> _visitedObjects = new();
        private readonly HashSet<string> _expandedPaths = new();

        public MainWindow(object vm) 
        { 
            InitializeComponent(); 
            DataContext = vm;
            _viewModel = vm;
            
            // Wire up button events
            RefreshBtn.Click += (_, __) => RefreshProperties();
            ExpandAllBtn.Click += (_, __) => ExpandAll();
            CollapseAllBtn.Click += (_, __) => CollapseAll();
            
            // Set up property change monitoring - rely on server notifications instead of periodic polling
            if (_viewModel is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += (_, e) => 
                {
                    Dispatcher.BeginInvoke(() => RefreshProperties());
                };
            }
            
            // Wire up tree selection changed
            PropertyTreeView.SelectedItemChanged += (_, e) => UpdatePropertyDetails();
            
            // Initial load
            Loaded += (_, __) => RefreshProperties();
        }

        private void RefreshProperties()
        {
            try
            {
                LoadPropertyTree();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing properties: {ex.Message}");
            }
        }

        private string GetTreePath(TreeViewItem item)
        {
            var path = new List<string>();
            var current = item;
            
            while (current != null)
            {
                if (current.Header?.ToString() is string header)
                {
                    // Extract property name from header (format: "PropertyName: value")
                    var propertyName = header.Split(':')[0].Trim();
                    path.Insert(0, propertyName);
                }
                current = ItemsControl.ItemsControlFromItemContainer(current) as TreeViewItem;
            }
            
            return path.Count > 0 ? string.Join("/", path) : string.Empty;
        }

        private void AttachTreeViewItemEvents(TreeViewItem item)
        {
            item.Expanded += (sender, e) =>
            {
                if (sender is TreeViewItem expandedItem)
                {
                    var path = GetTreePath(expandedItem);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Add(path);
                }
                e.Handled = true; // Prevent event bubbling
            };
            
            item.Collapsed += (sender, e) =>
            {
                if (sender is TreeViewItem collapsedItem)
                {
                    var path = GetTreePath(collapsedItem);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Remove(path);
                }
                e.Handled = true; // Prevent event bubbling
            };

            // Recursively attach events to child items
            foreach (TreeViewItem childItem in item.Items.OfType<TreeViewItem>())
            {
                AttachTreeViewItemEvents(childItem);
            }
        }

        private void RestoreExpandedState(ItemsControl container)
        {
            foreach (TreeViewItem item in container.Items.OfType<TreeViewItem>())
            {
                var itemPath = GetTreePath(item);
                if (!string.IsNullOrEmpty(itemPath) && _expandedPaths.Contains(itemPath))
                {
                    item.IsExpanded = true;
                }
                
                // Recursively restore child items
                if (item.Items.Count > 0)
                {
                    RestoreExpandedState(item);
                }
            }
        }

        private void StoreCurrentExpandedState(ItemsControl container)
        {
            foreach (TreeViewItem item in container.Items.OfType<TreeViewItem>())
            {
                if (item.IsExpanded)
                {
                    var path = GetTreePath(item);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Add(path);
                }
                
                // Recursively store child items
                if (item.Items.Count > 0)
                {
                    StoreCurrentExpandedState(item);
                }
            }
        }

        private void LoadPropertyTree()
        {
            // Store current expanded state before clearing
            StoreCurrentExpandedState(PropertyTreeView);
            
            PropertyTreeView.Items.Clear();
            _visitedObjects.Clear(); // Reset cycle detection
            
            var rootItem = new TreeViewItem { Header = "Client ViewModel Properties", IsExpanded = true };
            PropertyTreeView.Items.Add(rootItem);
            
            try
            {
                // Use reflection to discover properties dynamically with hierarchical structure
                var properties = _viewModel.GetType().GetProperties()
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .ToList();
                
                foreach (var prop in properties)
                {
                    try
                    {
                        var propItem = CreatePropertyTreeItem(prop, _viewModel, 0);
                        if (propItem != null)
                        {
                            rootItem.Items.Add(propItem);
                            AttachTreeViewItemEvents(propItem);
                        }
                    }
                    catch
                    {
                        var errorItem = new TreeViewItem { Header = $"{prop.Name}: <error>" };
                        rootItem.Items.Add(errorItem);
                        AttachTreeViewItemEvents(errorItem);
                    }
                }
                
                // Attach events to root item
                AttachTreeViewItemEvents(rootItem);
                
                // Restore expanded state after rebuilding tree - use lower priority to ensure tree is built first
                Dispatcher.BeginInvoke(() => RestoreExpandedState(PropertyTreeView), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                var errorItem = new TreeViewItem { Header = $"Error loading properties: {ex.Message}" };
                rootItem.Items.Add(errorItem);
                AttachTreeViewItemEvents(errorItem);
            }
        }

        private TreeViewItem? CreatePropertyTreeItem(System.Reflection.PropertyInfo prop, object obj, int depth)
        {
            try
            {
                // Prevent infinite recursion with depth limit and cycle detection
                if (depth > 5) return null;
                
                var value = prop.GetValue(obj);
                var displayValue = value?.ToString() ?? "<null>";
                
                // Cycle detection - prevent infinite recursion
                if (value != null && !IsSimpleType(prop.PropertyType))
                {
                    if (_visitedObjects.Contains(value))
                    {
                        return new TreeViewItem 
                        { 
                            Header = $"{prop.Name}: [Circular Reference]",
                            Tag = new { Property = prop, Value = value, Object = obj }
                        };
                    }
                }
                
                // For collections, show count information
                if (IsCollectionType(prop.PropertyType) && value != null)
                {
                    var countProp = value.GetType().GetProperty("Count") ?? value.GetType().GetProperty("Length");
                    if (countProp != null)
                    {
                        var count = countProp.GetValue(value);
                        displayValue = $"[{count} items]";
                    }
                }
                
                var propItem = new TreeViewItem
                {
                    Header = $"{prop.Name}: {displayValue}",
                    Tag = new { Property = prop, Value = value, Object = obj }
                };
                
                // For complex objects, try to expand their properties
                if (value != null && !IsSimpleType(prop.PropertyType))
                {
                    try
                    {
                        // Add to visited objects to prevent cycles
                        _visitedObjects.Add(value);
                        
                        if (IsCollectionType(prop.PropertyType))
                        {
                            // For collections, show first few items
                            if (value is System.Collections.IEnumerable enumerable)
                            {
                                int itemIndex = 0;
                                foreach (var item in enumerable)
                                {
                                    if (itemIndex >= 3) break; // Limit to first 3 items
                                    if (item == null) continue;
                        
                                    var itemProperties = item.GetType().GetProperties()
                                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                        .Take(5)
                                        .ToList();
                        
                                    var itemNode = new TreeViewItem 
                                    { 
                                        Header = $"[{itemIndex}] {item.GetType().Name}",
                                        Tag = new { Property = (System.Reflection.PropertyInfo?)null, Value = item, Object = item }
                                    };
                        
                                    foreach (var itemProp in itemProperties)
                                    {
                                        var childItem = CreatePropertyTreeItem(itemProp, item, depth + 1);
                                        if (childItem != null)
                                        {
                                            itemNode.Items.Add(childItem);
                                            AttachTreeViewItemEvents(childItem);
                                        }
                                    }
                        
                                    propItem.Items.Add(itemNode);
                                    AttachTreeViewItemEvents(itemNode);
                                    itemIndex++;
                                }
                            }
                        }
                        else
                        {
                            // For other complex objects, show their properties
                            var childProperties = value.GetType().GetProperties()
                                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                .Take(10) // Limit depth to prevent UI overload
                                .ToList();
                        
                            foreach (var childProp in childProperties)
                            {
                                var childItem = CreatePropertyTreeItem(childProp, value, depth + 1);
                                if (childItem != null)
                                {
                                    propItem.Items.Add(childItem);
                                    AttachTreeViewItemEvents(childItem);
                                }
                            }
                        }
                        
                        // Remove from visited objects when done
                        _visitedObjects.Remove(value);
                    }
                    catch
                    {
                        // Ignore child property errors and remove from visited set
                        if (value != null)
                            _visitedObjects.Remove(value);
                    }
                }
                
                return propItem;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(DateTime) || 
                   type == typeof(decimal) || 
                   type == typeof(Guid) ||
                   type.IsEnum ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        private static bool IsCollectionType(Type type)
        {
            return type != typeof(string) &&
                   typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsEditableType(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType == typeof(int) ||
                   underlyingType == typeof(double) ||
                   underlyingType == typeof(decimal) ||
                   underlyingType == typeof(float) ||
                   underlyingType == typeof(bool) ||
                   underlyingType == typeof(string) ||
                   underlyingType.IsEnum ||
                   underlyingType == typeof(DateTime);
        }

        private static bool IsEffectivelyReadOnly(PropertyInfo property)
        {
            var setter = property.GetSetMethod(true);
            return setter == null || setter.IsPrivate;
        }

        private FrameworkElement CreateEditableField(PropertyInfo property, object owner)
        {
            var propType = property.PropertyType;
            var currentValue = property.GetValue(owner);
            if (propType == typeof(bool) || propType == typeof(bool?))
            {
                var cb = new CheckBox { IsChecked = currentValue as bool? };
                cb.Checked += (_, __) => { property.SetValue(owner, true); LoadPropertyTree(); UpdatePropertyDetails(); };
                cb.Unchecked += (_, __) => { property.SetValue(owner, false); LoadPropertyTree(); UpdatePropertyDetails(); };
                return cb;
            }
            if (propType.IsEnum)
            {
                var combo = new ComboBox { ItemsSource = Enum.GetValues(propType), SelectedItem = currentValue };
                combo.SelectionChanged += (_, __) =>
                {
                    if (combo.SelectedItem != null)
                    {
                        property.SetValue(owner, combo.SelectedItem);
                        LoadPropertyTree();
                        UpdatePropertyDetails();
                    }
                };
                return combo;
            }
            var tb = new TextBox { Text = currentValue?.ToString() ?? string.Empty, Width = 200 };
            tb.LostFocus += (_, __) =>
            {
                try
                {
                    object? val;
                    var text = tb.Text;
                    if (propType == typeof(string))
                        val = text;
                    else if (propType == typeof(int) || propType == typeof(int?))
                        val = string.IsNullOrWhiteSpace(text) ? null : int.Parse(text);
                    else if (propType == typeof(double) || propType == typeof(double?))
                        val = string.IsNullOrWhiteSpace(text) ? null : double.Parse(text);
                    else if (propType == typeof(decimal) || propType == typeof(decimal?))
                        val = string.IsNullOrWhiteSpace(text) ? null : decimal.Parse(text);
                    else if (propType == typeof(float) || propType == typeof(float?))
                        val = string.IsNullOrWhiteSpace(text) ? null : float.Parse(text);
                    else if (propType == typeof(DateTime) || propType == typeof(DateTime?))
                    {
                        if (string.IsNullOrWhiteSpace(text)) val = null;
                        else
                        {
                            var dt = DateTime.Parse(text, null, DateTimeStyles.RoundtripKind);
                            if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                            val = dt;
                        }
                    }
                    else
                        val = text;
                    property.SetValue(owner, val);
                    LoadPropertyTree();
                    UpdatePropertyDetails();
                }
                catch { }
            };
            return tb;
        }

        private void UpdatePropertyDetails()
        {
            PropertyDetailsPanel.Children.Clear();

            if (PropertyTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is object tagData)
            {
                try
                {
                    var propertyInfo = tagData.GetType().GetProperty("Property")?.GetValue(tagData) as PropertyInfo;
                    var owner = tagData.GetType().GetProperty("Object")?.GetValue(tagData) ?? tagData;
                    if (propertyInfo != null && owner != null)
                    {
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Property: {propertyInfo.Name}",
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Type: {propertyInfo.PropertyType.Name}",
                            Foreground = System.Windows.Media.Brushes.Blue,
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        var value = propertyInfo.GetValue(owner);
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Value: {value?.ToString() ?? "<null>"}",
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        bool readOnly = IsEffectivelyReadOnly(propertyInfo);
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Read-Only: {readOnly}",
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        if (!readOnly && IsEditableType(propertyInfo.PropertyType))
                        {
                            PropertyDetailsPanel.Children.Add(CreateEditableField(propertyInfo, owner));
                        }
                    }
                }
                catch
                {
                    PropertyDetailsPanel.Children.Add(new TextBlock
                    {
                        Text = "Error displaying property details",
                        Foreground = System.Windows.Media.Brushes.Red
                    });
                }
            }
            else
            {
                PropertyDetailsPanel.Children.Add(new TextBlock
                {
                    Text = "Select a property in the tree to view details",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
            }
        }

        private void ExpandAll()
        {
            ExpandCollapseAll(PropertyTreeView, true);
        }

        private void CollapseAll()
        {
            ExpandCollapseAll(PropertyTreeView, false);
        }

        private void ExpandCollapseAll(ItemsControl control, bool expand)
        {
            foreach (var item in control.Items.OfType<TreeViewItem>())
            {
                item.IsExpanded = expand;
                ExpandCollapseAll(item, expand);
            }
        }
    }
}
""";
    }

    // ---------------- Server WPF Generators ----------------
    public static string GenerateServerWpfAppXaml() => """
<Application x:Class="ServerApp.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" ShutdownMode="OnMainWindowClose"></Application>
""";

    public static string GenerateServerWpfAppCodeBehind(string viewModelName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Windows;");
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine("using Generated.ViewModels;");
        sb.AppendLine();
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("    public partial class App : Application");
        sb.AppendLine("    {");
        sb.AppendLine("        protected override void OnStartup(StartupEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.OnStartup(e);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                int port = 50052;");
        sb.AppendLine("                if (e.Args.Length > 0 && int.TryParse(e.Args[0], out var p)) port = p;");
        sb.AppendLine();
        sb.AppendLine("                var serverOptions = new ServerOptions { Port = port, UseHttps = true };");
        sb.AppendLine($"                var vm = new {viewModelName}(serverOptions);");
        sb.AppendLine();
        sb.AppendLine("                var win = new MainWindow(vm);");
        sb.AppendLine("                win.Show();");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                MessageBox.Show(\"Server initialization failed:\\n\" + ex, \"Server Error\", MessageBoxButton.OK, MessageBoxImage.Error);");
        sb.AppendLine("                Shutdown(-1);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateServerWpfMainWindowXaml(string projectName, string viewModelName, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        // Use PropertyDiscoveryUtility for property analysis (legacy approach)
        var analysis = PropertyDiscoveryUtility.AnalyzeProperties(props);
        
        var sb = new StringBuilder();
        sb.AppendLine($"<Window x:Class=\"ServerApp.MainWindow\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Title=\"Server GUI - {projectName}\" Height=\"750\" Width=\"1100\">");
        sb.AppendLine("  <Grid>");
        sb.AppendLine("    <Grid.ColumnDefinitions>");
        sb.AppendLine("      <ColumnDefinition Width=\"*\"/>");
        sb.AppendLine("      <ColumnDefinition Width=\"*\"/>");
        sb.AppendLine("    </Grid.ColumnDefinitions>");
        sb.AppendLine();
        
        // Left panel - TreeView with hierarchical property structure
        sb.AppendLine("    <!-- Server Properties TreeView -->");
        sb.AppendLine("    <DockPanel Grid.Column=\"0\" Margin=\"10\">");
        sb.AppendLine("      <TextBlock DockPanel.Dock=\"Top\" Text=\"Server ViewModel Properties\" FontWeight=\"Bold\" FontSize=\"14\" Margin=\"0,0,0,10\"/>");
        sb.AppendLine("      <StackPanel DockPanel.Dock=\"Bottom\" Orientation=\"Horizontal\" HorizontalAlignment=\"Right\" Margin=\"0,10,0,0\">");
        sb.AppendLine("        <Button Name=\"RefreshBtn\" Content=\"Refresh\" Width=\"70\" Height=\"25\" Margin=\"0,0,5,0\"/>");
        sb.AppendLine("        <Button Name=\"ExpandAllBtn\" Content=\"Expand All\" Width=\"80\" Height=\"25\" Margin=\"0,0,5,0\"/>");
        sb.AppendLine("        <Button Name=\"CollapseAllBtn\" Content=\"Collapse\" Width=\"70\" Height=\"25\"/>");
        sb.AppendLine("      </StackPanel>");
        sb.AppendLine("      <TreeView Name=\"PropertyTreeView\" />");
        sb.AppendLine("    </DockPanel>");
        sb.AppendLine();
        
        // Right panel - Server status and commands
        sb.AppendLine("    <!-- Server Details Panel -->");
        sb.AppendLine("    <ScrollViewer Grid.Column=\"1\" Margin=\"10\" VerticalScrollBarVisibility=\"Auto\">");
        sb.AppendLine("      <StackPanel>");
        sb.AppendLine("        <TextBlock Text=\"Server Status: Running\" FontWeight=\"Bold\" Foreground=\"Green\" Margin=\"0,0,0,8\"/>");
        sb.AppendLine();

        sb.AppendLine("        <GroupBox Header=\"Property Details\" Margin=\"0,0,0,15\">");
        sb.AppendLine("          <StackPanel Name=\"PropertyDetailsPanel\">");
        sb.AppendLine("            <TextBlock Text=\"Select a property in the tree to view details\" FontStyle=\"Italic\" Foreground=\"Gray\"/>");
        sb.AppendLine("          </StackPanel>");
        sb.AppendLine("        </GroupBox>");
        sb.AppendLine();

        // Server status section
        sb.AppendLine("        <GroupBox Header=\"Server Information\" Margin=\"0,0,0,15\">");
        sb.AppendLine("          <StackPanel>");
        sb.AppendLine("            <TextBlock Name=\"ServerStatusText\" Text=\"Running\" Foreground=\"Green\" FontWeight=\"Bold\"/>");
        sb.AppendLine("            <TextBlock Name=\"ServerPortText\" Text=\"Port: Loading...\" Margin=\"0,5,0,0\"/>");
        sb.AppendLine("          </StackPanel>");
        sb.AppendLine("        </GroupBox>");
        sb.AppendLine();
        
        // Commands section if commands exist
        if (cmds.Any())
        {
            sb.AppendLine("        <GroupBox Header=\"Server Commands\" Margin=\"0,0,0,15\">");
            sb.AppendLine("          <WrapPanel>");
            foreach (var c in cmds)
            {
                var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                sb.AppendLine($"            <Button Content=\"{baseName}\" Command=\"{{Binding {c.CommandPropertyName}}}\" Width=\"120\" Height=\"30\" Margin=\"0,0,10,10\"/>");
            }
            sb.AppendLine("          </WrapPanel>");
            sb.AppendLine("        </GroupBox>");
        }
        
        sb.AppendLine("      </StackPanel>");
        sb.AppendLine("    </ScrollViewer>");
        sb.AppendLine("  </Grid>");
        sb.AppendLine("</Window>");
        return sb.ToString();
    }

    public static string GenerateServerWpfMainWindowCodeBehind() 
    {
        return """
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Globalization;

namespace ServerApp
{ 
    public partial class MainWindow : Window 
    { 
        private readonly object _viewModel;
        private readonly HashSet<object> _visitedObjects = new();
        private readonly HashSet<string> _expandedPaths = new();

        public MainWindow(object vm) 
        { 
            InitializeComponent(); 
            DataContext = vm;
            _viewModel = vm;
            
            // Wire up button events
            RefreshBtn.Click += (_, __) => RefreshProperties();
            ExpandAllBtn.Click += (_, __) => ExpandAll();
            CollapseAllBtn.Click += (_, __) => CollapseAll();
            
            // Set up property change monitoring - rely on server notifications instead of periodic polling
            if (_viewModel is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += (_, e) => 
                {
                    Dispatcher.BeginInvoke(() => 
                    {
                        RefreshProperties();
                        UpdateServerStatus();
                    });
                };
            }
            
            // Wire up tree selection changed
            PropertyTreeView.SelectedItemChanged += (_, e) => UpdatePropertyDetails();
            
            // Initial load
            Loaded += (_, __) => 
            {
                RefreshProperties();
                UpdateServerStatus();
            };
        }

        private void RefreshProperties()
        {
            try
            {
                LoadPropertyTree();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing server properties: {ex.Message}");
            }
        }

        private string GetTreePath(TreeViewItem item)
        {
            var path = new List<string>();
            var current = item;
            
            while (current != null)
            {
                if (current.Header?.ToString() is string header)
                {
                    // Extract property name from header (format: "PropertyName: value")
                    var propertyName = header.Split(':')[0].Trim();
                    path.Insert(0, propertyName);
                }
                current = ItemsControl.ItemsControlFromItemContainer(current) as TreeViewItem;
            }
            
            return path.Count > 0 ? string.Join("/", path) : string.Empty;
        }

        private void AttachTreeViewItemEvents(TreeViewItem item)
        {
            item.Expanded += (sender, e) =>
            {
                if (sender is TreeViewItem expandedItem)
                {
                    var path = GetTreePath(expandedItem);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Add(path);
                }
                e.Handled = true; // Prevent event bubbling
            };
            
            item.Collapsed += (sender, e) =>
            {
                if (sender is TreeViewItem collapsedItem)
                {
                    var path = GetTreePath(collapsedItem);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Remove(path);
                }
                e.Handled = true; // Prevent event bubbling
            };

            // Recursively attach events to child items
            foreach (TreeViewItem childItem in item.Items.OfType<TreeViewItem>())
            {
                AttachTreeViewItemEvents(childItem);
            }
        }

        private void RestoreExpandedState(ItemsControl container)
        {
            foreach (TreeViewItem item in container.Items.OfType<TreeViewItem>())
            {
                var itemPath = GetTreePath(item);
                if (!string.IsNullOrEmpty(itemPath) && _expandedPaths.Contains(itemPath))
                {
                    item.IsExpanded = true;
                }
                
                // Recursively restore child items
                if (item.Items.Count > 0)
                {
                    RestoreExpandedState(item);
                }
            }
        }

        private void StoreCurrentExpandedState(ItemsControl container)
        {
            foreach (TreeViewItem item in container.Items.OfType<TreeViewItem>())
            {
                if (item.IsExpanded)
                {
                    var path = GetTreePath(item);
                    if (!string.IsNullOrEmpty(path))
                        _expandedPaths.Add(path);
                }
                
                // Recursively store child items
                if (item.Items.Count > 0)
                {
                    StoreCurrentExpandedState(item);
                }
            }
        }

        private void LoadPropertyTree()
        {
            // Store current expanded state before clearing
            StoreCurrentExpandedState(PropertyTreeView);
            
            PropertyTreeView.Items.Clear();
            _visitedObjects.Clear(); // Reset cycle detection
            
            var rootItem = new TreeViewItem { Header = "Server ViewModel Properties", IsExpanded = true };
            PropertyTreeView.Items.Add(rootItem);
            
            try
            {
                // Use reflection to discover properties dynamically
                var properties = _viewModel.GetType().GetProperties()
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .ToList();
                
                foreach (var prop in properties)
                {
                    try
                    {
                        var propItem = CreatePropertyTreeItem(prop, _viewModel, 0);
                        if (propItem != null)
                            rootItem.Items.Add(propItem);
                    }
                    catch
                    {
                        var errorItem = new TreeViewItem { Header = $"{prop.Name}: <error>" };
                        rootItem.Items.Add(errorItem);
                    }
                }
                
                // Attach events to root item
                AttachTreeViewItemEvents(rootItem);
                
                // Restore expanded state after rebuilding tree - use lower priority to ensure tree is built first
                Dispatcher.BeginInvoke(() => RestoreExpandedState(PropertyTreeView), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                var errorItem = new TreeViewItem { Header = $"Error loading server properties: {ex.Message}" };
                rootItem.Items.Add(errorItem);
                AttachTreeViewItemEvents(errorItem);
            }
        }

        private TreeViewItem? CreatePropertyTreeItem(System.Reflection.PropertyInfo prop, object obj, int depth)
        {
            try
            {
                // Prevent infinite recursion with depth limit and cycle detection
                if (depth > 5) return null;
                
                var value = prop.GetValue(obj);
                var displayValue = value?.ToString() ?? "<null>";
                
                // Cycle detection - prevent infinite recursion
                if (value != null && !IsSimpleType(prop.PropertyType))
                {
                    if (_visitedObjects.Contains(value))
                    {
                        return new TreeViewItem 
                        { 
                            Header = $"{prop.Name}: [Circular Reference]",
                            Tag = new { Property = prop, Value = value, Object = obj }
                        };
                    }
                }
                
                // For collections, show count information
                if (IsCollectionType(prop.PropertyType) && value != null)
                {
                    var countProp = value.GetType().GetProperty("Count") ?? value.GetType().GetProperty("Length");
                    if (countProp != null)
                    {
                        var count = countProp.GetValue(value);
                        displayValue = $"[{count} items]";
                    }
                }
                
                var propItem = new TreeViewItem
                {
                    Header = $"{prop.Name}: {displayValue}",
                    Tag = new { Property = prop, Value = value, Object = obj }
                };
                
                // For complex objects, try to expand their properties
                if (value != null && !IsSimpleType(prop.PropertyType))
                {
                    try
                    {
                        // Add to visited objects to prevent cycles
                        _visitedObjects.Add(value);
                        
                        if (IsCollectionType(prop.PropertyType))
                        {
                            // For collections, show first few items
                            if (value is System.Collections.IEnumerable enumerable)
                            {
                                int itemIndex = 0;
                                foreach (var item in enumerable)
                                {
                                    if (itemIndex >= 3) break; // Limit to first 3 items
                                    
                                    var itemProperties = item?.GetType().GetProperties()
                                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                        .Take(5)
                                        .ToList();
                        
                                    var itemNode = new TreeViewItem 
                                    { 
                                        Header = $"[{itemIndex}] {item?.GetType().Name ?? "null"}",
                                        Tag = new { Property = (System.Reflection.PropertyInfo?)null, Value = item, Object = item }
                                    };
                        
                                    if (itemProperties != null)
                                    {
                                        foreach (var itemProp in itemProperties)
                                        {
                                            var childItem = CreatePropertyTreeItem(itemProp, item!, depth + 1);
                                            if (childItem != null)
                                            {
                                                itemNode.Items.Add(childItem);
                                                AttachTreeViewItemEvents(childItem);
                                            }
                                        }
                                    }
                        
                                    propItem.Items.Add(itemNode);
                                    AttachTreeViewItemEvents(itemNode);
                                    itemIndex++;
                                }
                            }
                        }
                        else
                        {
                            // For other complex objects, show their properties
                            var childProperties = value.GetType().GetProperties()
                                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                .Take(10) // Limit depth to prevent UI overload
                                .ToList();
                        
                            foreach (var childProp in childProperties)
                            {
                                var childItem = CreatePropertyTreeItem(childProp, value, depth + 1);
                                if (childItem != null)
                                {
                                    propItem.Items.Add(childItem);
                                    AttachTreeViewItemEvents(childItem);
                                }
                            }
                        }
                        
                        // Remove from visited objects when done
                        _visitedObjects.Remove(value);
                    }
                    catch
                    {
                        // Ignore child property errors and remove from visited set
                        if (value != null)
                            _visitedObjects.Remove(value);
                    }
                }
                
                return propItem;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(DateTime) || 
                   type == typeof(decimal) || 
                   type == typeof(Guid) ||
                   type.IsEnum ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        private static bool IsCollectionType(Type type)
        {
            return type != typeof(string) &&
                   typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsEditableType(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType == typeof(int) ||
                   underlyingType == typeof(double) ||
                   underlyingType == typeof(decimal) ||
                   underlyingType == typeof(float) ||
                   underlyingType == typeof(bool) ||
                   underlyingType == typeof(string) ||
                   underlyingType.IsEnum ||
                   underlyingType == typeof(DateTime);
        }

        private static bool IsEffectivelyReadOnly(PropertyInfo property)
        {
            var setter = property.GetSetMethod(true);
            return setter == null || setter.IsPrivate;
        }

        private FrameworkElement CreateEditableField(PropertyInfo property, object owner)
        {
            var propType = property.PropertyType;
            var currentValue = property.GetValue(owner);
            if (propType == typeof(bool) || propType == typeof(bool?))
            {
                var cb = new CheckBox { IsChecked = currentValue as bool? };
                cb.Checked += (_, __) => { property.SetValue(owner, true); LoadPropertyTree(); UpdatePropertyDetails(); };
                cb.Unchecked += (_, __) => { property.SetValue(owner, false); LoadPropertyTree(); UpdatePropertyDetails(); };
                return cb;
            }
            if (propType.IsEnum)
            {
                var combo = new ComboBox { ItemsSource = Enum.GetValues(propType), SelectedItem = currentValue };
                combo.SelectionChanged += (_, __) =>
                {
                    if (combo.SelectedItem != null)
                    {
                        property.SetValue(owner, combo.SelectedItem);
                        LoadPropertyTree();
                        UpdatePropertyDetails();
                    }
                };
                return combo;
            }
            var tb = new TextBox { Text = currentValue?.ToString() ?? string.Empty, Width = 200 };
            tb.LostFocus += (_, __) =>
            {
                try
                {
                    object? val;
                    var text = tb.Text;
                    if (propType == typeof(string))
                        val = text;
                    else if (propType == typeof(int) || propType == typeof(int?))
                        val = string.IsNullOrWhiteSpace(text) ? null : int.Parse(text);
                    else if (propType == typeof(double) || propType == typeof(double?))
                        val = string.IsNullOrWhiteSpace(text) ? null : double.Parse(text);
                    else if (propType == typeof(decimal) || propType == typeof(decimal?))
                        val = string.IsNullOrWhiteSpace(text) ? null : decimal.Parse(text);
                    else if (propType == typeof(float) || propType == typeof(float?))
                        val = string.IsNullOrWhiteSpace(text) ? null : float.Parse(text);
                    else if (propType == typeof(DateTime) || propType == typeof(DateTime?))
                    {
                        if (string.IsNullOrWhiteSpace(text)) val = null;
                        else
                        {
                            var dt = DateTime.Parse(text, null, DateTimeStyles.RoundtripKind);
                            if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                            val = dt;
                        }
                    }
                    else
                        val = text;
                    property.SetValue(owner, val);
                    LoadPropertyTree();
                    UpdatePropertyDetails();
                }
                catch { }
            };
            return tb;
        }

        private void UpdatePropertyDetails()
        {
            PropertyDetailsPanel.Children.Clear();

            if (PropertyTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is object tagData)
            {
                try
                {
                    var propertyInfo = tagData.GetType().GetProperty("Property")?.GetValue(tagData) as PropertyInfo;
                    var owner = tagData.GetType().GetProperty("Object")?.GetValue(tagData) ?? tagData;
                    if (propertyInfo != null && owner != null)
                    {
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Property: {propertyInfo.Name}",
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Type: {propertyInfo.PropertyType.Name}",
                            Foreground = System.Windows.Media.Brushes.Blue,
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        var value = propertyInfo.GetValue(owner);
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Value: {value?.ToString() ?? "<null>"}",
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        bool readOnly = IsEffectivelyReadOnly(propertyInfo);
                        PropertyDetailsPanel.Children.Add(new TextBlock
                        {
                            Text = $"Read-Only: {readOnly}",
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        if (!readOnly && IsEditableType(propertyInfo.PropertyType))
                        {
                            PropertyDetailsPanel.Children.Add(CreateEditableField(propertyInfo, owner));
                        }
                    }
                }
                catch
                {
                    PropertyDetailsPanel.Children.Add(new TextBlock
                    {
                        Text = "Error displaying property details",
                        Foreground = System.Windows.Media.Brushes.Red
                    });
                }
            }
            else
            {
                PropertyDetailsPanel.Children.Add(new TextBlock
                {
                    Text = "Select a property in the tree to view details",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
            }
        }

        private void UpdateServerStatus()
        {
            try
            {
                ServerStatusText.Text = "Running";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Green;
                
                // Try to get port information from ServerOptions if available
                var serverOptionsProperty = _viewModel.GetType().GetProperty("ServerOptions");
                if (serverOptionsProperty?.GetValue(_viewModel) is object serverOptions)
                {
                    var portProperty = serverOptions.GetType().GetProperty("Port");
                    if (portProperty?.GetValue(serverOptions) is int port)
                    {
                        ServerPortText.Text = $"Port: {port}";
                    }
                }
            }
            catch
            {
                ServerStatusText.Text = "Status Unknown";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void ExpandAll()
        {
            ExpandCollapseAll(PropertyTreeView, true);
        }

        private void CollapseAll()
        {
            ExpandCollapseAll(PropertyTreeView, false);
        }

        private void ExpandCollapseAll(ItemsControl control, bool expand)
        {
            foreach (var item in control.Items.OfType<TreeViewItem>())
            {
                item.IsExpanded = expand;
                ExpandCollapseAll(item, expand);
            }
        }
    }
}
""";
    }
}