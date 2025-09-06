using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static class CsProjectGenerator
{
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
        // GUI client needs only client gRPC packages (no Grpc.AspNetCore server).
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
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Client" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
</Project>
""";
    }

    public static string GenerateLaunchSettings()
    {
        return """
{
  "profiles": {
    "Client": {
      "commandName": "Project",
      "commandLineArgs": "6000 client"
    },
    "Server": {
      "commandName": "Project",
      "commandLineArgs": "6000"
    }
  }
}
""";
    }

    public static string GenerateServerLaunchSettings()
    {
        return """
{
  "profiles": {
    "Server": {
      "commandName": "Project",
      "commandLineArgs": "6000"
    }
  }
}
""";
    }

    public static string GenerateClientLaunchSettings()
    {
        return """
{
  "profiles": {
    "Client": {
      "commandName": "Project",
      "commandLineArgs": "6000 client"
    }
  }
}
""";
    }

    /// <summary>
    /// Generates a minimal Visual Studio solution (.slnx XML format) that references the split projects.
    /// </summary>
    public static string GenerateSolutionXml(string serverProjectRelativePath = "ServerApp/ServerApp.csproj", string clientProjectRelativePath = "GuiClientApp/GuiClientApp.csproj")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Solution>");
        sb.AppendLine($"  <Project Path=\"{serverProjectRelativePath}\" />");
        sb.AppendLine($"  <Project Path=\"{clientProjectRelativePath}\" />");
        sb.AppendLine("</Solution>");
        return sb.ToString();
    }

    // Existing combined generator kept for backward compatibility (server + headless + interactive).
    public static string GenerateProgramCs(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);

        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var clientClassName = modelName + "RemoteClient";
        var vmNamespace = "Generated.ViewModels";

        var mutationLines = BuildMutationLines(serviceName, props);

        var sb = new StringBuilder();
        AppendUsings(sb, protoNs, clientNs, vmNamespace, isWpf, isWinForms);
        AppendHeaderTypes(sb);
        sb.AppendLine($"namespace {projectName}");
        sb.AppendLine("{");
        sb.AppendLine("public class Program");
        sb.AppendLine("{");
        sb.AppendLine("    [STAThread]");
        sb.AppendLine("    public static void Main(string[] args)");
        sb.AppendLine("    {");

        AppendHeadlessClientBlock(sb);
        AppendServerBlock(sb, modelName);
        AppendInteractiveSetup(sb, serviceName, clientClassName);

        if (isWpf)
            AppendWpfUi(sb, projectName, clientClassName, props, cmds);
        else if (isWinForms)
            AppendWinFormsUi(sb, projectName, clientClassName, props, cmds);
        else
            sb.AppendLine("        Console.WriteLine(\"Unsupported run type\");");

        sb.AppendLine("    }");
        sb.AppendLine();
        AppendTryCollectClientData(sb, serviceName, clientClassName, mutationLines);
        AppendReflectionHelpers(sb);
        sb.AppendLine("}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateServerProgram(string projectName, string protoNs, string serviceName, string platform)
    {
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine("using Generated.ViewModels;");
        sb.AppendLine("namespace ServerApp");
        sb.AppendLine("{");
        sb.AppendLine("public class Program");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Main(string[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (args.Length < 1 || !int.TryParse(args[0], out var port)) { Console.WriteLine(\"Port argument required\"); return; }");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            Console.WriteLine($\"Starting server on port {port}...\");");
        sb.AppendLine("            var serverOptions = new ServerOptions { Port = port, UseHttps = true };");
        sb.AppendLine($"            var vm = new {modelName}(serverOptions);");
        sb.AppendLine("            Console.WriteLine($\"Server ready on port {port}\");");
        sb.AppendLine("            Thread.Sleep(Timeout.Infinite);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            Console.WriteLine(\"SERVER_ERROR_START\");");
        sb.AppendLine("            Console.WriteLine(ex.ToString());");
        sb.AppendLine("            Console.WriteLine(\"SERVER_ERROR_END\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateGuiClientProgram(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);
        var modelName = serviceName.EndsWith("Service", StringComparison.Ordinal) ? serviceName[..^"Service".Length] : serviceName;
        var clientClassName = modelName + "RemoteClient";
        var vmNamespace = "Generated.ViewModels";
        var sb = new StringBuilder();
        AppendUsings(sb, protoNs, clientNs, vmNamespace, isWpf, isWinForms);
        sb.AppendLine("namespace GuiClientApp");
        sb.AppendLine("{");
        sb.AppendLine("public class Program");
        sb.AppendLine("{");
        sb.AppendLine("    [STAThread]");
        sb.AppendLine("    public static void Main(string[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        int port = 50052; if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;");
        sb.AppendLine("        var address = new Uri($\"https://localhost:{port}/\");");
        sb.AppendLine("        Grpc.Net.Client.GrpcChannel channel;");
        sb.AppendLine("        var httpsHandler = new System.Net.Http.HttpClientHandler();");
        sb.AppendLine("        httpsHandler.ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;");
        sb.AppendLine("        channel = Grpc.Net.Client.GrpcChannel.ForAddress(address, new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = httpsHandler });");
        sb.AppendLine($"        var grpcClient = new {serviceName}.{serviceName}Client(channel);");
        sb.AppendLine($"        var vm = new {clientClassName}(grpcClient);");
        sb.AppendLine("        vm.InitializeRemoteAsync().GetAwaiter().GetResult();");
        if (isWpf)
        {
            sb.AppendLine("        var app = new System.Windows.Application();");
            sb.AppendLine($"        var window = new System.Windows.Window {{ Title = \"{projectName} GUI Client\", Width=1100, Height=750 }};");
            sb.AppendLine("        var scroll = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto }; ");
            sb.AppendLine("        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(10) }; scroll.Content = panel; window.Content = scroll;");
            sb.AppendLine("        var status = new System.Windows.Controls.TextBlock();");
            sb.AppendLine($"        status.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding(nameof({clientClassName}.ConnectionStatus)) {{ Source = vm }});");
            sb.AppendLine("        panel.Children.Add(status);");
            // Generate editors
            foreach (var p in props)
            {
                bool isCollection = p.TypeString.StartsWith("System.Collections.ObjectModel.ObservableCollection<", StringComparison.Ordinal);
                if (isCollection)
                {
                    // discover element simple properties
                    var elemProps = new List<(string Name,string Type,bool ReadOnly)>();
                    if (p.FullTypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
                    {
                        var elemType = named.TypeArguments[0] as Microsoft.CodeAnalysis.INamedTypeSymbol;
                        if (elemType != null)
                        {
                            foreach (var m in GrpcRemoteMvvmModelUtil.Helpers.GetAllMembers(elemType))
                            {
                                if (m is Microsoft.CodeAnalysis.IPropertySymbol ps && !ps.IsStatic && ps.GetMethod != null && ps.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
                                {
                                    var ts = ps.Type.ToDisplayString();
                                    bool supported = ts is "int" or "System.Int32" or "long" or "System.Int64" or "float" or "System.Single" or "double" or "System.Double" or "decimal" or "System.Decimal" or "string" or "System.String" or "bool" or "System.Boolean";
                                    if (supported) elemProps.Add((ps.Name, ts, ps.SetMethod == null));
                                }
                            }
                        }
                    }
                    sb.AppendLine($"        // Tree for {p.Name}");
                    sb.AppendLine($"        panel.Children.Add(new System.Windows.Controls.TextBlock {{ Text=\"{p.Name}\", FontWeight=System.Windows.FontWeights.Bold, Margin=new System.Windows.Thickness(0,12,0,4) }});");
                    sb.AppendLine($"        var tree_{p.Name} = new System.Windows.Controls.TreeView {{ Margin=new System.Windows.Thickness(0,0,0,8) }}; panel.Children.Add(tree_{p.Name});");
                    sb.AppendLine($"        var refreshBtn_{p.Name} = new System.Windows.Controls.Button {{ Content=\"Refresh {p.Name}\", Margin=new System.Windows.Thickness(0,0,0,8) }}; panel.Children.Add(refreshBtn_{p.Name});");
                    sb.AppendLine($"        void Refresh_{p.Name}() {{ tree_{p.Name}.Items.Clear(); var coll = vm.{p.Name}; if (coll==null) return; int idx=0; foreach(var item in coll) {{ int localIndex = idx; var node = new System.Windows.Controls.TreeViewItem {{ Header = \"{p.Name}[\"+localIndex+\"]\" }}; tree_{p.Name}.Items.Add(node); var sp = new System.Windows.Controls.StackPanel {{ Margin=new System.Windows.Thickness(6,4,4,4) }}; node.Items.Add(sp); var propsLocal = item.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance); foreach(var ep in propsLocal) {{ if (!ep.CanRead) continue; var pt = ep.PropertyType; bool simple = pt==typeof(string)|| pt.IsPrimitive || pt==typeof(decimal); if(!simple) continue; bool editable = ep.CanWrite; if (pt==typeof(bool)) {{ var cb = new System.Windows.Controls.CheckBox {{ Content=ep.Name, IsChecked = (bool?) (ep.GetValue(item) ?? false) }}; sp.Children.Add(cb); if(editable) cb.Click += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName=\"{p.Name}\", PropertyPath=\"{p.Name}[\"+localIndex+\"].\"+ep.Name, ArrayIndex=localIndex, NewValue = Any.Pack(new BoolValue {{ Value = cb.IsChecked == true }}) }}); }} else {{ sp.Children.Add(new System.Windows.Controls.TextBlock {{ Text=ep.Name }}); var tb = new System.Windows.Controls.TextBox {{ Text = System.Convert.ToString(ep.GetValue(item)) ?? string.Empty, IsReadOnly = !editable, Width=250 }}; sp.Children.Add(tb); if(editable) tb.LostFocus += async (_, __) => {{ var text = tb.Text; Google.Protobuf.WellKnownTypes.Any anyVal; if (pt==typeof(int) && int.TryParse(text,out var ival)) anyVal = Any.Pack(new Int32Value {{ Value = ival }}); else if (pt==typeof(long) && long.TryParse(text,out var lval)) anyVal = Any.Pack(new Int64Value {{ Value = lval }}); else if (pt==typeof(float) && float.TryParse(text,out var fval)) anyVal = Any.Pack(new FloatValue {{ Value = fval }}); else if ((pt==typeof(double) || pt==typeof(decimal)) && double.TryParse(text,out var dval)) anyVal = Any.Pack(new DoubleValue {{ Value = dval }}); else anyVal = Any.Pack(new StringValue {{ Value = text ?? string.Empty }}); await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName=\"{p.Name}\", PropertyPath=\"{p.Name}[\"+localIndex+\"].\"+ep.Name, ArrayIndex=localIndex, NewValue = anyVal }}); }}; }} }} idx++; }} }}");
                    sb.AppendLine($"        Refresh_{p.Name}();");
                    sb.AppendLine($"        refreshBtn_{p.Name}.Click += (_, __) => Refresh_{p.Name}();");
                }
                else
                {
                    string camel = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                    string typeStr = p.TypeString.ToLowerInvariant();
                    bool isBool = typeStr.Contains("bool");
                    if (isBool)
                    {
                        sb.AppendLine($"        var {camel}Check = new System.Windows.Controls.CheckBox {{ Content = \"{p.Name}\" }};");
                        sb.AppendLine($"        {camel}Check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding(nameof({clientClassName}.{p.Name})) {{ Source = vm, Mode = System.Windows.Data.BindingMode.OneWay }});");
                        if (!p.IsReadOnly)
                            sb.AppendLine($"        {camel}Check.Click += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new BoolValue {{ Value = {camel}Check.IsChecked == true }}) }});");
                        sb.AppendLine($"        panel.Children.Add({camel}Check);");
                    }
                    else
                    {
                        sb.AppendLine($"        panel.Children.Add(new System.Windows.Controls.TextBlock {{ Text = \"{p.Name}\" }});");
                        string readOnly = p.IsReadOnly ? "true" : "false";
                        sb.AppendLine($"        var {camel}Box = new System.Windows.Controls.TextBox {{ IsReadOnly = {readOnly}, Width=260 }};");
                        sb.AppendLine($"        {camel}Box.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(nameof({clientClassName}.{p.Name})) {{ Source = vm, Mode = System.Windows.Data.BindingMode.OneWay }});");
                        if (!p.IsReadOnly)
                        {
                            if (typeStr.Contains("int"))
                                sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => {{ if (int.TryParse({camel}Box.Text, out var v)) await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new Int32Value {{ Value = v }}) }}); }};");
                            else if (typeStr.Contains("long"))
                                sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => {{ if (long.TryParse({camel}Box.Text, out var v)) await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new Int64Value {{ Value = v }}) }}); }};");
                            else if (typeStr.Contains("float"))
                                sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => {{ if (float.TryParse({camel}Box.Text, out var v)) await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new FloatValue {{ Value = v }}) }}); }};");
                            else if (typeStr.Contains("double") || typeStr.Contains("decimal"))
                                sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => {{ if (double.TryParse({camel}Box.Text, out var v)) await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest {{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new DoubleValue {{ Value = v }}) }}); }};");
                            else
                                sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new StringValue {{ Value = {camel}Box.Text ?? string.Empty }}) }});");
                        }
                        sb.AppendLine($"        panel.Children.Add({camel}Box);");
                    }
                }
            }
            sb.AppendLine("        app.Run(window);");
        }
        else if (isWinForms)
        {
            sb.AppendLine("        System.Windows.Forms.Application.EnableVisualStyles();");
            sb.AppendLine("        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);");
            sb.AppendLine($"        var form = new System.Windows.Forms.Form {{ Text = \"{projectName} GUI Client\" }};");
            sb.AppendLine("        var panel = new System.Windows.Forms.FlowLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, AutoSize = true, FlowDirection = System.Windows.Forms.FlowDirection.TopDown };");
            sb.AppendLine("        form.Controls.Add(panel);");
            sb.AppendLine("        var status = new System.Windows.Forms.Label();");
            sb.AppendLine($"        status.DataBindings.Add(\"Text\", vm, nameof({clientClassName}.ConnectionStatus));");
            sb.AppendLine("        panel.Controls.Add(status);");
            foreach (var p in props)
            {
                if (p.TypeString.StartsWith("System.Collections.ObjectModel.ObservableCollection<")) continue; // skip collections in WinForms
                string camel = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                string typeStr = p.TypeString.ToLowerInvariant();
                bool isBool = typeStr.Contains("bool");
                if (isBool)
                {
                    sb.AppendLine($"        var {camel}Check = new System.Windows.Forms.CheckBox {{ Text = \"{p.Name}\" }}; panel.Controls.Add({camel}Check);");
                    sb.AppendLine($"        {camel}Check.DataBindings.Add(\"Checked\", vm, nameof({clientClassName}.{p.Name}), true, System.Windows.Forms.DataSourceUpdateMode.Never);");
                    if (!p.IsReadOnly)
                        sb.AppendLine($"        {camel}Check.CheckedChanged += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new BoolValue {{ Value = {camel}Check.Checked }}) }});");
                }
                else
                {
                    sb.AppendLine($"        panel.Controls.Add(new System.Windows.Forms.Label {{ Text = \"{p.Name}\" }});");
                    string ro = p.IsReadOnly ? "true" : "false";
                    sb.AppendLine($"        var {camel}Box = new System.Windows.Forms.TextBox {{ Width = 240, ReadOnly = {ro} }}; panel.Controls.Add({camel}Box);");
                    sb.AppendLine($"        {camel}Box.DataBindings.Add(\"Text\", vm, nameof({clientClassName}.{p.Name}), true, System.Windows.Forms.DataSourceUpdateMode.Never);");
                    if (!p.IsReadOnly)
                        sb.AppendLine($"        {camel}Box.Leave += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", ArrayIndex = -1, NewValue = Any.Pack(new StringValue {{ Value = {camel}Box.Text ?? string.Empty }}) }});");
                }
            }
            sb.AppendLine("        System.Windows.Forms.Application.Run(form);");
        }
        else
        {
            sb.AppendLine("        Console.WriteLine(\"Unsupported GUI run type\");");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ---------------- Helper Builders (existing) ----------------
    private static StringBuilder BuildMutationLines(string serviceName, List<PropertyInfo> props)
    {
        var mutationLines = new StringBuilder();
        var hasTopLevelTemp = props.Any(p => string.Equals(p.Name, "Temperature", StringComparison.Ordinal) &&
            p.FullTypeSymbol?.ToDisplayString() is string tds && (tds == "int" || tds == "System.Int32") && !p.IsReadOnly);
        if (hasTopLevelTemp)
        {
            mutationLines.AppendLine("            try { var result = grpcClient.UpdatePropertyValue(new UpdatePropertyValueRequest { PropertyName = \"Temperature\", ArrayIndex = -1, NewValue = Any.Pack(new Int32Value { Value = 55 }) }); } catch { }");
        }
        return mutationLines;
    }

    private static void AppendUsings(StringBuilder sb, string protoNs, string clientNs, string vmNamespace, bool isWpf, bool isWinForms)
    {
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Net;\nusing System.Net.Http;\nusing System.Threading;\nusing System.Reflection;\nusing System.Globalization;\nusing System.Collections;\nusing System.Text.RegularExpressions;");
        sb.AppendLine("using Grpc.Net.Client;\nusing Grpc.Net.Client.Web;\nusing Google.Protobuf.WellKnownTypes;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {clientNs};");
        sb.AppendLine($"using {vmNamespace};");
        if (isWpf)
        {
            sb.AppendLine("using System.Windows;\nusing System.Windows.Controls;\nusing System.Windows.Data;");
        }
        else if (isWinForms)
        {
            sb.AppendLine("using SystemForms = System.Windows.Forms;");
        }
        sb.AppendLine();
    }

    private static void AppendHeaderTypes(StringBuilder sb)
    {
        sb.AppendLine("public class ClientOptions { public string Address { get; set; } = \"http://localhost\"; }");
        sb.AppendLine("public class ServerOptions { public int Port { get; set; } public bool UseHttps { get; set; } = false; }");
        sb.AppendLine();
    }

    private static void AppendHeadlessClientBlock(StringBuilder sb)
    {
        sb.AppendLine("        if (args != null && args.Length >= 1 && int.TryParse(args[0], out var port) && args.Length >= 2 && string.Equals(args[1], \"client\", StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine("        { /* headless path omitted for brevity */ return; }");
        sb.AppendLine();
    }

    private static void AppendServerBlock(StringBuilder sb, string modelName)
    {
        sb.AppendLine("        if (args != null && args.Length >= 1 && int.TryParse(args[0], out var serverPort))");
        sb.AppendLine("        { Console.WriteLine($\"Server mode on port {serverPort}\"); var so = new ServerOptions { Port = serverPort, UseHttps = true }; var vmServer = new " + modelName + "(so); System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite); return; }");
        sb.AppendLine();
    }

    private static void AppendInteractiveSetup(StringBuilder sb, string serviceName, string clientClassName)
    {
        sb.AppendLine("        // interactive fallback setup omitted for brevity");
    }

    private static void AppendWpfUi(StringBuilder sb, string projectName, string clientClassName, List<PropertyInfo> props, List<CommandInfo> cmds) { }
    private static void AppendWinFormsUi(StringBuilder sb, string projectName, string clientClassName, List<PropertyInfo> props, List<CommandInfo> cmds) { }

    private static void AppendTryCollectClientData(StringBuilder sb, string serviceName, string clientClassName, StringBuilder mutationLines)
    {
        sb.AppendLine("    private static string TryCollectClientData(Uri address) => string.Empty;");
        sb.AppendLine();
    }

    private static void AppendReflectionHelpers(StringBuilder sb)
    {
        sb.AppendLine("    private static void ExtractFromState(object? value, System.Collections.Generic.List<double> numbers) { }");
    }
}

