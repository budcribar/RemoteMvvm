using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Base class for generating UI code across different UI frameworks (WPF, WinForms) and contexts (Server, Client)
/// Ensures consistent hierarchical property display and functionality
/// </summary>
public abstract class UIGeneratorBase
{
    protected readonly string ProjectName;
    protected readonly string ModelName;
    protected readonly List<PropertyInfo> Properties;
    protected readonly List<CommandInfo> Commands;
    protected readonly PropertyAnalysis Analysis;
    protected readonly string Context; // "Server" or "Client"

    protected UIGeneratorBase(string projectName, string modelName, List<PropertyInfo> properties, List<CommandInfo> commands, string context)
    {
        ProjectName = projectName;
        ModelName = modelName;
        Properties = properties;
        Commands = commands;
        Context = context;
        Analysis = PropertyDiscoveryUtility.AnalyzeProperties(properties);
    }

    /// <summary>
    /// Generates the complete UI program
    /// </summary>
    public abstract string GenerateProgram(string protoNs, string serviceName);

    /// <summary>
    /// Generates the tree view structure - framework-specific implementation
    /// </summary>
      protected abstract UIComponent GenerateTreeViewStructure();

    /// <summary>
    /// Generates the property details panel - framework-specific implementation
    /// </summary>
      protected abstract UIComponent GeneratePropertyDetailsPanel();

    /// <summary>
    /// Generates command buttons - framework-specific implementation
    /// </summary>
      protected abstract UIComponent GenerateCommandButtons();

    /// <summary>
    /// Generates property change monitoring code - framework-specific implementation
    /// </summary>
      protected abstract UIComponent GeneratePropertyChangeMonitoring();

    /// <summary>
    /// Gets the appropriate root node text based on context
    /// </summary>
    protected string GetRootNodeText()
    {
        return $"{Context} ViewModel Properties";
    }

    /// <summary>
    /// Gets the appropriate window/form title based on context
    /// </summary>
    protected string GetWindowTitle()
    {
        return Context == "Server" ? $"Server GUI - {ProjectName}" : $"{ProjectName} GUI Client";
    }

    /// <summary>
    /// Gets the appropriate status text based on context
    /// </summary>
    protected string GetStatusText()
    {
        return Context == "Server" ? "Server Status: Running" : "Ready";
    }

    /// <summary>
    /// Indents a multi-line code block with the provided indent while preserving line breaks.
    /// </summary>
    /// <param name="code">Code that should be indented.</param>
    /// <param name="indent">Indentation prefix applied to every non-empty line.</param>
    /// <returns>Indented code block.</returns>
    protected static string IndentCodeBlock(string code, string indent)
    {
        var normalized = code.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.Append(indent);
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates framework-agnostic tree loading logic that can be converted to specific frameworks
    /// </summary>
    protected string GenerateFrameworkAgnosticTreeLogic(string treeVariableName, string viewModelVariableName)
    {
        var treeCommands = new List<TreeCommand>();
        
        // Generate the loading function structure
        treeCommands.Add(new TreeCommand(TreeCommandType.BeginFunction, "LoadTree"));
        treeCommands.Add(new TreeCommand(TreeCommandType.TryBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.BeginUpdate, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.Clear, treeVariableName));
        
        // Only create root node if we have properties to display
        var hasAnyProperties = Analysis.SimpleProperties.Any() || 
                              Analysis.BooleanProperties.Any() || 
                              Analysis.CollectionProperties.Any() || 
                              Analysis.ComplexProperties.Any() || 
                              Analysis.EnumProperties.Any();
        
        if (hasAnyProperties)
        {
            treeCommands.Add(new TreeCommand(TreeCommandType.CreateNode, "rootNode", $"\"{GetRootNodeText()}\""));
            treeCommands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "rootNode"));
        }
        
        // Generate property categories using PropertyDiscoveryUtility analysis
        GenerateSimpleProperties(treeCommands, viewModelVariableName, hasAnyProperties, treeVariableName);
        GenerateBooleanProperties(treeCommands, viewModelVariableName, hasAnyProperties, treeVariableName);
        GenerateCollectionProperties(treeCommands, viewModelVariableName, hasAnyProperties, treeVariableName);
        GenerateComplexProperties(treeCommands, viewModelVariableName, hasAnyProperties, treeVariableName);
        GenerateEnumProperties(treeCommands, viewModelVariableName, hasAnyProperties, treeVariableName);
        
        if (hasAnyProperties)
        {
            treeCommands.Add(new TreeCommand(TreeCommandType.ExpandNode, "rootNode"));
        }
        
        // Error handling and cleanup
        treeCommands.Add(new TreeCommand(TreeCommandType.TryEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.Clear, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.CreateNode, "errorNode", "\"Error loading properties: \" + ex.Message"));
        treeCommands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "errorNode"));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.FinallyBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.EndUpdate, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.FinallyEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.EndFunction));
        
        return ConvertTreeCommandsToFrameworkCode(treeCommands);
    }

    /// <summary>
    /// Generate simple properties tree commands
    /// </summary>
    private void GenerateSimpleProperties(List<TreeCommand> commands, string viewModelVariableName, bool hasAnyProperties, string treeVariableName)
    {
        if (!Analysis.SimpleProperties.Any()) return;
        
        commands.Add(new TreeCommand(TreeCommandType.Comment, "Simple properties"));
        commands.Add(new TreeCommand(TreeCommandType.CreateNode, "simplePropsNode", "\"Simple Properties\""));
        
        if (hasAnyProperties)
        {
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "simplePropsNode"));
        }
        else
        {
            commands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "simplePropsNode"));
        }
        
        foreach (var prop in Analysis.SimpleProperties)
        {
            var metadata = Analysis.GetMetadata(prop);
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            
            // Generate value assignment with proper null handling
            if (metadata.RequiresNullCheck && !metadata.IsNonNullableValueType)
            {
                commands.Add(new TreeCommand(TreeCommandType.AssignValue, $"{metadata.SafeVariableName}Value", 
                    $"{viewModelVariableName}.{metadata.SafePropertyAccess}?.ToString() ?? \"<null>\""));
            }
            else
            {
                commands.Add(new TreeCommand(TreeCommandType.AssignValue, $"{metadata.SafeVariableName}Value", 
                    $"{viewModelVariableName}.{metadata.SafePropertyAccess}.ToString()"));
            }
            
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                $"\"{prop.Name}: \" + {metadata.SafeVariableName}Value"));
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, $"{metadata.SafeVariableName}Node",
                $"\"{prop.Name}\"", viewModelVariableName, $"IsSimpleProperty = true, PropertyPath = \"{prop.Name}\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "simplePropsNode", $"{metadata.SafeVariableName}Node"));
            
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}ErrorNode", $"\"{prop.Name}: <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "simplePropsNode", $"{metadata.SafeVariableName}ErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        }
    }

    /// <summary>
    /// Generate boolean properties tree commands
    /// </summary>
    private void GenerateBooleanProperties(List<TreeCommand> commands, string viewModelVariableName, bool hasAnyProperties, string treeVariableName)
    {
        if (!Analysis.BooleanProperties.Any()) return;
        
        commands.Add(new TreeCommand(TreeCommandType.Comment, "Boolean properties"));
        commands.Add(new TreeCommand(TreeCommandType.CreateNode, "boolPropsNode", "\"Boolean Properties\""));
        
        if (hasAnyProperties)
        {
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "boolPropsNode"));
        }
        else
        {
            commands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "boolPropsNode"));
        }
        
        foreach (var prop in Analysis.BooleanProperties)
        {
            var metadata = Analysis.GetMetadata(prop);
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            
            if (metadata.RequiresNullCheck && !metadata.IsNonNullableValueType)
            {
                commands.Add(new TreeCommand(TreeCommandType.AssignValue, $"{metadata.SafeVariableName}Value", 
                    $"{viewModelVariableName}.{metadata.SafePropertyAccess}?.ToString() ?? \"<null>\""));
                commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                    $"\"{prop.Name}: \" + {metadata.SafeVariableName}Value"));
            }
            else
            {
                commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                    $"\"{prop.Name}: \" + {viewModelVariableName}.{metadata.SafePropertyAccess}.ToString()"));
            }
            
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, $"{metadata.SafeVariableName}Node",
                $"\"{prop.Name}\"", viewModelVariableName, $"IsBooleanProperty = true, PropertyPath = \"{prop.Name}\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "boolPropsNode", $"{metadata.SafeVariableName}Node"));
            
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}ErrorNode", $"\"{prop.Name}: <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "boolPropsNode", $"{metadata.SafeVariableName}ErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        }
    }

    /// <summary>
    /// Generate collection properties tree commands
    /// </summary>
    private void GenerateCollectionProperties(List<TreeCommand> commands, string viewModelVariableName, bool hasAnyProperties, string treeVariableName)
    {
        if (!Analysis.CollectionProperties.Any()) return;
        
        commands.Add(new TreeCommand(TreeCommandType.Comment, "Collection properties"));
        commands.Add(new TreeCommand(TreeCommandType.CreateNode, "collectionPropsNode", "\"Collections\""));
        
        if (hasAnyProperties)
        {
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "collectionPropsNode"));
        }
        else
        {
            commands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "collectionPropsNode"));
        }
        
        foreach (var prop in Analysis.CollectionProperties)
        {
            var metadata = Analysis.GetMetadata(prop);
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            commands.Add(new TreeCommand(TreeCommandType.IfNotNull, $"{viewModelVariableName}.{metadata.SafePropertyAccess}"));
            
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                $"\"{prop.Name} [\" + {viewModelVariableName}.{metadata.SafePropertyAccess}.{metadata.CountProperty} + \" items]\""));
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, $"{metadata.SafeVariableName}Node",
                $"\"{prop.Name}\"", viewModelVariableName, $"IsCollectionProperty = true, PropertyPath = \"{prop.Name}\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "collectionPropsNode", $"{metadata.SafeVariableName}Node"));
            
            // Add individual items (limit to first 3 for performance)
            commands.Add(new TreeCommand(TreeCommandType.AssignValue, "idx", "0"));
            commands.Add(new TreeCommand(TreeCommandType.ForEach, "item", $"{viewModelVariableName}.{metadata.SafePropertyAccess}"));
            commands.Add(new TreeCommand(TreeCommandType.IfBreak, "idx >= 3", "Limit to first 3 items"));
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            commands.Add(new TreeCommand(TreeCommandType.AssignValue, "itemText", "item.ToString()"));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, "itemNode", "\"[\" + idx + \"] \" + itemText"));
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, "itemNode", "\"[\" + idx + \"]\"", "item", $"IsCollectionItem = true, CollectionIndex = idx, PropertyPath = \"{prop.Name}[\" + idx + \"]\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, $"{metadata.SafeVariableName}Node", "itemNode"));
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, "itemErrorNode", "\"[\" + idx + \"] <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, $"{metadata.SafeVariableName}Node", "itemErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
            commands.Add(new TreeCommand(TreeCommandType.Increment, "idx"));
            commands.Add(new TreeCommand(TreeCommandType.EndForEach));
            
            commands.Add(new TreeCommand(TreeCommandType.Else));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", $"\"{prop.Name} [null]\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "collectionPropsNode", $"{metadata.SafeVariableName}Node"));
            commands.Add(new TreeCommand(TreeCommandType.EndIf));
            
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}ErrorNode", $"\"{prop.Name}: <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "collectionPropsNode", $"{metadata.SafeVariableName}ErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        }
    }

    /// <summary>
    /// Generate complex properties tree commands
    /// </summary>
    private void GenerateComplexProperties(List<TreeCommand> commands, string viewModelVariableName, bool hasAnyProperties, string treeVariableName)
    {
        if (!Analysis.ComplexProperties.Any()) return;
        
        commands.Add(new TreeCommand(TreeCommandType.Comment, "Complex properties (nested objects)"));
        commands.Add(new TreeCommand(TreeCommandType.CreateNode, "complexPropsNode", "\"Complex Properties\""));
        
        if (hasAnyProperties)
        {
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "complexPropsNode"));
        }
        else
        {
            commands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "complexPropsNode"));
        }
        
        foreach (var prop in Analysis.ComplexProperties)
        {
            var metadata = Analysis.GetMetadata(prop);
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            commands.Add(new TreeCommand(TreeCommandType.IfNotNull, $"{viewModelVariableName}.{metadata.SafePropertyAccess}"));
            
            commands.Add(new TreeCommand(TreeCommandType.AssignValue, $"{metadata.SafeVariableName}TypeName", 
                $"{viewModelVariableName}.{metadata.SafePropertyAccess}.GetType().Name"));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                $"\"{prop.Name} (\" + {metadata.SafeVariableName}TypeName + \")\""));
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, $"{metadata.SafeVariableName}Node",
                $"\"{prop.Name}\"", $"{viewModelVariableName}.{metadata.SafePropertyAccess}", $"IsComplexProperty = true, PropertyPath = \"{prop.Name}\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "complexPropsNode", $"{metadata.SafeVariableName}Node"));
            
            commands.Add(new TreeCommand(TreeCommandType.Else));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", $"\"{prop.Name} [null]\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "complexPropsNode", $"{metadata.SafeVariableName}Node"));
            commands.Add(new TreeCommand(TreeCommandType.EndIf));
            
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}ErrorNode", $"\"{prop.Name}: <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "complexPropsNode", $"{metadata.SafeVariableName}ErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        }
    }

    /// <summary>
    /// Generate enum properties tree commands
    /// </summary>
    private void GenerateEnumProperties(List<TreeCommand> commands, string viewModelVariableName, bool hasAnyProperties, string treeVariableName)
    {
        if (!Analysis.EnumProperties.Any()) return;
        
        commands.Add(new TreeCommand(TreeCommandType.Comment, "Enum properties"));
        commands.Add(new TreeCommand(TreeCommandType.CreateNode, "enumPropsNode", "\"Enum Properties\""));
        
        if (hasAnyProperties)
        {
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "enumPropsNode"));
        }
        else
        {
            commands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "enumPropsNode"));
        }
        
        foreach (var prop in Analysis.EnumProperties)
        {
            var metadata = Analysis.GetMetadata(prop);
            commands.Add(new TreeCommand(TreeCommandType.TryBegin));
            
            if (metadata.RequiresNullCheck && !metadata.IsNonNullableValueType)
            {
                commands.Add(new TreeCommand(TreeCommandType.AssignValue, $"{metadata.SafeVariableName}Value", 
                    $"{viewModelVariableName}.{metadata.SafePropertyAccess}?.ToString() ?? \"<null>\""));
                commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                    $"\"{prop.Name}: \" + {metadata.SafeVariableName}Value"));
            }
            else
            {
                commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}Node", 
                    $"\"{prop.Name}: \" + {viewModelVariableName}.{metadata.SafePropertyAccess}.ToString()"));
            }
            
            commands.Add(new TreeCommand(TreeCommandType.SetNodeTag, $"{metadata.SafeVariableName}Node",
                $"\"{prop.Name}\"", viewModelVariableName, $"IsEnumProperty = true, PropertyPath = \"{prop.Name}\"") );
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "enumPropsNode", $"{metadata.SafeVariableName}Node"));
            
            commands.Add(new TreeCommand(TreeCommandType.TryEnd));
            commands.Add(new TreeCommand(TreeCommandType.CatchBegin));
            commands.Add(new TreeCommand(TreeCommandType.CreateNode, $"{metadata.SafeVariableName}ErrorNode", $"\"{prop.Name}: <error>\""));
            commands.Add(new TreeCommand(TreeCommandType.AddChildNode, "enumPropsNode", $"{metadata.SafeVariableName}ErrorNode"));
            commands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        }
    }

    /// <summary>
    /// Convert framework-agnostic tree commands to actual C# code
    /// This method should be overridden by specific frameworks
    /// </summary>
    protected abstract string ConvertTreeCommandsToFrameworkCode(List<TreeCommand> commands);

    // Legacy method for backward compatibility - now delegates to framework-agnostic version
    protected string GenerateTreeLoadingLogic(string treeVariableName, string viewModelVariableName)
    {
        return GenerateFrameworkAgnosticTreeLogic(treeVariableName, viewModelVariableName);
    }

    // Framework-specific tree operations - to be implemented by derived classes (for legacy support)
    protected abstract string GenerateTreeBeginUpdate(string treeVariableName);
    protected abstract string GenerateTreeEndUpdate(string treeVariableName);
    protected abstract string GenerateTreeClear(string treeVariableName);
    protected abstract string GenerateCreateTreeNode(string text);
    protected abstract string GenerateAddTreeNode(string treeVariableName, string nodeVariableName);
    protected abstract string GenerateAddChildTreeNode(string parentNodeVariableName, string childNodeVariableName);
    protected abstract string GenerateExpandTreeNode(string nodeVariableName);
    protected abstract string GenerateSetTreeNodeTag(string nodeVariableName, string propertyName, string objectReference, string additionalProperties);
    
    /// <summary>
    /// Generates the PropertyNodeInfo class definition - shared across frameworks
    /// </summary>
    protected string GeneratePropertyNodeInfoClass()
    {
        return """
        // Property node information class
        public class PropertyNodeInfo
        {
            public string PropertyName { get; set; } = string.Empty;
            public string PropertyPath { get; set; } = string.Empty;
            public object? Object { get; set; }
            public bool IsSimpleProperty { get; set; }
            public bool IsBooleanProperty { get; set; }
            public bool IsEnumProperty { get; set; }
            public bool IsCollectionProperty { get; set; }
            public bool IsComplexProperty { get; set; }
            public bool IsCollectionItem { get; set; }
            public int CollectionIndex { get; set; }
        }
        """;
    }

    /// <summary>
    /// Generates strongly typed hierarchical tree loading logic that mirrors the WPF editor layout
    /// without relying on runtime reflection.
    /// </summary>
    /// <param name="treeVariableName">Tree control variable name.</param>
    /// <param name="viewModelVariableName">Root view model variable name.</param>
    /// <param name="viewModelTypeName">Fully qualified view model type name.</param>
    /// <returns>C# code for the hierarchical tree loader.</returns>
    protected string GenerateHierarchicalTreeLogic(string treeVariableName, string viewModelVariableName, string viewModelTypeName)
    {
        var generator = new HierarchicalTreeGenerator(treeVariableName, viewModelVariableName, viewModelTypeName, GetRootNodeText(), Properties);
        return generator.Generate();
    }

    /// <summary>
    /// Generates reflection-based hierarchical tree loading logic (like WPF approach)
    /// </summary>
    protected string GenerateReflectionBasedTreeLogic(string treeVariableName, string viewModelVariableName)
    {
        var treeCommands = new List<TreeCommand>();
        
        // Generate the loading function structure with reflection-based hierarchy
        treeCommands.Add(new TreeCommand(TreeCommandType.BeginFunction, "LoadTree"));
        treeCommands.Add(new TreeCommand(TreeCommandType.TryBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.BeginUpdate, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.Clear, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.Comment, "Clear visited objects for cycle detection"));
        treeCommands.Add(new TreeCommand(TreeCommandType.AssignValue, "visitedObjects", "new HashSet<object>()"));
        
        // Create root node
        treeCommands.Add(new TreeCommand(TreeCommandType.CreateNode, "rootNode", $"\"{GetRootNodeText()}\""));
        treeCommands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "rootNode"));
        
        // Use reflection to discover properties dynamically
        treeCommands.Add(new TreeCommand(TreeCommandType.Comment, "Use reflection to discover properties dynamically"));
        treeCommands.Add(new TreeCommand(TreeCommandType.AssignValue, "properties", 
            $"{viewModelVariableName}.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToList()"));
        
        // Iterate through properties
        treeCommands.Add(new TreeCommand(TreeCommandType.ForEach, "prop", "properties"));
        treeCommands.Add(new TreeCommand(TreeCommandType.TryBegin));
        
        // Create property tree node using helper method
        treeCommands.Add(new TreeCommand(TreeCommandType.AssignValue, "propNode",
            $"CreatePropertyTreeNode(prop, {viewModelVariableName}, 0, visitedObjects, prop.Name)"));
        treeCommands.Add(new TreeCommand(TreeCommandType.IfNotNull, "propNode"));
        treeCommands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "propNode"));
        treeCommands.Add(new TreeCommand(TreeCommandType.EndIf));
        
        treeCommands.Add(new TreeCommand(TreeCommandType.TryEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.CreateNode, "errorNode", "prop.Name + \": <error>\""));
        treeCommands.Add(new TreeCommand(TreeCommandType.AddChildNode, "rootNode", "errorNode"));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        
        treeCommands.Add(new TreeCommand(TreeCommandType.EndForEach));
        
        treeCommands.Add(new TreeCommand(TreeCommandType.ExpandNode, "rootNode"));
        
        // Error handling and cleanup
        treeCommands.Add(new TreeCommand(TreeCommandType.TryEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.Clear, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.CreateNode, "errorNode", "\"Error loading properties: \" + ex.Message"));
        treeCommands.Add(new TreeCommand(TreeCommandType.AddToTree, treeVariableName, "errorNode"));
        treeCommands.Add(new TreeCommand(TreeCommandType.CatchEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.FinallyBegin));
        treeCommands.Add(new TreeCommand(TreeCommandType.EndUpdate, treeVariableName));
        treeCommands.Add(new TreeCommand(TreeCommandType.FinallyEnd));
        treeCommands.Add(new TreeCommand(TreeCommandType.EndFunction));
        
        return ConvertTreeCommandsToFrameworkCode(treeCommands);
    }
}

/// <summary>
/// Framework-agnostic tree command representation
/// </summary>
public class TreeCommand
{
    public TreeCommandType Type { get; }
    public string[] Parameters { get; }

    public TreeCommand(TreeCommandType type, params string[] parameters)
    {
        Type = type;
        Parameters = parameters;
    }
}

/// <summary>
/// Types of tree operations that can be performed
/// </summary>
public enum TreeCommandType
{
    // Structure
    BeginFunction,
    EndFunction,
    Comment,
    
    // Control flow
    TryBegin,
    TryEnd,
    CatchBegin,
    CatchEnd,
    FinallyBegin,
    FinallyEnd,
    IfNotNull,
    Else,
    EndIf,
    IfBreak,
    ForEach,
    EndForEach,
    
    // Tree operations
    BeginUpdate,
    EndUpdate,
    Clear,
    CreateNode,
    AddToTree,
    AddChildNode,
    ExpandNode,
    SetNodeTag,
    
    // Variable operations
    AssignValue,
    Increment
}