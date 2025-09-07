using RemoteMvvmTool.Generators;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;

namespace ToolExecution;

public class CsProjectGeneratorTests
{
    [Fact]
    public void GenerateCsProj_UsesWpfFlag()
    {
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "wpf");
        Assert.Contains("<UseWPF>true</UseWPF>", proj);
        Assert.Contains("protos/Svc.proto", proj);
        Assert.DoesNotContain("UseWindowsForms", proj);
    }

    [Fact]
    public void GenerateCsProj_UsesWinFormsFlag()
    {
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "winforms");
        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", proj);
        Assert.DoesNotContain("<UseWPF>", proj);
    }

    [Fact]
    public void GenerateGuiClientProgram_WpfCreatesMainWindow()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("MyApp", "wpf", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        Assert.Contains("var app = new Application();", prog);
        Assert.Contains("app.Run(win);", prog);
        Assert.Contains("var win = new MainWindow(vm);", prog);
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsCreatesForm()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("MyApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        Assert.Contains("Application.EnableVisualStyles();", prog);
        Assert.Contains("Application.Run(form);", prog);
        Assert.Contains("var form = new Form", prog);
    }

    [Fact]
    public void GenerateGuiClientProgram_IncludesRemoteClientSetup()
    {
        var props = new List<PropertyInfo> { new("IsEnabled", "bool", null!) };
        var cmds = new List<CommandInfo> { new("DoWork", "DoWorkCommand", new List<ParameterInfo>(), false) };
        string prog = CsProjectGenerator.GenerateGuiClientProgram("Vm", "wpf", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        Assert.Contains("SvcServiceClient", prog);
        Assert.Contains("SvcRemoteClient", prog);
        Assert.Contains("InitializeRemoteAsync", prog);
    }

    [Fact]
    public void GenerateWpfMainWindowXaml_HandlesCollections()
    {
        var props = new List<PropertyInfo>
        {
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("Status", "string", null!)
        };
        var cmds = new List<CommandInfo>();
        string xaml = CsProjectGenerator.GenerateWpfMainWindowXaml("TestApp", "TestRemoteClient", props, cmds);
        
        // Should create ListBox for collections
        Assert.Contains("<ListBox ItemsSource=\"{Binding ZoneList}\"", xaml);
        Assert.Contains("<ListBox.ItemTemplate>", xaml);
        Assert.Contains("<DataTemplate>", xaml);
        Assert.Contains("{Binding Zone}", xaml);
        Assert.Contains("{Binding Temperature}", xaml);
        
        // Should create TextBox for simple properties
        Assert.Contains("{Binding Status, Mode=TwoWay}", xaml);
    }

    [Fact]
    public void GenerateWpfMainWindowXaml_HandlesReadOnlyProperties()
    {
        var props = new List<PropertyInfo>
        {
            new("Instructions", "string", null!, true),
            new("Counter", "int", null!, false)
        };
        var cmds = new List<CommandInfo>();
        string xaml = CsProjectGenerator.GenerateWpfMainWindowXaml("TestApp", "TestRemoteClient", props, cmds);
        
        // Read-only property should use OneWay binding
        Assert.Contains("{Binding Instructions, Mode=OneWay}", xaml);
        
        // Writable property should use TwoWay binding
        Assert.Contains("{Binding Counter, Mode=TwoWay}", xaml);
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsIncludesTreeView()
    {
        var props = new List<PropertyInfo> 
        { 
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("Status", "string", null!)
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        // Should include tree view for collections
        Assert.Contains("var tree = new TreeView", prog);
        Assert.Contains("split.Panel1.Controls.Add(tree);", prog);
        Assert.Contains("LoadTree();", prog);
        
        // Should include collection management buttons
        Assert.Contains("var refreshBtn = new Button { Text = \"Refresh\"", prog);
        Assert.Contains("var expandBtn = new Button { Text = \"Expand All\"", prog);
        Assert.Contains("var collapseBtn = new Button { Text = \"Collapse\"", prog);
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsIncludesDetailBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("Status", "string", null!)
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        // Should include detail binding for collection items
        Assert.Contains("void BindDetail(object? item)", prog);
        Assert.Contains("detailLayout.Controls.Add", prog);
        Assert.Contains("DataBindings.Add(\"Text\", item,", prog);
    }

    [Fact]
    public void GenerateGuiClientProgram_IncludesConnectionStatus()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("MyApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        // Should always include ConnectionStatus
        Assert.Contains("ConnectionStatus", prog);
        Assert.Contains("vm.ConnectionStatus", prog);
    }

    [Fact]
    public void GenerateWpfAppCodeBehind_IncludesRemoteInitialization()
    {
        string codeBehind = CsProjectGenerator.GenerateWpfAppCodeBehind("TestService", "TestRemoteClient");
        
        Assert.Contains("TestServiceClient", codeBehind);
        Assert.Contains("TestRemoteClient", codeBehind);
        Assert.Contains("InitializeRemoteAsync", codeBehind);
        Assert.Contains("new MainWindow(vm)", codeBehind);
    }

    [Fact]
    public void GenerateGuiClientProgram_IncludesCommands()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo> 
        { 
            new("DoWork", "DoWorkCommand", new List<ParameterInfo>(), false),
            new("ProcessAsync", "ProcessCommand", new List<ParameterInfo>(), true)
        };
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        Assert.Contains("DoWorkCommand", prog);
        Assert.Contains("ProcessCommand", prog);
        Assert.Contains("vm.DoWorkCommand?.Execute", prog);
        Assert.Contains("vm.ProcessCommand?.Execute", prog);
    }

    [Fact]
    public void GenerateProgramCs_ReturnsLegacyPlaceholder()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateProgramCs("MyApp", "wpf", "Proto.Ns", "Svc", "Client.Ns", props, cmds);
        
        // The legacy method now just returns a placeholder
        Assert.Contains("Legacy combined harness omitted", prog);
    }

    [Fact]
    public void GenerateWinFormsGui_IncludesDataBindingWithErrorHandling()
    {
        var props = new List<PropertyInfo> 
        { 
            new("Message", "string", null!),
            new("IsEnabled", "bool", null!, true) // read-only
        };
        var cmds = new List<CommandInfo>();
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // Should include try-catch for data binding
        Assert.Contains("try", gui);
        Assert.Contains("DataBindings.Add", gui);
        Assert.Contains("catch", gui);
        
        // Should handle read-only properties differently
        Assert.Contains("// Property IsEnabled is read-only", gui);
        Assert.Contains("GetProperty(\"IsEnabled\")", gui);
    }

    [Fact]
    public void GenerateWpfMainWindowXaml_IncludesBooleanCheckBoxes()
    {
        var props = new List<PropertyInfo>
        {
            new("IsActive", "bool", null!),
            new("IsReadOnlyFlag", "bool", null!, true)
        };
        var cmds = new List<CommandInfo>();
        string xaml = CsProjectGenerator.GenerateWpfMainWindowXaml("TestApp", "TestRemoteClient", props, cmds);
        
        // Writable boolean should use CheckBox with TwoWay
        Assert.Contains("<CheckBox Content=\"IsActive\" IsChecked=\"{Binding IsActive, Mode=TwoWay}\"", xaml);
        
        // Read-only boolean should use TextBlock with OneWay
        Assert.Contains("IsReadOnlyFlag: {Binding IsReadOnlyFlag, Mode=OneWay}", xaml);
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsHandlesEmptyCollections()
    {
        var props = new List<PropertyInfo> 
        { 
            new("Status", "string", null!)
            // No collection properties
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        // Should handle case with no collections gracefully
        Assert.Contains("(No collection detected)", prog);
        Assert.Contains("detailGroup.Visible = false", prog);
    }

    [Fact]
    public void GenerateWpfMainWindowXaml_IncludesCommands()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo> 
        { 
            new("SaveDataAsync", "SaveDataCommand", new List<ParameterInfo>(), true),
            new("RefreshData", "RefreshDataCommand", new List<ParameterInfo>(), false)
        };
        string xaml = CsProjectGenerator.GenerateWpfMainWindowXaml("TestApp", "TestRemoteClient", props, cmds);
        
        Assert.Contains("Commands", xaml);
        Assert.Contains("Command=\"{Binding SaveDataCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding RefreshDataCommand}\"", xaml);
        Assert.Contains("Content=\"SaveData\"", xaml); // Should strip "Async" suffix
        Assert.Contains("Content=\"RefreshData\"", xaml);
    }
}
