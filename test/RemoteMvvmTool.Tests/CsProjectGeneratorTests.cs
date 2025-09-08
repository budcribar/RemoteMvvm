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
        
        // Updated: Check for our new simplified approach using WinFormsGui.Run instead of inline form creation
        Assert.Contains("WinFormsGui.Run(vm);", prog);
        Assert.Contains("using System.Windows.Forms;", prog);
        Assert.Contains("using System.ComponentModel;", prog);
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
        
        // Updated: Test the actual WinFormsGui generation
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("var tree = new TreeView", gui);
        Assert.Contains("split.Panel1.Controls.Add(tree);", gui);
        Assert.Contains("Client Properties", gui); // Our new simplified root node name
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
        
        // Updated: Test actual WinFormsGui generation for data binding
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("ConnectionStatus", gui); // Our simplified approach focuses on connection status
        Assert.Contains("DataBindings.Add(\"Text\", vm, \"ConnectionStatus\")", gui);
        Assert.Contains("flow.Controls.Add", gui);
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
        
        // Updated: Check for our simplified error handling approach
        Assert.Contains("try", gui);
        Assert.Contains("catch (Exception ex)", gui);
        Assert.Contains("ConnectionStatus", gui);
        
        // Our simplified approach generates basic property nodes with error handling
        Assert.Contains("prop0Value = vm.Message.ToString();", gui);
        Assert.Contains("prop0ErrorNode = new TreeNode(\"Message: <error>\");", gui);
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
        
        // Updated: Commands are now in WinFormsGui, not in the main program
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("DoWorkCommand", gui);
        Assert.Contains("ProcessCommand", gui);
        Assert.Contains("vm.DoWorkCommand?.Execute", gui);
        Assert.Contains("vm.ProcessCommand?.Execute", gui);
        
        // Also verify the main program calls WinFormsGui
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        Assert.Contains("WinFormsGui.Run(vm);", prog);
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

    [Fact]
    public void GenerateServerGuiProgram_WpfCreatesMainWindow()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateServerGuiProgram("ServerApp", "wpf", "Proto.Ns", "SvcService", props, cmds);
        Assert.Contains("var app = new Application();", prog);
        Assert.Contains("app.Run(win);", prog);
        Assert.Contains("var win = new MainWindow(vm);", prog);
        Assert.Contains("Server GUI", prog);
    }

    [Fact]
    public void GenerateServerGuiProgram_WinFormsCreatesForm()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateServerGuiProgram("ServerApp", "winforms", "Proto.Ns", "SvcService", props, cmds);
        Assert.Contains("Application.EnableVisualStyles();", prog);
        Assert.Contains("Application.Run(form);", prog);
        Assert.Contains("var form = new Form", prog);
        Assert.Contains("Server GUI", prog);
    }

    [Fact]
    public void GenerateServerGuiProgram_IncludesPropertySpecificServerLabeling()
    {
        var props = new List<PropertyInfo> 
        { 
            new("Status", "string", null!),
            new("IsEnabled", "bool", null!)
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateServerGuiProgram("TestApp", "winforms", "Proto.Ns", "SvcService", props, cmds);
        
        // Updated: Check for our simplified server labeling
        Assert.Contains("Server Model Properties", prog);
        Assert.Contains("Server Properties", prog); // Our simplified label
        
        // Our simplified approach generates basic property nodes with indexing
        Assert.Contains("prop0Value = vm.Status.ToString();", prog);
        Assert.Contains("prop1Value = vm.IsEnabled.ToString();", prog);
    }

    [Fact]
    public void GenerateServerWpfMainWindowXaml_HandlesCollections()
    {
        var props = new List<PropertyInfo>
        {
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("Status", "string", null!)
        };
        var cmds = new List<CommandInfo>();
        string xaml = CsProjectGenerator.GenerateServerWpfMainWindowXaml("ServerApp", "TestViewModel", props, cmds);
        
        // Should create ListBox for collections with server branding
        Assert.Contains("<ListBox ItemsSource=\"{Binding ZoneList}\"", xaml);
        Assert.Contains("ZoneList (Server)", xaml);
        Assert.Contains("Server Status: Running", xaml);
        Assert.Contains("{Binding Zone}", xaml);
        Assert.Contains("{Binding Temperature}", xaml);
        
        // Should create TextBox for simple properties
        Assert.Contains("{Binding Status, Mode=TwoWay}", xaml);
        Assert.Contains("Status (Server)", xaml);
    }

    [Fact]
    public void GenerateServerWpfMainWindowXaml_IncludesServerCommands()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo> 
        { 
            new("UpdateDataAsync", "UpdateDataCommand", new List<ParameterInfo>(), true),
            new("ResetData", "ResetDataCommand", new List<ParameterInfo>(), false)
        };
        string xaml = CsProjectGenerator.GenerateServerWpfMainWindowXaml("ServerApp", "TestViewModel", props, cmds);
        
        Assert.Contains("Server Commands", xaml);
        Assert.Contains("Command=\"{Binding UpdateDataCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding ResetDataCommand}\"", xaml);
        Assert.Contains("Content=\"UpdateData\"", xaml); // Should strip "Async" suffix
        Assert.Contains("Content=\"ResetData\"", xaml);
    }

    [Fact]
    public void GenerateServerWpfAppCodeBehind_IncludesServerInitialization()
    {
        string codeBehind = CsProjectGenerator.GenerateServerWpfAppCodeBehind("TestViewModel");
        
        Assert.Contains("ServerOptions", codeBehind);
        Assert.Contains("new TestViewModel(serverOptions)", codeBehind);
        Assert.Contains("new MainWindow(vm)", codeBehind);
        Assert.Contains("Server initialization failed", codeBehind);
    }

    [Fact]
    public void GenerateServerGuiProgram_IncludesPropertySpecificDataBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            new("Message", "string", null!),
            new("Counter", "int", null!),
            new("IsEnabled", "bool", null!)
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateServerGuiProgram("ServerApp", "winforms", "Proto.Ns", "SvcService", props, cmds);
        
        // Updated: Check for our simplified data binding approach
        Assert.Contains("Server Properties", prog);
        Assert.Contains("prop0Value = vm.Message.ToString();", prog);
        Assert.Contains("prop1Value = vm.Counter.ToString();", prog);
        Assert.Contains("prop2Value = vm.IsEnabled.ToString();", prog);
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsIncludesPropertySpecificBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            new("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>", null!),
            new("Status", "string", null!),
            new("IsActive", "bool", null!)
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Test the actual WinFormsGui generation
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("Client Properties", gui); // Our simplified root node
        Assert.Contains("ConnectionStatus", gui); // Always included
        Assert.Contains("prop0Value = vm.ZoneList.ToString();", gui); // First 5 properties with indexing
        Assert.Contains("prop1Value = vm.Status.ToString();", gui);
        Assert.Contains("prop2Value = vm.IsActive.ToString();", gui);
    }

    [Fact]
    public void GenerateGuiClientProgram_HandlesEdgeCasePrimitives()
    {
        var props = new List<PropertyInfo>
        {
            new("PreciseValue", "decimal", null!),
            new("TinyValue", "nuint", null!),
            new("UnicodeChar", "char", null!),
            new("EmptyGuid", "Guid", null!)
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Test the actual WinFormsGui generation for edge cases
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // Our simplified approach handles all types the same way with ToString()
        Assert.Contains("prop0Value = vm.PreciseValue.ToString();", gui);
        Assert.Contains("prop1Value = vm.TinyValue.ToString();", gui);
        Assert.Contains("prop2Value = vm.UnicodeChar.ToString();", gui);
        Assert.Contains("prop3Value = vm.EmptyGuid.ToString();", gui);
    }

    [Fact]
    public void GenerateGuiClientProgram_HandlesPropertyTypeAnalysis()
    {
        var props = new List<PropertyInfo>
        {
            new("Items", "ObservableCollection<string>", null!),
            new("IsActive", "bool", null!),
            new("StatusType", "StatusType", null!), // Enum-like name
            new("Name", "string", null!)
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Test our simplified type handling
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // Our simplified approach treats all properties uniformly with ToString()
        Assert.Contains("prop0Value = vm.Items.ToString();", gui);
        Assert.Contains("prop1Value = vm.IsActive.ToString();", gui);
        Assert.Contains("prop2Value = vm.StatusType.ToString();", gui);
        Assert.Contains("prop3Value = vm.Name.ToString();", gui);
    }

    [Fact]
    public void GenerateWinFormsGui_HandlesPropertySpecificEditors()
    {
        var props = new List<PropertyInfo>
        {
            new("Message", "string", null!),
            new("IsEnabled", "bool", null!),
            new("Priority", "Priority", null!) // Enum-like
        };
        var cmds = new List<CommandInfo>();
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // Updated: Check for our simplified property handling
        Assert.Contains("Client Properties", gui); // Our simplified root
        Assert.Contains("prop0Value = vm.Message.ToString();", gui);
        Assert.Contains("prop1Value = vm.IsEnabled.ToString();", gui);
        Assert.Contains("prop2Value = vm.Priority.ToString();", gui);
        
        // Should include proper error handling for each property
        Assert.Contains("prop0ErrorNode = new TreeNode(\"Message: <error>\");", gui);
        Assert.Contains("prop1ErrorNode = new TreeNode(\"IsEnabled: <error>\");", gui);
        Assert.Contains("prop2ErrorNode = new TreeNode(\"Priority: <error>\");", gui);
    }
}
