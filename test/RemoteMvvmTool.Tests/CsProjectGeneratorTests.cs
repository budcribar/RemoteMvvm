using RemoteMvvmTool.Generators;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;
using System;

namespace ToolExecution;

public class CsProjectGeneratorTests
{
    // ===== SERVER UI GENERATION FLAGS =====
    /// <summary>
    /// Controls whether server GUI generation is tested in CsProjectGeneratorTests.
    /// Set to false to skip server UI generation validation in unit tests.
    /// </summary>
    private static readonly bool TestServerUIGeneration = false; // Disabled to avoid conflicts with client tests
    
    /// <summary>
    /// Default server UI platform for testing server UI generation.
    /// Can be "wpf" or "winforms".
    /// </summary>
    private static readonly string DefaultServerUITestPlatform = "wpf";

    /// <summary>
    /// Helper method to test server UI generation when enabled
    /// </summary>
    private static void TestServerUIGenerationIfEnabled(string projectName, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds, string platform = null)
    {
        if (!TestServerUIGeneration) return;
        
        var uiPlatform = platform ?? DefaultServerUITestPlatform;
        var serverProgram = CsProjectGenerator.GenerateServerGuiProgram(projectName, uiPlatform, "Proto.Ns", serviceName, props, cmds);
        
        // Validate basic server UI generation
        Assert.NotNull(serverProgram);
        Assert.Contains("Server GUI", serverProgram);
        
        if (uiPlatform.Equals("wpf", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("var app = new Application();", serverProgram);
            Assert.Contains("app.Run(win);", serverProgram);
            Assert.Contains("var win = new MainWindow(vm);", serverProgram);
            
            // Test WPF XAML generation
            var serverXaml = CsProjectGenerator.GenerateServerWpfMainWindowXaml(projectName, serviceName.Replace("Service", ""), props, cmds);
            var serverCodeBehind = CsProjectGenerator.GenerateServerWpfAppCodeBehind(serviceName.Replace("Service", ""));
            Assert.NotNull(serverXaml);
            Assert.NotNull(serverCodeBehind);
            Assert.Contains("Server Status: Running", serverXaml);
            Assert.Contains("ServerOptions", serverCodeBehind);
        }
        else if (uiPlatform.Equals("winforms", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("Application.EnableVisualStyles();", serverProgram);
            Assert.Contains("Application.Run(form);", serverProgram);
            Assert.Contains("var form = new Form", serverProgram);
            Assert.Contains("Server ViewModel Properties", serverProgram);
        }
        
        // Log successful server UI generation test
        Console.WriteLine($"[CsProjectGeneratorTests] Server UI generation test passed for {projectName} using {uiPlatform}");
    }

    [Fact]
    public void Test_ServerUI_Configuration_Validation()
    {
        // Log server UI generation settings for this test run
        Console.WriteLine($"[CsProjectGeneratorTests] TestServerUIGeneration: {TestServerUIGeneration}");
        Console.WriteLine($"[CsProjectGeneratorTests] DefaultServerUITestPlatform: {DefaultServerUITestPlatform}");
        
        // Simple validation test
        Assert.True(DefaultServerUITestPlatform == "wpf" || DefaultServerUITestPlatform == "winforms");
    }

    [Fact]
    public void GenerateCsProj_UsesWpfFlag()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "wpf");
        Assert.Contains("<UseWPF>true</UseWPF>", proj);
        Assert.Contains("protos/Svc.proto", proj);
        Assert.DoesNotContain("UseWindowsForms", proj);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestProj", "SvcService", props, cmds, "wpf");
    }

    [Fact]
    public void GenerateCsProj_UsesWinFormsFlag()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "winforms");
        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", proj);
        Assert.DoesNotContain("<UseWPF>", proj);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestProj", "SvcService", props, cmds, "winforms");
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("MyApp", "SvcService", props, cmds, "wpf");
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("MyApp", "SvcService", props, cmds, "winforms");
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("Vm", "SvcService", props, cmds);
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds);
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds);
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
        
        // Updated: Test PropertyDiscoveryUtility comprehensive features
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("var tree = new TreeView", gui);
        Assert.Contains("split.Panel1.Controls.Add(tree);", gui);
        Assert.Contains("Client ViewModel Properties", gui); // PropertyDiscoveryUtility context labeling
        Assert.Contains("LoadTree();", gui); // PropertyDiscoveryUtility generates LoadTree method
        Assert.Contains("Collections", gui); // ZoneList should be categorized as collection
        Assert.Contains("Simple Properties", gui); // Status should be categorized as simple
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
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
        
        // Updated: Test our simplified PropertyDiscoveryUtility integration
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("ConnectionStatus", gui); // Still includes connection status
        Assert.Contains("PropertyNodeInfo", gui); // PropertyDiscoveryUtility generates PropertyNodeInfo
        Assert.Contains("LoadTree", gui); // LoadTree as Action delegate
        Assert.Contains("flow.Controls.Add", gui);
        Assert.Contains("Collections", gui); // ZoneList should be categorized as collection
        Assert.Contains("Simple Properties", gui); // Status should be categorized as simple
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
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
        
        // Updated: Check for our simplified PropertyDiscoveryUtility features
        Assert.Contains("try", gui);
        Assert.Contains("catch", gui);
        Assert.Contains("ConnectionStatus", gui);
        
        // PropertyDiscoveryUtility generates comprehensive property handling
        Assert.Contains("Simple Properties", gui); // Message should be categorized as simple
        Assert.Contains("Boolean Properties", gui); // IsEnabled should be categorized as boolean
        Assert.Contains("PropertyNodeInfo", gui); // PropertyDiscoveryUtility generates PropertyNodeInfo class
        Assert.Contains("IsSimpleProperty = true", gui); // Property categorization
        Assert.Contains("IsBooleanProperty = true", gui); // Property categorization
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateWpfAppCodeBehind_IncludesRemoteInitialization()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        
        string codeBehind = CsProjectGenerator.GenerateWpfAppCodeBehind("TestService", "TestRemoteClient");
        
        Assert.Contains("TestServiceClient", codeBehind);
        Assert.Contains("TestRemoteClient", codeBehind);
        Assert.Contains("InitializeRemoteAsync", codeBehind);
        Assert.Contains("new MainWindow(vm)", codeBehind);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds);
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "SvcService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateProgramCs_ReturnsLegacyPlaceholder()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateProgramCs("MyApp", "wpf", "Proto.Ns", "Svc", "Client.Ns", props, cmds);
        
        // The legacy method now just returns a placeholder
        Assert.Contains("Legacy combined harness omitted", prog);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("MyApp", "SvcService", props, cmds);
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestRemoteClient", props, cmds);
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
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestRemoteClient", props, cmds);
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
        
        // Updated: Check for PropertyDiscoveryUtility comprehensive property handling
        Assert.Contains("Server Properties", prog); // Server labeling
        Assert.Contains("Simple Properties", prog); // Message and Counter should be categorized as simple
        Assert.Contains("Boolean Properties", prog); // IsEnabled should be categorized as boolean
        Assert.Contains("PropertyNodeInfo", prog); // PropertyDiscoveryUtility generates PropertyNodeInfo class
        Assert.Contains("IsSimpleProperty = true", prog); // PropertyDiscoveryUtility property categorization
        Assert.Contains("IsBooleanProperty = true", prog); // PropertyDiscoveryUtility property categorization
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
        
        // Updated: Test PropertyDiscoveryUtility comprehensive property categorization
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        Assert.Contains("Client ViewModel Properties", gui); // PropertyDiscoveryUtility context labeling
        Assert.Contains("ConnectionStatus", gui); // Always included
        Assert.Contains("Collections", gui); // ZoneList should be categorized as collection
        Assert.Contains("Simple Properties", gui); // Status should be categorized as simple
        Assert.Contains("Boolean Properties", gui); // IsActive should be categorized as boolean
        Assert.Contains("IsCollectionProperty = true", gui); // Property categorization
        Assert.Contains("IsSimpleProperty = true", gui); // Property categorization
        Assert.Contains("IsBooleanProperty = true", gui); // Property categorization
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
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
        
        // Updated: Test PropertyDiscoveryUtility comprehensive type analysis for edge cases
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // PropertyDiscoveryUtility should categorize all these as simple properties
        Assert.Contains("Simple Properties", gui);
        Assert.Contains("PropertyNodeInfo", gui);
        Assert.Contains("IsSimpleProperty = true", gui);
        // Should handle all primitive types safely with proper categorization
        Assert.Contains("LoadTree();", gui);
        Assert.Contains("tree.BeginUpdate();", gui);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
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
        
        // Updated: Test PropertyDiscoveryUtility comprehensive type analysis
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // PropertyDiscoveryUtility should categorize properties correctly
        Assert.Contains("Simple Properties", gui); // Name
        Assert.Contains("Boolean Properties", gui); // IsActive
        Assert.Contains("Collections", gui); // Items
        Assert.Contains("Enum Properties", gui); // StatusType (detected by naming pattern)
        Assert.Contains("IsCollectionProperty = true", gui);
        Assert.Contains("IsBooleanProperty = true", gui);
        Assert.Contains("IsEnumProperty = true", gui);
        Assert.Contains("IsSimpleProperty = true", gui);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateWinFormsGui_HandlesPropertySpecificEditors()
    {
        var props = new List<PropertyInfo>
        {
            new("Message", "string", null!),
            new("IsEnabled", "bool", null!),
            new("PriorityType", "PriorityType", null!) // Enum-like name with "Type" suffix that matches IsEnumType detection
        };
        var cmds = new List<CommandInfo>();
        string gui = CsProjectGenerator.GenerateWinFormsGui("TestApp", "TestService", "TestRemoteClient", props, cmds);
        
        // Updated: Check for our simplified PropertyDiscoveryUtility integration
        Assert.Contains("Simple Properties", gui);
        Assert.Contains("Boolean Properties", gui);
        Assert.Contains("Enum Properties", gui);
        Assert.Contains("PropertyNodeInfo", gui);
        Assert.Contains("LoadTree", gui); // Using Action<> delegate instead of method
        Assert.Contains("IsSimpleProperty = true", gui);
        Assert.Contains("IsBooleanProperty = true", gui);
        Assert.Contains("IsEnumProperty = true", gui);
        
        // Check for tree structure
        Assert.Contains("rootNode.Nodes.Add", gui);
        Assert.Contains("tree.BeginUpdate", gui);
        Assert.Contains("tree.EndUpdate", gui);
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }
}
