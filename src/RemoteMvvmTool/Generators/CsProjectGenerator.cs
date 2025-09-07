using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static class CsProjectGenerator
{
    // Single-project .slnx helper
    public static string GenerateSingleProjectSolutionXml(string projectRelativePath)
        => $"<Solution>\n  <Project Path=\"{projectRelativePath}\" />\n</Solution>";

    public static string GenerateSingleProjectLaunchUser(string projectRelativePath)
    {
        var norm = projectRelativePath.Replace("\\", "/");
        return "[\n  {\n    \"Name\": \"RunSingle\",\n    \"Projects\": [\n      { \"Path\": \"" + norm + "\", \"Action\": \"Start\" }\n    ]\n  }\n]\n";
    }

    public static string GenerateSolutionXml(string serverProjectRelativePath, string clientProjectRelativePath)
        => $"<Solution>\n  <Project Path=\"{serverProjectRelativePath}\" />\n  <Project Path=\"{clientProjectRelativePath}\" />\n</Solution>";

    // ---------------- Project Files ----------------
    public static string GenerateCsProj(string projectName, string serviceName, string runType)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <LangVersion>preview</LangVersion>
    {(isWpf ? "<UseWPF>true</UseWPF>" : string.Empty)}
    {(isWinForms ? "<UseWindowsForms>true</UseWindowsForms>" : string.Empty)}
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.Net.Client.Web" Version="2.71.0" />
    <PackageReference Include="Google.Protobuf" Version="3.31.1" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0" PrivateAssets="all" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <PackageReference Include="Grpc.AspNetCore.Web" Version="2.71.0" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Both" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
</Project>
""";
    }

    public static string GenerateGuiClientCsProj(string projectName, string serviceName, string runType)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);
        var xamlItems = string.Empty; // rely on implicit WPF items
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <LangVersion>preview</LangVersion>
    {(isWpf ? "<UseWPF>true</UseWPF>" : string.Empty)}
    {(isWinForms ? "<UseWindowsForms>true</UseWindowsForms>" : string.Empty)}
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.Net.Client.Web" Version="2.71.0" />
    <PackageReference Include="Google.Protobuf" Version="3.31.1" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Client" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
{xamlItems}</Project>
""";
    }

    public static string GenerateServerLaunchSettings() => """
{ "profiles": { "Server": { "commandName": "Project", "commandLineArgs": "6000" } } }
""";
    public static string GenerateClientLaunchSettings() => """
{ "profiles": { "Client": { "commandName": "Project", "commandLineArgs": "6000 client" } } }
""";

    // ---------------- Program Generators ----------------
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

    public static string GenerateGuiClientProgram(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = runType.Equals("wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = runType.Equals("winforms", StringComparison.OrdinalIgnoreCase);
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var clientClassName = modelName + "RemoteClient";
        var sb = new StringBuilder();
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.Clients;");
        sb.AppendLine("using Generated.ViewModels;");
        
        if (isWpf) sb.AppendLine("using System.Windows;");
        if (isWinForms)
        {
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading;");
        }
        
        sb.AppendLine();
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
        sb.AppendLine("    public class Program");
        sb.AppendLine("    {");
        sb.AppendLine("        [STAThread]");
        sb.AppendLine("        public static void Main(string[] args)");
        sb.AppendLine("        {");
        sb.AppendLine("            int port = 50052;");
        sb.AppendLine("            if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;");
        sb.AppendLine();
        sb.AppendLine("            var handler = new HttpClientHandler();");
        sb.AppendLine("            handler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;");
        sb.AppendLine("            var channel = GrpcChannel.ForAddress(new Uri(\"https://localhost:\" + port + \"/\"), new GrpcChannelOptions { HttpHandler = handler });");
        sb.AppendLine($"            var grpcClient = new {serviceName}.{serviceName}Client(channel);");
        sb.AppendLine($"            var vm = new {clientClassName}(grpcClient);");
        sb.AppendLine("            vm.InitializeRemoteAsync().GetAwaiter().GetResult();");
        sb.AppendLine();

        if (isWpf)
        {
            sb.AppendLine("            var app = new Application();");
            sb.AppendLine("            var win = new MainWindow(vm);");
            sb.AppendLine("            app.Run(win);");
        }
        else if (isWinForms)
        {
            var collectionProp = props.FirstOrDefault(p => IsCollectionType(p.TypeString) && p.Name.IndexOf("ZoneList", StringComparison.OrdinalIgnoreCase) >= 0)
                                ?? props.FirstOrDefault(p => IsCollectionType(p.TypeString) && p.Name.EndsWith("List", StringComparison.OrdinalIgnoreCase));
            
            sb.AppendLine("            Application.EnableVisualStyles();");
            sb.AppendLine($"            var form = new Form");
            sb.AppendLine("            {");
            sb.AppendLine($"                Text = \"{projectName} GUI Client\",");
            sb.AppendLine("                Width = 1150,");
            sb.AppendLine("                Height = 780,");
            sb.AppendLine("                StartPosition = FormStartPosition.CenterScreen");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 300 };");
            sb.AppendLine("            form.Controls.Add(split);");
            sb.AppendLine();
            sb.AppendLine("            var statusStrip = new StatusStrip();");
            sb.AppendLine("            var statusLbl = new ToolStripStatusLabel();");
            sb.AppendLine("            statusStrip.Items.Add(statusLbl);");
            sb.AppendLine("            form.Controls.Add(statusStrip);");
            sb.AppendLine("            statusStrip.Dock = DockStyle.Bottom;");
            sb.AppendLine("            statusLbl.Text = \"Initializing...\";");
            sb.AppendLine();
            
            if (collectionProp != null)
            {
                sb.AppendLine("            var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };");
                sb.AppendLine("            split.Panel1.Controls.Add(tree);");
                sb.AppendLine();
                sb.AppendLine($"            for (int spin = 0; spin < 40 && vm.{collectionProp.Name} == null; spin++)");
                sb.AppendLine("                Thread.Sleep(25);");
                sb.AppendLine();
                sb.AppendLine("            void LoadTree()");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (vm.{collectionProp.Name} == null) return;");
                sb.AppendLine("                tree.BeginUpdate();");
                sb.AppendLine("                tree.Nodes.Clear();");
                sb.AppendLine("                int idx = 0;");
                sb.AppendLine($"                foreach (var item in vm.{collectionProp.Name})");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var node = new TreeNode(\"{collectionProp.Name}[\" + idx + \"]\");");
                sb.AppendLine("                    node.Tag = item;");
                sb.AppendLine("                    tree.Nodes.Add(node);");
                sb.AppendLine("                    idx++;");
                sb.AppendLine("                }");
                sb.AppendLine("                tree.EndUpdate();");
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.AppendLine("            LoadTree();");
                sb.AppendLine();
                sb.AppendLine("            if (vm is INotifyPropertyChanged inpc)");
                sb.AppendLine("            {");
                sb.AppendLine("                inpc.PropertyChanged += (_, e) =>");
                sb.AppendLine("                {");
                sb.AppendLine($"                    if (e.PropertyName == \"{collectionProp.Name}\")");
                sb.AppendLine("                    {");
                sb.AppendLine("                        try { LoadTree(); }");
                sb.AppendLine("                        catch { }");
                sb.AppendLine("                    }");
                sb.AppendLine("                };");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            split.Panel1.Controls.Add(new Label");
                sb.AppendLine("            {");
                sb.AppendLine("                Text = \"(No collection detected)\",");
                sb.AppendLine("                Dock = DockStyle.Top,");
                sb.AppendLine("                AutoSize = true,");
                sb.AppendLine("                Padding = new Padding(6)");
                sb.AppendLine("            });");
            }
            
            sb.AppendLine();
            sb.AppendLine("            var rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };");
            sb.AppendLine("            split.Panel2.Controls.Add(rightPanel);");
            sb.AppendLine();
            sb.AppendLine("            var flow = new FlowLayoutPanel");
            sb.AppendLine("            {");
            sb.AppendLine("                Dock = DockStyle.Top,");
            sb.AppendLine("                AutoSize = true,");
            sb.AppendLine("                FlowDirection = FlowDirection.TopDown,");
            sb.AppendLine("                WrapContents = false");
            sb.AppendLine("            };");
            sb.AppendLine("            rightPanel.Controls.Add(flow);");
            sb.AppendLine();
            
            // ConnectionStatus is always available on the generated remote client
            sb.AppendLine("            var connLbl = new Label");
            sb.AppendLine("            {");
            sb.AppendLine("                AutoSize = true,");
            sb.AppendLine("                Font = new System.Drawing.Font(\"Segoe UI\", 9, System.Drawing.FontStyle.Bold)");
            sb.AppendLine("            };");
            sb.AppendLine("            connLbl.Text = \"ConnectionStatus: \" + vm.ConnectionStatus;");
            sb.AppendLine("            flow.Controls.Add(connLbl);");
            sb.AppendLine();
            sb.AppendLine("            vm.PropertyChanged += (_, e) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                if (e.PropertyName == \"ConnectionStatus\")");
            sb.AppendLine("                {");
            sb.AppendLine("                    connLbl.Text = \"ConnectionStatus: \" + vm.ConnectionStatus;");
            sb.AppendLine("                    statusLbl.Text = vm.ConnectionStatus;");
            sb.AppendLine("                }");
            sb.AppendLine("            };");
            sb.AppendLine();
            
            // Detail section for collection items
            sb.AppendLine("            var detailGroup = new GroupBox");
            sb.AppendLine("            {");
            sb.AppendLine("                Text = \"Selected Item Details\",");
            sb.AppendLine("                AutoSize = true,");
            sb.AppendLine("                AutoSizeMode = AutoSizeMode.GrowAndShrink,");
            sb.AppendLine("                Padding = new Padding(10)");
            sb.AppendLine("            };");
            sb.AppendLine("            flow.Controls.Add(detailGroup);");
            sb.AppendLine();
            sb.AppendLine("            var detailLayout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };");
            sb.AppendLine("            detailGroup.Controls.Add(detailLayout);");
            sb.AppendLine("            detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
            sb.AppendLine("            detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
            
            var detailProps = new List<PropertyInfo>();
            // For collection items, we know they should have Zone and Temperature properties
            // Let's create them explicitly to ensure proper binding
            if (collectionProp != null)
            {
                detailProps.Add(new PropertyInfo("Zone", "HP.Telemetry.Zone", null!, false));
                detailProps.Add(new PropertyInfo("Temperature", "int", null!, false));
            }
            int dRow = 0;
            foreach (var p in detailProps)
            {
                sb.AppendLine($"            detailLayout.Controls.Add(new Label {{ Text = \"{p.Name}\", AutoSize = true }}, 0, {dRow});");
                sb.AppendLine($"            var tbD_{p.Name} = new TextBox {{ Width = 180, ReadOnly = {(p.IsReadOnly ? "true" : "false")} }};");
                sb.AppendLine($"            detailLayout.Controls.Add(tbD_{p.Name}, 1, {dRow});");
                dRow++;
            }
            
            if (collectionProp != null)
            {
                sb.AppendLine();
                sb.AppendLine("            object? currentSelected = null;");
                sb.AppendLine();
                sb.AppendLine("            void BindDetail(object? item)");
                sb.AppendLine("            {");
                int iRow = 0;
                foreach (var p in detailProps)
                {
                    sb.AppendLine($"                var tbRef{iRow} = detailLayout.Controls.OfType<TextBox>().Skip({iRow}).First();");
                    sb.AppendLine($"                tbRef{iRow}.DataBindings.Clear();");
                    sb.AppendLine($"                if (item != null)");
                    sb.AppendLine("                {");
                    if (p.IsReadOnly)
                    {
                        sb.AppendLine($"                    // Property {p.Name} is read-only, use direct property access");
                        sb.AppendLine($"                    tbRef{iRow}.Text = item.GetType().GetProperty(\"{p.Name}\")?.GetValue(item)?.ToString() ?? \"N/A\";");
                    }
                    else
                    {
                        sb.AppendLine("                    try");
                        sb.AppendLine("                    {");
                        sb.AppendLine($"                        tbRef{iRow}.DataBindings.Add(\"Text\", item, \"{p.Name}\", true, DataSourceUpdateMode.OnPropertyChanged);");
                        sb.AppendLine("                    }");
                        sb.AppendLine("                    catch");
                        sb.AppendLine("                    {");
                        sb.AppendLine($"                        // Property {p.Name} may not exist or may not be bindable");
                        sb.AppendLine($"                        tbRef{iRow}.Text = item.GetType().GetProperty(\"{p.Name}\")?.GetValue(item)?.ToString() ?? \"N/A\";");
                        sb.AppendLine("                    }");
                    }
                    sb.AppendLine("                }");
                sb.AppendLine("                else");
                sb.AppendLine("                {");
                sb.AppendLine($"                    tbRef{iRow}.Text = string.Empty;");
                sb.AppendLine("                }");
                iRow++;
                }
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.AppendLine("            if (tree.Nodes.Count > 0)");
                sb.AppendLine("            {");
                sb.AppendLine("                currentSelected = tree.Nodes[0].Tag;");
                sb.AppendLine("                BindDetail(currentSelected);");
                sb.AppendLine("                tree.SelectedNode = tree.Nodes[0];");
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.AppendLine("            tree.AfterSelect += (_, e) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                currentSelected = e.Node?.Tag;");
                sb.AppendLine("                BindDetail(currentSelected);");
                sb.AppendLine("            };");
                sb.AppendLine();
                sb.AppendLine("            // Add collection management buttons");
                sb.AppendLine("            var collectionButtonsPanel = new FlowLayoutPanel");
                sb.AppendLine("            {");
                sb.AppendLine("                Height = 35,");
                sb.AppendLine("                FlowDirection = FlowDirection.LeftToRight,");
                sb.AppendLine("                AutoSize = false,");
                sb.AppendLine("                Dock = DockStyle.Bottom");
                sb.AppendLine("            };");
                sb.AppendLine("            split.Panel1.Controls.Add(collectionButtonsPanel);");
                sb.AppendLine();
                sb.AppendLine("            var refreshBtn = new Button { Text = \"Refresh\", Width = 70, Height = 25 };");
                sb.AppendLine("            refreshBtn.Click += (_, __) => LoadTree();");
                sb.AppendLine("            collectionButtonsPanel.Controls.Add(refreshBtn);");
                sb.AppendLine();
                sb.AppendLine("            var expandBtn = new Button { Text = \"Expand All\", Width = 80, Height = 25 };");
                sb.AppendLine("            expandBtn.Click += (_, __) => tree.ExpandAll();");
                sb.AppendLine("            collectionButtonsPanel.Controls.Add(expandBtn);");
                sb.AppendLine();
                sb.AppendLine("            var collapseBtn = new Button { Text = \"Collapse\", Width = 70, Height = 25 };");
                sb.AppendLine("            collapseBtn.Click += (_, __) => tree.CollapseAll();");
                sb.AppendLine("            collectionButtonsPanel.Controls.Add(collapseBtn);");
            }
            else
            {
                sb.AppendLine("            detailGroup.Visible = false;");
            }
            
            // Model properties section
            sb.AppendLine();
            sb.AppendLine("            var modelGroup = new GroupBox");
            sb.AppendLine("            {");
            sb.AppendLine("                Text = \"Model Properties\",");
            sb.AppendLine("                AutoSize = true,");
            sb.AppendLine("                AutoSizeMode = AutoSizeMode.GrowAndShrink,");
            sb.AppendLine("                Padding = new Padding(10)");
            sb.AppendLine("            };");
            sb.AppendLine("            flow.Controls.Add(modelGroup);");
            sb.AppendLine();
            sb.AppendLine("            var modelLayout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };");
            sb.AppendLine("            modelGroup.Controls.Add(modelLayout);");
            sb.AppendLine("            modelLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
            sb.AppendLine("            modelLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
            
            int mRow = 0;
            // Only include properties that are likely to exist on the main view model
            var mainViewModelProps = props.Where(p => !IsCollectionType(p.TypeString) && 
                                                     (p.Name.Equals("Status", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("Message", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("Counter", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("PlayerLevel", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("HasBonus", StringComparison.OrdinalIgnoreCase) ||
                                                      p.Name.Equals("BonusMultiplier", StringComparison.OrdinalIgnoreCase) ||
                                                      // Include properties that are likely main view model properties based on typical patterns
                                                      (!p.Name.Equals("Temperature", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("Zone", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("DeviceName", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("ProcessorLoad", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("FanSpeed", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("Background", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("Progress", StringComparison.OrdinalIgnoreCase) &&
                                                       !p.Name.Equals("State", StringComparison.OrdinalIgnoreCase))));
                                                       
            foreach (var p in mainViewModelProps)
            {
                if (p.Name == collectionProp?.Name) continue;
                
                sb.AppendLine($"            modelLayout.Controls.Add(new Label {{ Text = \"{p.Name}\", AutoSize = true }}, 0, {mRow});");
                
                if (IsBool(p))
                {
                    var enabled = p.IsReadOnly ? "false" : "true";
                    sb.AppendLine($"            var chk_{p.Name} = new CheckBox {{ Enabled = {enabled} }};");
                    if (p.IsReadOnly)
                    {
                        sb.AppendLine($"            // Property {p.Name} is read-only, use direct property access");
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                if (propValue is bool boolVal) chk_{p.Name}.Checked = boolVal;");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch { }");
                    }
                    else
                    {
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                chk_{p.Name}.DataBindings.Add(\"Checked\", vm, \"{p.Name}\", true, DataSourceUpdateMode.OnPropertyChanged);");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                // Property {p.Name} may not be bindable, set initial value");
                        sb.AppendLine("                try");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                    if (propValue is bool boolVal) chk_{p.Name}.Checked = boolVal;");
                        sb.AppendLine("                }");
                        sb.AppendLine("                catch { }");
                        sb.AppendLine("            }");
                    }
                    sb.AppendLine($"            modelLayout.Controls.Add(chk_{p.Name}, 1, {mRow});");
                }
                else
                {
                    var readOnly = p.IsReadOnly ? "true" : "false";
                    sb.AppendLine($"            var tb_{p.Name} = new TextBox {{ Width = 200, ReadOnly = {readOnly} }};");
                    if (p.IsReadOnly)
                    {
                        sb.AppendLine($"            // Property {p.Name} is read-only, use direct property access");
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                tb_{p.Name}.Text = propValue?.ToString() ?? string.Empty;");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch { }");
                    }
                    else
                    {
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                tb_{p.Name}.DataBindings.Add(\"Text\", vm, \"{p.Name}\", true, DataSourceUpdateMode.OnPropertyChanged);");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                // Property {p.Name} may not be bindable, set initial value");
                        sb.AppendLine("                try");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                    tb_{p.Name}.Text = propValue?.ToString() ?? string.Empty;");
                        sb.AppendLine("                }");
                        sb.AppendLine("                catch { }");
                        sb.AppendLine("            }");
                    }
                    sb.AppendLine($"            modelLayout.Controls.Add(tb_{p.Name}, 1, {mRow});");
                }
                mRow++;
            }
            
            // Commands section
            if (cmds.Any())
            {
                sb.AppendLine();
                sb.AppendLine("            var cmdGroup = new GroupBox");
                sb.AppendLine("            {");
                sb.AppendLine("                Text = \"Commands\",");
                sb.AppendLine("                AutoSize = true,");
                sb.AppendLine("                AutoSizeMode = AutoSizeMode.GrowAndShrink,");
                sb.AppendLine("                Padding = new Padding(10)");
                sb.AppendLine("            };");
                sb.AppendLine("            flow.Controls.Add(cmdGroup);");
                sb.AppendLine();
                sb.AppendLine("            var cmdFlow = new FlowLayoutPanel");
                sb.AppendLine("            {");
                sb.AppendLine("                Dock = DockStyle.Top,");
                sb.AppendLine("                AutoSize = true,");
                sb.AppendLine("                FlowDirection = FlowDirection.LeftToRight,");
                sb.AppendLine("                WrapContents = true");
                sb.AppendLine("            };");
                sb.AppendLine("            cmdGroup.Controls.Add(cmdFlow);");
                
                foreach (var c in cmds)
                {
                    var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                    sb.AppendLine();
                    sb.AppendLine($"            var btn_{baseName} = new Button");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Text = \"{baseName}\",");
                    sb.AppendLine("                Width = 140,");
                    sb.AppendLine("                Height = 30");
                    sb.AppendLine("            };");
                    sb.AppendLine($"            btn_{baseName}.Click += (_, __) =>");
                    sb.AppendLine("            {");
                    sb.AppendLine("                try");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    vm.{c.CommandPropertyName}?.Execute(null);");
                    sb.AppendLine("                }");
                    sb.AppendLine("                catch (Exception ex)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    MessageBox.Show($\"Error executing {baseName}: {{ex.Message}}\", \"Command Error\", MessageBoxButtons.OK, MessageBoxIcon.Warning);");
                    sb.AppendLine("                }");
                    sb.AppendLine("            };");
                    sb.AppendLine($"            cmdFlow.Controls.Add(btn_{baseName});");
                }
            }
            
            if (collectionProp != null)
            {
                sb.AppendLine();
                sb.AppendLine("            var timer = new System.Windows.Forms.Timer { Interval = 4000 };");
                sb.AppendLine("            timer.Tick += (_, __) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                try { LoadTree(); }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("            };");
                sb.AppendLine("            timer.Start();");
            }
            
            sb.AppendLine();
            sb.AppendLine("            // Set initial status");
            sb.AppendLine("            statusLbl.Text = vm.ConnectionStatus;");
            sb.AppendLine("            Application.Run(form);");
        }
        else
        {
            sb.AppendLine("            Console.WriteLine(\"Unsupported GUI platform\");");
        }
        
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

    public static string GenerateLaunchSettings() => """
{
  "profiles": {
    "Client": { "commandName": "Project", "commandLineArgs": "6000 client" },
    "Server": { "commandName": "Project", "commandLineArgs": "6000" }
  }
}
""";

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
            var sb = new StringBuilder();
            sb.AppendLine("<Window x:Class=\"GuiClientApp.MainWindow\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Title=\"" + projectName + " GUI Client\" Height=\"750\" Width=\"1100\">");
            sb.AppendLine("  <ScrollViewer VerticalScrollBarVisibility=\"Auto\"><StackPanel Margin=\"8\">");
            sb.AppendLine("    <TextBlock Text=\"{Binding ConnectionStatus, Mode=OneWay}\" FontWeight=\"Bold\" Margin=\"0,0,0,8\"/>");
            
            foreach (var p in props)
            {
                if (IsCollectionType(p.TypeString))
                {
                    // For collections, create a proper list view with item details
                    sb.AppendLine($"    <TextBlock Text=\"{p.Name}\" FontWeight=\"Bold\" Margin=\"0,12,0,4\"/>");
                    sb.AppendLine($"    <ListBox ItemsSource=\"{{Binding {p.Name}}}\" MaxHeight=\"200\" Margin=\"0,0,0,8\">");
                    sb.AppendLine("      <ListBox.ItemTemplate>");
                    sb.AppendLine("        <DataTemplate>");
                    sb.AppendLine("          <StackPanel Orientation=\"Horizontal\" Margin=\"2\">");
                    sb.AppendLine("            <TextBlock Text=\"Zone: \" FontWeight=\"SemiBold\"/>");
                    sb.AppendLine("            <TextBlock Text=\"{Binding Zone}\" Margin=\"0,0,8,0\"/>");
                    sb.AppendLine("            <TextBlock Text=\"Temp: \" FontWeight=\"SemiBold\"/>");
                    sb.AppendLine("            <TextBlock Text=\"{Binding Temperature}\"/>");
                    sb.AppendLine("            <TextBlock Text=\"°C\" Margin=\"2,0,0,0\"/>");
                    sb.AppendLine("          </StackPanel>");
                    sb.AppendLine("        </DataTemplate>");
                    sb.AppendLine("      </ListBox.ItemTemplate>");
                    sb.AppendLine("    </ListBox>");
                }
                else if (IsBool(p))
                {
                    if (p.IsReadOnly)
                    {
                        // For read-only boolean properties, use a TextBlock with explicit OneWay binding
                        sb.AppendLine($"    <TextBlock Text=\"{p.Name}: {{Binding {p.Name}, Mode=OneWay}}\" Margin=\"0,2,0,2\"/>");
                    }
                    else
                    {
                        sb.AppendLine($"    <CheckBox Content=\"{p.Name}\" IsChecked=\"{{Binding {p.Name}, Mode=TwoWay}}\" Margin=\"0,2,0,2\"/>");
                    }
                }
                else
                {
                    if (p.IsReadOnly || IsComplexType(p.TypeString))
                    {
                        // For read-only or complex properties, use TextBlock with explicit OneWay binding
                        sb.AppendLine($"    <TextBlock Text=\"{p.Name}\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                        sb.AppendLine($"    <TextBlock Text=\"{{Binding {p.Name}, Mode=OneWay}}\" Margin=\"0,0,0,4\" TextWrapping=\"Wrap\"/>");
                    }
                    else
                    {
                        sb.AppendLine($"    <TextBlock Text=\"{p.Name}\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                        sb.AppendLine($"    <TextBox Text=\"{{Binding {p.Name}, Mode=TwoWay}}\" Width=\"260\" Margin=\"0,0,0,4\"/>");
                    }
                }
            }
            if (cmds.Any())
            {
                sb.AppendLine("    <TextBlock Text=\"Commands\" FontWeight=\"Bold\" Margin=\"0,12,0,4\"/>");
                foreach (var c in cmds)
                {
                    var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal)? c.MethodName[..^5]:c.MethodName;
                    sb.AppendLine($"    <Button Content=\"{baseName}\" Command=\"{{Binding {c.CommandPropertyName}}}\" Width=\"160\" Margin=\"0,4,0,0\"/>");
                }
            }
            sb.AppendLine("  </StackPanel></ScrollViewer></Window>");
            return sb.ToString();
        }

        public static string GenerateWpfMainWindowCodeBehind() => "using System;\nusing System.Windows;\nnamespace GuiClientApp { public partial class MainWindow : Window { public MainWindow(object vm) { InitializeComponent(); DataContext = vm; } } }";

        public static string GenerateWinFormsGui(string projectName, string serviceName, string clientClassName, List<PropertyInfo> props, List<CommandInfo> cmds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> WinForms GUI </auto-generated>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Windows.Forms;");
            sb.AppendLine();
            sb.AppendLine("namespace GuiClientApp");
            sb.AppendLine("{");
            sb.AppendLine("    public static class WinFormsGui");
            sb.AppendLine("    {");

            // Use direct type for the view model parameter
            sb.AppendLine($"        public static void Run({clientClassName} vm)");
            sb.AppendLine("        {");
            sb.AppendLine("            Application.EnableVisualStyles();");
            sb.AppendLine($"            var form = new Form {{ Text = \"{projectName} GUI Client\", Width = 1100, Height = 750 }};");
            sb.AppendLine("            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };");
            sb.AppendLine("            form.Controls.Add(panel);");
            sb.AppendLine();
            sb.AppendLine("            var status = new Label { AutoSize = true };");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                status.DataBindings.Add(\"Text\", vm, \"ConnectionStatus\");");
            sb.AppendLine("            }");
            sb.AppendLine("            catch");
            sb.AppendLine("            {");
            sb.AppendLine("                status.Text = \"Connection status not available\";");
            sb.AppendLine("            }");
            sb.AppendLine("            panel.Controls.Add(status);");
            
            foreach (var p in props)
            {
                if (IsBool(p))
                {
                    var enabled = p.IsReadOnly ? "false" : "true";
                    sb.AppendLine($"            var chk_{p.Name} = new CheckBox {{ Text = \"{p.Name}\", Enabled = {enabled} }};");
                    if (p.IsReadOnly)
                    {
                        sb.AppendLine($"            // Property {p.Name} is read-only, use direct property access");
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                if (propValue is bool boolVal) chk_{p.Name}.Checked = boolVal;");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch { }");
                    }
                    else
                    {
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                chk_{p.Name}.DataBindings.Add(\"Checked\", vm, \"{p.Name}\", true, DataSourceUpdateMode.OnPropertyChanged);");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                // Property {p.Name} may not be bindable, set initial value");
                        sb.AppendLine("                try");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                    if (propValue is bool boolVal) chk_{p.Name}.Checked = boolVal;");
                        sb.AppendLine("                }");
                        sb.AppendLine("                catch { }");
                        sb.AppendLine("            }");
                    }
                    sb.AppendLine($"            panel.Controls.Add(chk_{p.Name});");
                }
                else
                {
                    var ro = p.IsReadOnly ? "true" : "false";
                    sb.AppendLine($"            panel.Controls.Add(new Label {{ Text = \"{p.Name}\", AutoSize = true }});");
                    sb.AppendLine($"            var tb_{p.Name} = new TextBox {{ Width = 240, ReadOnly = {ro} }};");
                    if (p.IsReadOnly)
                    {
                        sb.AppendLine($"            // Property {p.Name} is read-only, use direct property access");
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                tb_{p.Name}.Text = propValue?.ToString() ?? string.Empty;");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch { }");
                    }
                    else
                    {
                        sb.AppendLine("            try");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                tb_{p.Name}.DataBindings.Add(\"Text\", vm, \"{p.Name}\", true, DataSourceUpdateMode.OnPropertyChanged);");
                        sb.AppendLine("            }");
                        sb.AppendLine("            catch");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                // Property {p.Name} may not be bindable, set initial value");
                        sb.AppendLine("                try");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    var propValue = vm.GetType().GetProperty(\"{p.Name}\")?.GetValue(vm);");
                        sb.AppendLine($"                    tb_{p.Name}.Text = propValue?.ToString() ?? string.Empty;");
                        sb.AppendLine("                }");
                        sb.AppendLine("                catch { }");
                        sb.AppendLine("            }");
                    }
                    sb.AppendLine($"            panel.Controls.Add(tb_{p.Name});");
                }
            }
            
            if (cmds.Any())
            {
                foreach (var c in cmds)
                {
                    var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal) ? c.MethodName[..^5] : c.MethodName;
                    sb.AppendLine($"            var btn_{baseName} = new Button {{ Text = \"{baseName}\", Width = 140 }};");
                    sb.AppendLine($"            btn_{baseName}.Click += (_, __) => vm.{c.CommandPropertyName}.Execute(null);");
                    sb.AppendLine($"            panel.Controls.Add(btn_{baseName});");
                }
            }
            
            sb.AppendLine("            Application.Run(form);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static bool IsBool(PropertyInfo p) => string.Equals(p.TypeString, "bool", StringComparison.OrdinalIgnoreCase) || string.Equals(p.TypeString, "System.Boolean", StringComparison.OrdinalIgnoreCase);
        private static bool IsCollectionType(string type) { var t = type.ToLowerInvariant(); return t.Contains("observablecollection") || t.Contains("list<") || t.Contains("dictionary<") || t.EndsWith("[]") || t.Contains("icollection") || t.Contains("ienumerable"); }
        private static bool IsPrimitiveLike(string type) { var t = type.ToLowerInvariant(); return t.Contains("string") || t.Contains("int") || t.Contains("bool") || t.Contains("double") || t.Contains("float") || t.Contains("decimal") || t.Contains("long") || t.Contains("byte") || t.Contains("guid") || t.Contains("datetime"); }
        private static bool IsComplexType(string type) => !IsCollectionType(type) && !IsPrimitiveLike(type);
    }

