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
        var sb = new StringBuilder();
        sb.AppendLine("<Window x:Class=\"ServerApp.MainWindow\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Title=\"Server GUI - " + projectName + "\" Height=\"750\" Width=\"1100\">");
        sb.AppendLine("  <ScrollViewer VerticalScrollBarVisibility=\"Auto\"><StackPanel Margin=\"8\">");
        sb.AppendLine("    <TextBlock Text=\"Server Status: Running\" FontWeight=\"Bold\" Foreground=\"Green\" Margin=\"0,0,0,8\"/>");
        
        foreach (var p in props)
        {
            if (IsCollectionType(p.TypeString))
            {
                sb.AppendLine($"    <TextBlock Text=\"{p.Name} (Server)\" FontWeight=\"Bold\" Margin=\"0,12,0,4\"/>");
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
                    sb.AppendLine($"    <TextBlock Text=\"{p.Name}: {{Binding {p.Name}, Mode=OneWay}}\" Margin=\"0,2,0,2\"/>");
                }
                else
                {
                    sb.AppendLine($"    <CheckBox Content=\"{p.Name} (Server)\" IsChecked=\"{{Binding {p.Name}, Mode=TwoWay}}\" Margin=\"0,2,0,2\"/>");
                }
            }
            else
            {
                if (p.IsReadOnly || IsComplexType(p.TypeString))
                {
                    sb.AppendLine($"    <TextBlock Text=\"{p.Name}\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                    sb.AppendLine($"    <TextBlock Text=\"{{Binding {p.Name}, Mode=OneWay}}\" Margin=\"0,0,0,4\" TextWrapping=\"Wrap\"/>");
                }
                else
                {
                    sb.AppendLine($"    <TextBlock Text=\"{p.Name} (Server)\" FontWeight=\"SemiBold\" Margin=\"0,6,0,0\"/>");
                    sb.AppendLine($"    <TextBox Text=\"{{Binding {p.Name}, Mode=TwoWay}}\" Width=\"260\" Margin=\"0,0,0,4\"/>");
                }
            }
        }
        if (cmds.Any())
        {
            sb.AppendLine("    <TextBlock Text=\"Server Commands\" FontWeight=\"Bold\" Margin=\"0,12,0,4\"/>");
            foreach (var c in cmds)
            {
                var baseName = c.MethodName.EndsWith("Async", StringComparison.Ordinal)? c.MethodName[..^5]:c.MethodName;
                sb.AppendLine($"    <Button Content=\"{baseName}\" Command=\"{{Binding {c.CommandPropertyName}}}\" Width=\"160\" Margin=\"0,4,0,0\"/>");
            }
        }
        sb.AppendLine("  </StackPanel></ScrollViewer></Window>");
        return sb.ToString();
    }

    public static string GenerateServerWpfMainWindowCodeBehind() => "using System;\nusing System.Windows;\nnamespace ServerApp { public partial class MainWindow : Window { public MainWindow(object vm) { InitializeComponent(); DataContext = vm; } } }";
}