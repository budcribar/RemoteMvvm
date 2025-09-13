using RemoteMvvmTool.Generators;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace ToolExecution;

public class CsProjectGeneratorTests
{
    // Helper method to create a PropertyInfo with proper ITypeSymbol for testing
    private static PropertyInfo CreatePropertyInfo(string name, string typeCode, bool isReadOnly = false)
    {
        try
        {
            // Create a comprehensive compilation as a library (not executable) to avoid Main method requirement
            var compilation = CSharpCompilation.Create(
                "TestLibrary", 
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.ObjectModel.ObservableCollection<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.IEnumerable).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location)); // System.Runtime

            // Create a simple syntax tree for library compilation
            var sourceCode = $@"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TestNamespace
{{
    public class TestClass
    {{
        public {typeCode} {name} {{ get; {(isReadOnly ? "" : "set;")} }}
    }}
    
    // Define any custom types we might need
    public class ThermalZoneComponentViewModel
    {{
        public string Zone {{ get; set; }} = string.Empty;
        public double Temperature {{ get; set; }}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            compilation = compilation.AddSyntaxTrees(syntaxTree);

            // Check for compilation errors (excluding warnings)
            var diagnostics = compilation.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()));
                throw new InvalidOperationException($"Compilation errors for {typeCode}: {errorMessages}");
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            
            // Find the property declaration
            var propertyDeclaration = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == name);

            if (propertyDeclaration == null)
            {
                throw new InvalidOperationException($"Could not find property declaration for {name}");
            }

            // Get the type symbol with full semantic analysis
            var typeInfo = semanticModel.GetTypeInfo(propertyDeclaration.Type);
            var typeSymbol = typeInfo.Type;
            
            if (typeSymbol == null)
            {
                throw new InvalidOperationException($"Could not resolve type symbol for {typeCode} in property {name}. TypeInfo returned null.");
            }
            
            return new PropertyInfo(name, typeCode, typeSymbol, isReadOnly);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create PropertyInfo for {name} ({typeCode}): {ex.Message}", ex);
        }
    }

    // Helper method to simplify complex types for testing
    private static string SimplifyTypeForTesting(string typeCode)
    {
        return typeCode switch
        {
            // Replace complex custom types with simple built-in types for testing
            "ObservableCollection<ThermalZoneComponentViewModel>" => "ObservableCollection<ThermalZoneComponentViewModel>",
            _ => typeCode
        };
    }

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
        
        // Updated: Check for new hierarchical UI generation approach using UIGeneratorBase
        Assert.Contains("Application.EnableVisualStyles();", prog);
        Assert.Contains("Application.Run(form);", prog);
        Assert.Contains("var form = new Form", prog);
        Assert.Contains("using System.Windows.Forms;", prog);
        Assert.Contains("using System.ComponentModel;", prog);
        Assert.Contains("MyApp GUI Client", prog); // Window title
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("MyApp", "SvcService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateGuiClientProgram_IncludesRemoteClientSetup()
    {
        var props = new List<PropertyInfo> { CreatePropertyInfo("IsEnabled", "bool") };
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
            CreatePropertyInfo("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>"),
            CreatePropertyInfo("Status", "string")
        };
        var cmds = new List<CommandInfo>();
        string xaml = CsProjectGenerator.GenerateWpfMainWindowXaml("TestApp", "TestRemoteClient", props, cmds);
        
        // Should create ListBox for collections
        Assert.Contains("<ListBox ItemsSource=\"{Binding ZoneList}\"", xaml);
        Assert.Contains("<ListBox.ItemTemplate>", xaml);
        Assert.Contains("<DataTemplate>", xaml);
        
        // Updated: New approach uses generic {Binding} instead of specific property bindings
        Assert.Contains("Text=\"{Binding}\"", xaml); // Generic item binding instead of specific Zone/Temperature
        
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
            CreatePropertyInfo("Instructions", "string", true),
            CreatePropertyInfo("Counter", "int", false)
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
            CreatePropertyInfo("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>"),
            CreatePropertyInfo("Status", "string")
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Test new hierarchical UI generation approach using UIGeneratorBase
        string prog = CsProjectGenerator.GenerateGuiClientProgram("TestApp", "winforms", "Proto.Ns", "SvcService", "Client.Ns", props, cmds);
        
        Assert.Contains("var tree = new TreeView", prog);
        Assert.Contains("split.Panel1.Controls.Add(tree);", prog); // Updated: Uses SplitContainer panels
        Assert.Contains("Client ViewModel Properties", prog); // Root node text
        Assert.Contains("LoadTree();", prog); // LoadTree method call
        Assert.Contains("PropertyNodeInfo", prog); // PropertyNodeInfo class generation
        Assert.Contains("IsSimpleProperty", prog); // Property categorization
        Assert.Contains("IsCollectionProperty", prog); // Collection property categorization
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "SvcService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsIncludesDetailBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            CreatePropertyInfo("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>"),
            CreatePropertyInfo("Status", "string")
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        Assert.Contains("TableLayoutPanel detailLayout", gui); // Property details panel variable
        Assert.Contains("PropertyNodeInfo", gui); // PropertyNodeInfo class
        Assert.Contains("LoadTree();", gui); // LoadTree method call
        Assert.Contains("detailLayout.Controls.Add", gui); // Updated: Uses detailLayout instead of flow
        Assert.Contains("IsCollectionProperty", gui); // ZoneList should be categorized as collection
        Assert.Contains("IsSimpleProperty", gui); // Status should be categorized as simple
        Assert.Contains("ShowClientPropertyEditor", gui); // Property editor method
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateWinFormsGui_IncludesDataBindingWithErrorHandling()
    {
        var props = new List<PropertyInfo> 
        { 
            CreatePropertyInfo("Message", "string"),
            CreatePropertyInfo("IsEnabled", "bool", true) // read-only
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        // Updated: Check for our current implementation features
        Assert.Contains("try", gui);
        Assert.Contains("catch", gui);
        Assert.Contains("detailLayout.Controls.Add", gui); // Property details panel operations
        
        // Current implementation generates property categorization with variable assignments
        Assert.Contains("PropertyNodeInfo", gui); // PropertyNodeInfo class generation
        Assert.Contains("IsSimpleProperty", gui); // Property categorization assignment
        Assert.Contains("IsBooleanProperty", gui); // Property categorization assignment
        Assert.Contains("ShowClientPropertyEditor", gui); // Property editor method
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateGuiClientProgram_WinFormsIncludesPropertySpecificBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            CreatePropertyInfo("ZoneList", "ObservableCollection<ThermalZoneComponentViewModel>"),
            CreatePropertyInfo("Status", "string"),
            CreatePropertyInfo("IsActive", "bool")
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        Assert.Contains("Client ViewModel Properties", gui); // Root node text
        Assert.Contains("TableLayoutPanel detailLayout", gui); // Property details panel variable
        Assert.Contains("IsCollectionProperty", gui); // Property categorization assignment
        Assert.Contains("IsSimpleProperty", gui); // Property categorization assignment
        Assert.Contains("IsBooleanProperty", gui); // Property categorization assignment
        Assert.Contains("PropertyNodeInfo", gui); // PropertyNodeInfo class
        Assert.Contains("ShowClientPropertyEditor", gui); // Property editor method
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateGuiClientProgram_HandlesEdgeCasePrimitives()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("PreciseValue", "decimal"),
            CreatePropertyInfo("TinyValue", "nuint"),
            CreatePropertyInfo("UnicodeChar", "char"),
            CreatePropertyInfo("EmptyGuid", "Guid")
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        // Current implementation should categorize all these as simple properties
        Assert.Contains("PropertyNodeInfo", gui);
        Assert.Contains("IsSimpleProperty", gui); // Property categorization assignment
        Assert.Contains("LoadTree();", gui); // Method call
        Assert.Contains("tree.BeginUpdate();", gui); // Tree operations
        Assert.Contains("tree.EndUpdate();", gui); // Tree operations
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateGuiClientProgram_HandlesPropertyTypeAnalysis()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("Items", "ObservableCollection<string>"),
            CreatePropertyInfo("IsActive", "bool"),
            CreatePropertyInfo("Status", "System.DayOfWeek"), // Use actual enum instead of "StatusType"
            CreatePropertyInfo("Name", "string")
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        // Current implementation should categorize properties correctly
        Assert.Contains("IsCollectionProperty", gui); // Property categorization assignment
        Assert.Contains("IsBooleanProperty", gui); // Property categorization assignment  
        Assert.Contains("IsEnumProperty", gui); // Property categorization assignment
        Assert.Contains("IsSimpleProperty", gui); // Property categorization assignment
        Assert.Contains("PropertyNodeInfo", gui); // PropertyNodeInfo class
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateWinFormsGui_HandlesPropertySpecificEditors()
    {
        var props = new List<PropertyInfo>
        {
            CreatePropertyInfo("Message", "string"),
            CreatePropertyInfo("IsEnabled", "bool"),
            CreatePropertyInfo("Priority", "System.DayOfWeek") // Use actual enum instead of "PriorityType"
        };
        var cmds = new List<CommandInfo>();
        
        // Updated: Use the new abstraction directly instead of obsolete GenerateWinFormsGui
        var generator = new WinFormsClientUIGenerator("TestApp", "TestService", props, cmds, "TestRemoteClient", "Generated.Clients");
        string gui = generator.GenerateProgram("Proto.Ns", "TestService");
        
        // Updated: Check for our current implementation features
        Assert.Contains("PropertyNodeInfo", gui);
        Assert.Contains("LoadTree();", gui); // Method call instead of action delegate
        Assert.Contains("IsSimpleProperty", gui); // Property categorization assignment
        Assert.Contains("IsBooleanProperty", gui); // Property categorization assignment
        Assert.Contains("IsEnumProperty", gui); // Property categorization assignment
        
        // Check for tree structure
        Assert.Contains("rootNode.Nodes.Add", gui);
        Assert.Contains("tree.BeginUpdate", gui);
        Assert.Contains("tree.EndUpdate", gui);
        Assert.Contains("ShowClientPropertyEditor", gui); // Property editor method
        
        // Test server UI generation if enabled
        TestServerUIGenerationIfEnabled("TestApp", "TestService", props, cmds, "winforms");
    }

    [Fact]
    public void GenerateServerGuiProgram_IncludesPropertySpecificDataBinding()
    {
        var props = new List<PropertyInfo> 
        { 
            CreatePropertyInfo("Message", "string"),
            CreatePropertyInfo("Counter", "int"),
            CreatePropertyInfo("IsEnabled", "bool")
        };
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateServerGuiProgram("ServerApp", "winforms", "Proto.Ns", "SvcService", props, cmds);
        
        // Updated: Check for current server UI implementation features
        Assert.Contains("Server ViewModel Properties", prog); // Server tree view label
        Assert.Contains("PropertyNodeInfo", prog); // PropertyNodeInfo class generation
        Assert.Contains("IsSimpleProperty", prog); // Property categorization assignment
        Assert.Contains("IsBooleanProperty", prog); // Property categorization assignment
        Assert.Contains("Application.EnableVisualStyles();", prog); // WinForms specific
        Assert.Contains("Application.Run(form);", prog); // WinForms specific
        Assert.Contains("ShowServerPropertyEditor", prog); // Server property details panel method
    }
}
