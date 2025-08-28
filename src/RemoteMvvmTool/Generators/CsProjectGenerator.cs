using System;
using System.Collections.Generic;
using System.Text;
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
    {(isWpf ? "<UseWPF>true</UseWPF>" : string.Empty)}
    {(isWinForms ? "<UseWindowsForms>true</UseWindowsForms>" : string.Empty)}
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="Grpc.Net.Client.Web" Version="2.71.0" />
    <PackageReference Include="Google.Protobuf" Version="3.31.1" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0" PrivateAssets="all" />
    <Protobuf Include="protos/{serviceName}.proto" GrpcServices="Client" ProtoRoot="protos" Access="Public" />
  </ItemGroup>
</Project>
""";
    }

    public static string GenerateProgramCs(string projectName, string runType, string protoNs, string serviceName, string clientNs, List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        bool isWpf = string.Equals(runType, "wpf", StringComparison.OrdinalIgnoreCase);
        bool isWinForms = string.Equals(runType, "winforms", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {clientNs};");
        if (isWpf)
        {
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("using System.Windows.Controls;");
            sb.AppendLine("using System.Windows.Data;");
        }
        else if (isWinForms)
        {
            sb.AppendLine("using System.Windows.Forms;");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName};");
        sb.AppendLine();
        sb.AppendLine("public class Program");
        sb.AppendLine("{");
        sb.AppendLine("    [STAThread]");
        sb.AppendLine("    public static async Task Main()");
        sb.AppendLine("    {");
        sb.AppendLine("        var channel = GrpcChannel.ForAddress(\"http://localhost:50052\");");
        sb.AppendLine($"        var grpcClient = new {serviceName}.{serviceName}Client(channel);");
        sb.AppendLine($"        var vm = new {projectName}RemoteClient(grpcClient);");
        sb.AppendLine("        await vm.InitializeRemoteAsync();");
        sb.AppendLine();

        if (isWpf)
        {
            sb.AppendLine("        var app = new Application();");
            sb.AppendLine($"        var window = new Window {{ Title = \"{projectName}\" }};");
            sb.AppendLine("        var panel = new StackPanel { Margin = new Thickness(10) };");
            sb.AppendLine("        window.Content = panel;");
            sb.AppendLine("        var status = new TextBlock();");
            sb.AppendLine($"        status.SetBinding(TextBlock.TextProperty, new Binding(nameof({projectName}RemoteClient.ConnectionStatus)) {{ Source = vm }});");
            sb.AppendLine("        panel.Children.Add(status);");
            foreach (var p in props)
            {
                string camel = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                bool isBool = p.TypeString.Contains("bool", StringComparison.OrdinalIgnoreCase);
                if (isBool)
                {
                    sb.AppendLine($"        var {camel}Check = new CheckBox {{ Content = \"{p.Name}\" }};");
                    sb.AppendLine($"        {camel}Check.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof({projectName}RemoteClient.{p.Name})) {{ Source = vm }});");
                    sb.AppendLine($"        {camel}Check.Click += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", NewValue = Any.Pack(new BoolValue {{ Value = {camel}Check.IsChecked == true }}) }});");
                    sb.AppendLine($"        panel.Children.Add({camel}Check);");
                }
                else
                {
                    sb.AppendLine($"        var {camel}Label = new TextBlock {{ Text = \"{p.Name}\" }};");
                    sb.AppendLine($"        panel.Children.Add({camel}Label);");
                    sb.AppendLine($"        var {camel}Box = new TextBox();");
                    sb.AppendLine($"        {camel}Box.SetBinding(TextBox.TextProperty, new Binding(nameof({projectName}RemoteClient.{p.Name})) {{ Source = vm }});");
                    sb.AppendLine($"        {camel}Box.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", NewValue = Any.Pack(new StringValue {{ Value = {camel}Box.Text }}) }});");
                    sb.AppendLine($"        panel.Children.Add({camel}Box);");
                }
            }
            foreach (var c in cmds)
            {
                sb.AppendLine($"        var btn{c.MethodName} = new Button {{ Content = \"{c.MethodName}\" }};");
                sb.AppendLine($"        btn{c.MethodName}.Click += (_, __) => vm.{c.CommandPropertyName}.Execute(null);");
                sb.AppendLine($"        panel.Children.Add(btn{c.MethodName});");
            }
            sb.AppendLine("        app.Run(window);");
        }
        else if (isWinForms)
        {
            sb.AppendLine("        ApplicationConfiguration.Initialize();");
            sb.AppendLine($"        var form = new Form {{ Text = \"{projectName}\" }};");
            sb.AppendLine("        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };");
            sb.AppendLine("        form.Controls.Add(panel);");
            sb.AppendLine("        var status = new Label();");
            sb.AppendLine($"        status.DataBindings.Add(\"Text\", vm, nameof({projectName}RemoteClient.ConnectionStatus));");
            sb.AppendLine("        panel.Controls.Add(status);");
            foreach (var p in props)
            {
                string camel = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                bool isBool = p.TypeString.Contains("bool", StringComparison.OrdinalIgnoreCase);
                if (isBool)
                {
                    sb.AppendLine($"        var {camel}Check = new CheckBox {{ Text = \"{p.Name}\" }};");
                    sb.AppendLine($"        {camel}Check.DataBindings.Add(\"Checked\", vm, nameof({projectName}RemoteClient.{p.Name}));");
                    sb.AppendLine($"        {camel}Check.CheckedChanged += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", NewValue = Any.Pack(new BoolValue {{ Value = {camel}Check.Checked }}) }});");
                    sb.AppendLine($"        panel.Controls.Add({camel}Check);");
                }
                else
                {
                    sb.AppendLine($"        var {camel}Label = new Label {{ Text = \"{p.Name}\" }};");
                    sb.AppendLine($"        panel.Controls.Add({camel}Label);");
                    sb.AppendLine($"        var {camel}Box = new TextBox {{ Width = 200 }};");
                    sb.AppendLine($"        {camel}Box.DataBindings.Add(\"Text\", vm, nameof({projectName}RemoteClient.{p.Name}));");
                    sb.AppendLine($"        {camel}Box.Leave += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{{ PropertyName = \"{p.Name}\", NewValue = Any.Pack(new StringValue {{ Value = {camel}Box.Text }}) }});");
                    sb.AppendLine($"        panel.Controls.Add({camel}Box);");
                }
            }
            foreach (var c in cmds)
            {
                sb.AppendLine($"        var btn{c.MethodName} = new Button {{ Text = \"{c.MethodName}\" }};");
                sb.AppendLine($"        btn{c.MethodName}.Click += (_, __) => vm.{c.CommandPropertyName}.Execute(null);");
                sb.AppendLine($"        panel.Controls.Add(btn{c.MethodName});");
            }
            sb.AppendLine("        Application.Run(form);");
        }
        else
        {
            sb.AppendLine("        Console.WriteLine(\"Unsupported run type\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

