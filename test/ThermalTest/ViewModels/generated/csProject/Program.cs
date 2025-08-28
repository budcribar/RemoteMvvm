using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using Generated.Protos;
using HPSystemsTools.ViewModels.RemoteClients;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HP3LSThermalTestViewModel;

public class Program
{
    [STAThread]
    public static async Task Main()
    {
        var channel = GrpcChannel.ForAddress("http://localhost:50052");
        var grpcClient = new HP3LSThermalTestViewModelService.HP3LSThermalTestViewModelServiceClient(channel);
        var vm = new HP3LSThermalTestViewModelRemoteClient(grpcClient);
        await vm.InitializeRemoteAsync();

        var app = new Application();
        var window = new Window { Title = "HP3LSThermalTestViewModel" };
        var panel = new StackPanel { Margin = new Thickness(10) };
        window.Content = panel;
        var status = new TextBlock();
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.ConnectionStatus)) { Source = vm });
        panel.Children.Add(status);
        var instructionsLabel = new TextBlock { Text = "Instructions" };
        panel.Children.Add(instructionsLabel);
        var instructionsBox = new TextBox();
        instructionsBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.Instructions)) { Source = vm });
        instructionsBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "Instructions", NewValue = Any.Pack(new StringValue { Value = instructionsBox.Text }) });
        panel.Children.Add(instructionsBox);
        var cpuTemperatureThresholdLabel = new TextBlock { Text = "CpuTemperatureThreshold" };
        panel.Children.Add(cpuTemperatureThresholdLabel);
        var cpuTemperatureThresholdBox = new TextBox();
        cpuTemperatureThresholdBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.CpuTemperatureThreshold)) { Source = vm });
        cpuTemperatureThresholdBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "CpuTemperatureThreshold", NewValue = Any.Pack(new StringValue { Value = cpuTemperatureThresholdBox.Text }) });
        panel.Children.Add(cpuTemperatureThresholdBox);
        var cpuLoadThresholdLabel = new TextBlock { Text = "CpuLoadThreshold" };
        panel.Children.Add(cpuLoadThresholdLabel);
        var cpuLoadThresholdBox = new TextBox();
        cpuLoadThresholdBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.CpuLoadThreshold)) { Source = vm });
        cpuLoadThresholdBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "CpuLoadThreshold", NewValue = Any.Pack(new StringValue { Value = cpuLoadThresholdBox.Text }) });
        panel.Children.Add(cpuLoadThresholdBox);
        var cpuLoadTimeSpanLabel = new TextBlock { Text = "CpuLoadTimeSpan" };
        panel.Children.Add(cpuLoadTimeSpanLabel);
        var cpuLoadTimeSpanBox = new TextBox();
        cpuLoadTimeSpanBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.CpuLoadTimeSpan)) { Source = vm });
        cpuLoadTimeSpanBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "CpuLoadTimeSpan", NewValue = Any.Pack(new StringValue { Value = cpuLoadTimeSpanBox.Text }) });
        panel.Children.Add(cpuLoadTimeSpanBox);
        var zonesLabel = new TextBlock { Text = "Zones" };
        panel.Children.Add(zonesLabel);
        var zonesBox = new TextBox();
        zonesBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.Zones)) { Source = vm });
        zonesBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "Zones", NewValue = Any.Pack(new StringValue { Value = zonesBox.Text }) });
        panel.Children.Add(zonesBox);
        var testSettingsLabel = new TextBlock { Text = "TestSettings" };
        panel.Children.Add(testSettingsLabel);
        var testSettingsBox = new TextBox();
        testSettingsBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.TestSettings)) { Source = vm });
        testSettingsBox.LostFocus += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "TestSettings", NewValue = Any.Pack(new StringValue { Value = testSettingsBox.Text }) });
        panel.Children.Add(testSettingsBox);
        var showDescriptionCheck = new CheckBox { Content = "ShowDescription" };
        showDescriptionCheck.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.ShowDescription)) { Source = vm });
        showDescriptionCheck.Click += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "ShowDescription", NewValue = Any.Pack(new BoolValue { Value = showDescriptionCheck.IsChecked == true }) });
        panel.Children.Add(showDescriptionCheck);
        var showReadmeCheck = new CheckBox { Content = "ShowReadme" };
        showReadmeCheck.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(HP3LSThermalTestViewModelRemoteClient.ShowReadme)) { Source = vm });
        showReadmeCheck.Click += async (_, __) => await grpcClient.UpdatePropertyValueAsync(new UpdatePropertyValueRequest{ PropertyName = "ShowReadme", NewValue = Any.Pack(new BoolValue { Value = showReadmeCheck.IsChecked == true }) });
        panel.Children.Add(showReadmeCheck);
        var btnStateChanged = new Button { Content = "StateChanged" };
        btnStateChanged.Click += (_, __) => vm.StateChangedCommand.Execute(null);
        panel.Children.Add(btnStateChanged);
        var btnCancelTest = new Button { Content = "CancelTest" };
        btnCancelTest.Click += (_, __) => vm.CancelTestCommand.Execute(null);
        panel.Children.Add(btnCancelTest);
        app.Run(window);
    }
}
