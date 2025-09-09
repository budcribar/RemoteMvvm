using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Utility class for discovering properties at generation time and generating appropriate UI code
/// based on the actual properties found in the view model
/// </summary>
public static class PropertyDiscoveryUtility
{
    /// <summary>
    /// Analyzes a list of properties and categorizes them for UI generation
    /// </summary>
    public static PropertyAnalysis AnalyzeProperties(List<PropertyInfo> props)
    {
        var analysis = new PropertyAnalysis();
        
        foreach (var prop in props)
        {
            if (IsMemoryType(prop.TypeString))
            {
                // Memory<T> and similar types should be treated as simple properties for UI purposes
                analysis.SimpleProperties.Add(prop);
            }
            else if (IsCollectionType(prop.TypeString))
            {
                analysis.CollectionProperties.Add(prop);
            }
            else if (IsBooleanType(prop.TypeString))
            {
                analysis.BooleanProperties.Add(prop);
            }
            else if (IsEnumType(prop.TypeString))
            {
                analysis.EnumProperties.Add(prop);
            }
            else if (IsComplexType(prop.TypeString))
            {
                analysis.ComplexProperties.Add(prop);
            }
            else
            {
                analysis.SimpleProperties.Add(prop);
            }
        }
        
        return analysis;
    }

    /// <summary>
    /// Generates a complete dynamic TreeView setup based on discovered properties
    /// </summary>
    public static string GenerateTreeViewForProperties(List<PropertyInfo> props, string viewModelVarName = "vm", string context = "")
    {
        var analysis = AnalyzeProperties(props);
        var rootNodeText = string.IsNullOrEmpty(context) ? "ViewModel Properties" : $"{context} ViewModel Properties";
        
        var sb = new StringBuilder();
        sb.AppendLine("                var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };");
        sb.AppendLine("                split.Panel1.Controls.Add(tree);");
        sb.AppendLine();
        
        // Generate tree control buttons
        sb.Append(GenerateTreeControlButtons());
        sb.AppendLine();
        
        // Generate tree loading method based on actual properties
        sb.Append(GeneratePropertySpecificTreeLoader(analysis, viewModelVarName, rootNodeText));
        sb.AppendLine();
        
        // Generate property change monitoring - but make it safe for insertion inside try blocks
        sb.Append(GeneratePropertyChangeMonitoring(viewModelVarName));
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates a tree loading method tailored to the specific properties found
    /// </summary>
    private static string GeneratePropertySpecificTreeLoader(PropertyAnalysis analysis, string viewModelVarName, string rootNodeText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Property-specific tree building function");
        sb.AppendLine("                void LoadTree()");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.BeginUpdate();");
        sb.AppendLine("                        tree.Nodes.Clear();");
        sb.AppendLine("                        ");
        
        // Only create root node if we have properties to display
        var hasAnyProperties = analysis.SimpleProperties.Any() || 
                              analysis.BooleanProperties.Any() ||
                              analysis.CollectionProperties.Any() ||
                              analysis.ComplexProperties.Any() || 
                              analysis.EnumProperties.Any();
        
        if (hasAnyProperties)
        {
            sb.AppendLine($"                        var rootNode = new TreeNode(\"{rootNodeText}\");");
            sb.AppendLine("                        tree.Nodes.Add(rootNode);");
            sb.AppendLine("                        ");
        }
        
        // Add nodes for each property category - only if they have properties
        if (analysis.SimpleProperties.Any())
        {
            sb.AppendLine("                        // Simple properties");
            var simpleNode = "simplePropsNode";
            sb.AppendLine($"                        var {simpleNode} = new TreeNode(\"Simple Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine($"                        rootNode.Nodes.Add({simpleNode});");
            }
            else
            {
                sb.AppendLine($"                        tree.Nodes.Add({simpleNode});");
            }
            
            foreach (var prop in analysis.SimpleProperties)
            {
                var safeVarName = MakeSafeVariableName(prop.Name.ToLower());
                var safePropAccess = MakeSafePropertyAccess(prop.Name);
                
                sb.AppendLine($"                        try");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}Value = {viewModelVarName}.{safePropAccess}?.ToString() ?? \"<null>\";");
                sb.AppendLine($"                            var {safeVarName}Node = new TreeNode(\"{prop.Name}: \" + {safeVarName}Value);");
                
                // Use raw string literal just for object initializer to avoid brace escaping issues
                var objectInit = $$"""new PropertyNodeInfo { PropertyName = "{{prop.Name}}", Object = {{viewModelVarName}}, IsSimpleProperty = true }""";
                sb.AppendLine($"                            {safeVarName}Node.Tag = {objectInit};");
                
                sb.AppendLine($"                            {simpleNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                        }");
                sb.AppendLine("                        catch (Exception ex)");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                            {simpleNode}.Nodes.Add({safeVarName}ErrorNode);");
                sb.AppendLine("                        }");
            }
        }
        
        if (analysis.BooleanProperties.Any())
        {
            sb.AppendLine("                        // Boolean properties");
            var boolNode = "boolPropsNode";
            sb.AppendLine($"                        var {boolNode} = new TreeNode(\"Boolean Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine($"                        rootNode.Nodes.Add({boolNode});");
            }
            else
            {
                sb.AppendLine($"                        tree.Nodes.Add({boolNode});");
            }
            
            foreach (var prop in analysis.BooleanProperties)
            {
                var safeVarName = MakeSafeVariableName(prop.Name.ToLower());
                var safePropAccess = MakeSafePropertyAccess(prop.Name);
                
                sb.AppendLine($"                        try");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}Node = new TreeNode(\"{prop.Name}: \" + {viewModelVarName}.{safePropAccess});");
                
                var objectInit = $$"""new PropertyNodeInfo { PropertyName = "{{prop.Name}}", Object = {{viewModelVarName}}, IsBooleanProperty = true }""";
                sb.AppendLine($"                            {safeVarName}Node.Tag = {objectInit};");
                
                sb.AppendLine($"                            {boolNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                        }");
                sb.AppendLine("                        catch (Exception ex)");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                            {boolNode}.Nodes.Add({safeVarName}ErrorNode);");
                sb.AppendLine("                        }");
            }
        }
        
        if (analysis.CollectionProperties.Any())
        {
            sb.AppendLine("                        // Collection properties");
            var collectionNode = "collectionPropsNode";
            sb.AppendLine($"                        var {collectionNode} = new TreeNode(\"Collections\");");
            if (hasAnyProperties)
            {
                sb.AppendLine($"                        rootNode.Nodes.Add({collectionNode});");
            }
            else
            {
                sb.AppendLine($"                        tree.Nodes.Add({collectionNode});");
            }
            
            foreach (var prop in analysis.CollectionProperties)
            {
                var safeVarName = MakeSafeVariableName(prop.Name.ToLower());
                var safePropAccess = MakeSafePropertyAccess(prop.Name);
                
                sb.AppendLine($"                        try");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            if ({viewModelVarName}.{safePropAccess} != null)");
                sb.AppendLine("                            {");
                
                // Use .Length for arrays, .Count for other collections
                var isArrayType = prop.TypeString.EndsWith("[]") || 
                                 prop.TypeString.EndsWith("Byte[]") || 
                                 prop.TypeString.Contains("[]");
                var countProperty = isArrayType ? "Length" : "Count";
                sb.AppendLine($"                                var {safeVarName}Node = new TreeNode(\"{prop.Name} [\" + {viewModelVarName}.{safePropAccess}.{countProperty} + \" items]\");");
                
                var objectInit = $$"""new PropertyNodeInfo { PropertyName = "{{prop.Name}}", Object = {{viewModelVarName}}, IsCollectionProperty = true }""";
                sb.AppendLine($"                                {safeVarName}Node.Tag = {objectInit};");
                
                sb.AppendLine($"                                {collectionNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                                ");
                sb.AppendLine($"                                // Add individual items");
                sb.AppendLine($"                                int idx = 0;");
                sb.AppendLine($"                                foreach (var item in {viewModelVarName}.{safePropAccess})");
                sb.AppendLine("                                {");
                sb.AppendLine("                                    var itemTypeName = item?.GetType().Name ?? \"null\";");
                sb.AppendLine("                                    var itemNode = new TreeNode(\"[\" + idx + \"] \" + itemTypeName);");
                
                var itemObjectInit = """new PropertyNodeInfo { PropertyName = "[" + idx + "]", Object = item, IsCollectionItem = true, CollectionIndex = idx }""";
                sb.AppendLine($"                                    itemNode.Tag = {itemObjectInit};");
                
                sb.AppendLine($"                                    {safeVarName}Node.Nodes.Add(itemNode);");
                sb.AppendLine("                                    idx++;");
                sb.AppendLine("                                }");
                sb.AppendLine("                            }");
                sb.AppendLine("                            else");
                sb.AppendLine("                            {");
                sb.AppendLine($"                                var {safeVarName}Node = new TreeNode(\"{prop.Name} [null]\");");
                sb.AppendLine($"                                {collectionNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                            }");
                sb.AppendLine("                        }");
                sb.AppendLine("                        catch (Exception ex)");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                            {collectionNode}.Nodes.Add({safeVarName}ErrorNode);");
                sb.AppendLine("                        }");
            }
        }
        
        if (analysis.ComplexProperties.Any())
        {
            sb.AppendLine("                        // Complex properties (nested objects)");
            var complexNode = "complexPropsNode";
            sb.AppendLine($"                        var {complexNode} = new TreeNode(\"Complex Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine($"                        rootNode.Nodes.Add({complexNode});");
            }
            else
            {
                sb.AppendLine($"                        tree.Nodes.Add({complexNode});");
            }
            
            foreach (var prop in analysis.ComplexProperties)
            {
                var safeVarName = MakeSafeVariableName(prop.Name.ToLower());
                var safePropAccess = MakeSafePropertyAccess(prop.Name);
                
                sb.AppendLine($"                        try");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            if ({viewModelVarName}.{safePropAccess} != null)");
                sb.AppendLine("                            {");
                sb.AppendLine($"                                var {safeVarName}TypeName = {viewModelVarName}.{safePropAccess}.GetType().Name;");
                sb.AppendLine($"                                var {safeVarName}Node = new TreeNode(\"{prop.Name} (\" + {safeVarName}TypeName + \")\");");
                
                var objectInit = $$"""new PropertyNodeInfo { PropertyName = "{{prop.Name}}", Object = {{viewModelVarName}}.{{safePropAccess}}, IsComplexProperty = true }""";
                sb.AppendLine($"                                {safeVarName}Node.Tag = {objectInit};");
                
                sb.AppendLine($"                                {complexNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                            }");
                sb.AppendLine("                            else");
                sb.AppendLine("                            {");
                sb.AppendLine($"                                var {safeVarName}Node = new TreeNode(\"{prop.Name} [null]\");");
                sb.AppendLine($"                                {complexNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                            }");
                sb.AppendLine("                        }");
                sb.AppendLine("                        catch (Exception ex)");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                            {complexNode}.Nodes.Add({safeVarName}ErrorNode);");
                sb.AppendLine("                        }");
            }
        }
        
        if (analysis.EnumProperties.Any())
        {
            sb.AppendLine("                        // Enum properties");
            var enumNode = "enumPropsNode";
            sb.AppendLine($"                        var {enumNode} = new TreeNode(\"Enum Properties\");");
            if (hasAnyProperties)
            {
                sb.AppendLine($"                        rootNode.Nodes.Add({enumNode});");
            }
            else
            {
                sb.AppendLine($"                        tree.Nodes.Add({enumNode});");
            }
            
            foreach (var prop in analysis.EnumProperties)
            {
                var safeVarName = MakeSafeVariableName(prop.Name.ToLower());
                var safePropAccess = MakeSafePropertyAccess(prop.Name);
                
                sb.AppendLine($"                        try");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}Node = new TreeNode(\"{prop.Name}: \" + {viewModelVarName}.{safePropAccess});");
                
                var objectInit = $$"""new PropertyNodeInfo { PropertyName = "{{prop.Name}}", Object = {{viewModelVarName}}, IsEnumProperty = true }""";
                sb.AppendLine($"                            {safeVarName}Node.Tag = {objectInit};");
                
                sb.AppendLine($"                            {enumNode}.Nodes.Add({safeVarName}Node);");
                sb.AppendLine("                        }");
                sb.AppendLine("                        catch (Exception ex)");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var {safeVarName}ErrorNode = new TreeNode(\"{prop.Name}: <error>\");");
                sb.AppendLine($"                            {enumNode}.Nodes.Add({safeVarName}ErrorNode);");
                sb.AppendLine("                        }");
            }
        }
        
        sb.AppendLine("                        ");
        
        // Only expand root node if it exists
        if (hasAnyProperties)
        {
            sb.AppendLine("                        rootNode.Expand();");
        }
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        // Handle any errors in tree loading");
        sb.AppendLine("                        tree.Nodes.Clear();");
        sb.AppendLine("                        tree.Nodes.Add(new TreeNode(\"Error loading properties: \" + ex.Message));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    finally");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tree.EndUpdate();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Property node information class");
        sb.AppendLine("                class PropertyNodeInfo");
        sb.AppendLine("                {");
        sb.AppendLine("                    public string PropertyName { get; set; } = string.Empty;");
        sb.AppendLine("                    public object? Object { get; set; }");
        sb.AppendLine("                    public bool IsSimpleProperty { get; set; }");
        sb.AppendLine("                    public bool IsBooleanProperty { get; set; }");
        sb.AppendLine("                    public bool IsEnumProperty { get; set; }");
        sb.AppendLine("                    public bool IsCollectionProperty { get; set; }");
        sb.AppendLine("                    public bool IsComplexProperty { get; set; }");
        sb.AppendLine("                    public bool IsCollectionItem { get; set; }");
        sb.AppendLine("                    public int CollectionIndex { get; set; }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Load initial tree");
        sb.AppendLine("                LoadTree();");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates property editor UI based on specific properties found
    /// </summary>
    public static string GeneratePropertyEditor(List<PropertyInfo> props, string context = "")
    {
        var analysis = AnalyzeProperties(props);
        var sb = new StringBuilder();
        
        sb.AppendLine("                var detailGroup = new GroupBox");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Text = \"Property Details ({context})\",");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    AutoSizeMode = AutoSizeMode.GrowAndShrink,");
        sb.AppendLine("                    Padding = new Padding(10),");
        sb.AppendLine("                    Width = 350");
        sb.AppendLine("                };");
        sb.AppendLine("                flow.Controls.Add(detailGroup);");
        sb.AppendLine();
        
        sb.AppendLine("                var detailLayout = new TableLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    ColumnCount = 2,");
        sb.AppendLine("                    AutoSize = true,");
        sb.AppendLine("                    Width = 320");
        sb.AppendLine("                };");
        sb.AppendLine("                detailGroup.Controls.Add(detailLayout);");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.AutoSize));");
        sb.AppendLine("                detailLayout.ColumnStyles.Add(new ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));");
        sb.AppendLine();
        
        // Generate specific property editors based on what was found
        sb.Append(GenerateSpecificPropertyEditors(analysis));
        
        return sb.ToString();
    }

    private static bool IsCollectionType(string typeString)
    {
        return typeString.Contains("ObservableCollection") || 
               typeString.Contains("List<") || 
               typeString.Contains("IEnumerable<") ||
               typeString.Contains("ICollection<") ||
               typeString.Contains("Dictionary<") ||
               typeString.Contains("IDictionary<") ||
               typeString.Contains("IList<") ||
               typeString.Contains("HashSet<") ||
               typeString.Contains("ISet<") ||
               typeString.Contains("Collection<") ||
               typeString.EndsWith("[]"); // Arrays
    }

    private static bool IsBooleanType(string typeString)
    {
        return typeString == "bool" || typeString == "bool?";
    }

    private static bool IsEnumType(string typeString)
    {
        // Enhanced enum detection
        // Check for common enum patterns and known enum types
        if (typeString.Contains("Enum") && typeString.Contains("."))
            return true;
            
        // Check for nullable enum
        if (typeString.EndsWith("?") && !typeString.StartsWith("bool") && 
            !typeString.StartsWith("int") && !typeString.StartsWith("string") &&
            !typeString.StartsWith("double") && !typeString.StartsWith("float") &&
            !typeString.StartsWith("decimal") && !typeString.StartsWith("DateTime"))
        {
            var baseType = typeString.TrimEnd('?');
            // Could be an enum if it's not a known primitive
            return !IsPrimitiveTypeName(baseType);
        }
        
        // Check for common enum naming patterns
        if (typeString.EndsWith("Type") || typeString.EndsWith("Kind") || 
            typeString.EndsWith("Status") || typeString.EndsWith("State") ||
            typeString.EndsWith("Mode") || typeString.EndsWith("Option"))
            return true;
            
        return false;
    }

    private static bool IsPrimitiveTypeName(string typeName)
    {
        return typeName == "string" || typeName == "int" || typeName == "long" ||
               typeName == "double" || typeName == "float" || typeName == "decimal" ||
               typeName == "bool" || typeName == "char" || typeName == "byte" ||
               typeName == "sbyte" || typeName == "short" || typeName == "ushort" ||
               typeName == "uint" || typeName == "ulong" || typeName == "nuint" || 
               typeName == "nint" || typeName == "DateTime" || typeName == "DateOnly" || 
               typeName == "TimeOnly" || typeName == "Guid" || typeName == "TimeSpan" ||
               typeName == "Half" || // .NET 5+ half-precision float
               typeName.EndsWith("[]") || // Arrays are primitive-like for our purposes
               IsMemoryType(typeName) || // Memory<T> types are primitive-like for our purposes
               (typeName.EndsWith("?") && IsPrimitiveTypeName(typeName.TrimEnd('?'))); // Nullable primitives
    }

    private static bool IsComplexType(string typeString)
    {
        // A type is complex if it's not primitive, not collection, not enum, and not boolean
        return !IsPrimitiveTypeName(typeString) && 
               !IsCollectionType(typeString) && 
               !IsBooleanType(typeString) && 
               !IsEnumType(typeString) &&
               (!typeString.EndsWith("?") || // Handle nullable complex types
               (typeString.EndsWith("?") && !IsPrimitiveTypeName(typeString.TrimEnd('?'))));
    }

    private static bool IsMemoryType(string typeString)
    {
        return typeString.Contains("Memory<") || 
               typeString.Contains("ReadOnlyMemory<") ||
               typeString.Contains("Span<") ||
               typeString.Contains("ReadOnlySpan<");
    }

    /// <summary>
    /// Generates property editors tailored to the specific properties found
    /// </summary>
    private static string GenerateSpecificPropertyEditors(PropertyAnalysis analysis)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("                PropertyNodeInfo? currentSelectedNode = null;");
        sb.AppendLine("                var propertyControls = new List<Control>();");
        sb.AppendLine();
        sb.AppendLine("                void ClearPropertyEditor()");
        sb.AppendLine("                {");
        sb.AppendLine("                    foreach (var control in propertyControls)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        detailLayout.Controls.Remove(control);");
        sb.AppendLine("                        control.Dispose();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    propertyControls.Clear();");
        sb.AppendLine("                    detailLayout.RowCount = 0;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                void ShowPropertyEditor(PropertyNodeInfo nodeInfo)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ClearPropertyEditor();");
        sb.AppendLine("                    currentSelectedNode = nodeInfo;");
        sb.AppendLine("                    ");
        sb.AppendLine("                    if (nodeInfo?.Object == null) return;");
        sb.AppendLine("                    ");
        sb.AppendLine("                    int row = 0;");
        sb.AppendLine("                    ");
        sb.AppendLine("                    // Show property name");
        sb.AppendLine("                    var nameLabel = new Label { Text = \"Property:\", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };");
        sb.AppendLine("                    var nameValue = new Label { Text = nodeInfo.PropertyName, AutoSize = true };");
        sb.AppendLine("                    detailLayout.Controls.Add(nameLabel, 0, row);");
        sb.AppendLine("                    detailLayout.Controls.Add(nameValue, 1, row);");
        sb.AppendLine("                    propertyControls.Add(nameLabel);");
        sb.AppendLine("                    propertyControls.Add(nameValue);");
        sb.AppendLine("                    row++;");
        sb.AppendLine("                    ");
        
        // Generate specific editors for each property type
        if (analysis.BooleanProperties.Any())
        {
            sb.AppendLine("                    // Boolean property editor");
            sb.AppendLine("                    if (nodeInfo.IsBooleanProperty)");
            sb.AppendLine("                    {");
            foreach (var prop in analysis.BooleanProperties)
            {
                var enabled = prop.IsReadOnly ? "false" : "true";
                
                sb.AppendLine($"                        if (nodeInfo.PropertyName == \"{prop.Name}\")");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var checkbox = new CheckBox {{ Text = \"Value\", Enabled = {enabled} }};");
                
                if (prop.IsReadOnly)
                {
                    sb.AppendLine($"                            checkbox.Checked = vm.{prop.Name};");
                }
                else
                {
                    sb.AppendLine("                            try");
                    sb.AppendLine("                            {");
                    sb.AppendLine($"                                checkbox.DataBindings.Add(\"Checked\", vm, \"{prop.Name}\", false, DataSourceUpdateMode.OnPropertyChanged);");
                    sb.AppendLine("                            }");
                    sb.AppendLine("                            catch { }");
                }
                
                sb.AppendLine("                            detailLayout.Controls.Add(new Label { Text = \"Value:\" }, 0, row);");
                sb.AppendLine("                            detailLayout.Controls.Add(checkbox, 1, row);");
                sb.AppendLine("                            propertyControls.Add(checkbox);");
                sb.AppendLine("                            row++;");
                sb.AppendLine("                        }");
            }
            sb.AppendLine("                    }");
        }
        
        if (analysis.SimpleProperties.Any())
        {
            sb.AppendLine("                    // Simple property editor");
            sb.AppendLine("                    if (nodeInfo.IsSimpleProperty)");
            sb.AppendLine("                    {");
            foreach (var prop in analysis.SimpleProperties)
            {
                var readOnly = prop.IsReadOnly ? "true" : "false";
                
                sb.AppendLine($"                        if (nodeInfo.PropertyName == \"{prop.Name}\")");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var textBox = new TextBox {{ Width = 150, ReadOnly = {readOnly} }};");
                
                if (prop.IsReadOnly)
                {
                    sb.AppendLine($"                            textBox.Text = vm.{prop.Name}?.ToString() ?? string.Empty;");
                }
                else
                {
                    sb.AppendLine("                            try");
                    sb.AppendLine("                            {");
                    sb.AppendLine($"                                textBox.DataBindings.Add(\"Text\", vm, \"{prop.Name}\", false, DataSourceUpdateMode.OnPropertyChanged);");
                    sb.AppendLine("                            }");
                    sb.AppendLine("                            catch { }");
                }
                
                sb.AppendLine("                            detailLayout.Controls.Add(new Label { Text = \"Value:\" }, 0, row);");
                sb.AppendLine("                            detailLayout.Controls.Add(textBox, 1, row);");
                sb.AppendLine("                            propertyControls.Add(textBox);");
                sb.AppendLine("                            row++;");
                sb.AppendLine("                        }");
            }
            sb.AppendLine("                    }");
        }
        
        if (analysis.EnumProperties.Any())
        {
            sb.AppendLine("                    // Enum property editor");
            sb.AppendLine("                    if (nodeInfo.IsEnumProperty)");
            sb.AppendLine("                    {");
            foreach (var prop in analysis.EnumProperties)
            {
                var enabled = prop.IsReadOnly ? "false" : "true";
                
                sb.AppendLine($"                        if (nodeInfo.PropertyName == \"{prop.Name}\")");
                sb.AppendLine("                        {");
                sb.AppendLine($"                            var comboBox = new ComboBox {{ Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = {enabled} }};");
                sb.AppendLine("                            try");
                sb.AppendLine("                            {");
                sb.AppendLine($"                                var enumType = vm.{prop.Name}.GetType();");
                sb.AppendLine("                                comboBox.DataSource = Enum.GetValues(enumType);");
                
                if (!prop.IsReadOnly)
                {
                    sb.AppendLine($"                                comboBox.DataBindings.Add(\"SelectedItem\", vm, \"{prop.Name}\", false, DataSourceUpdateMode.OnPropertyChanged);");
                }
                else
                {
                    sb.AppendLine($"                                comboBox.SelectedItem = vm.{prop.Name};");
                }
                
                sb.AppendLine("                            }");
                sb.AppendLine("                            catch { }");
                sb.AppendLine("                            detailLayout.Controls.Add(new Label { Text = \"Value:\" }, 0, row);");
                sb.AppendLine("                            detailLayout.Controls.Add(comboBox, 1, row);");
                sb.AppendLine("                            propertyControls.Add(comboBox);");
                sb.AppendLine("                            row++;");
                sb.AppendLine("                        }");
            }
            sb.AppendLine("                    }");
        }
        
        if (analysis.ComplexProperties.Any())
        {
            sb.AppendLine("                    // Complex property editor");
            sb.AppendLine("                    if (nodeInfo.IsComplexProperty)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        var infoLabel = new Label { Text = \"Complex Object\", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };");
            sb.AppendLine("                        detailLayout.Controls.Add(new Label { Text = \"Type:\" }, 0, row);");
            sb.AppendLine("                        detailLayout.Controls.Add(infoLabel, 1, row);");
            sb.AppendLine("                        propertyControls.Add(infoLabel);");
            sb.AppendLine("                        row++;");
            sb.AppendLine("                        ");
            sb.AppendLine("                        var expandLabel = new Label { Text = \"(Expand tree node to see properties)\", ForeColor = Color.Gray, AutoSize = true };");
            sb.AppendLine("                        detailLayout.Controls.Add(new Label { Text = \"\" }, 0, row);");
            sb.AppendLine("                        detailLayout.Controls.Add(expandLabel, 1, row);");
            sb.AppendLine("                        propertyControls.Add(expandLabel);");
            sb.AppendLine("                        row++;");
            sb.AppendLine("                    }");
        }
        
        sb.AppendLine("                    ");
        sb.AppendLine("                    detailLayout.RowCount = row;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                // Tree selection event");
        sb.AppendLine("                tree.AfterSelect += (_, e) =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (e.Node?.Tag is PropertyNodeInfo nodeInfo)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ShowPropertyEditor(nodeInfo);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ClearPropertyEditor();");
        sb.AppendLine("                    }");
        sb.AppendLine("                };");
        
        return sb.ToString();
    }

    private static string GenerateTreeControlButtons()
    {
        var sb = new StringBuilder();
        sb.AppendLine("                // Add tree view control buttons");
        sb.AppendLine("                var treeButtonsPanel = new FlowLayoutPanel");
        sb.AppendLine("                {");
        sb.AppendLine("                    Height = 35,");
        sb.AppendLine("                    FlowDirection = FlowDirection.LeftToRight,");
        sb.AppendLine("                    AutoSize = false,");
        sb.AppendLine("                    Dock = DockStyle.Bottom");
        sb.AppendLine("                };");
        sb.AppendLine("                split.Panel1.Controls.Add(treeButtonsPanel);");
        sb.AppendLine();
        sb.AppendLine("                var refreshBtn = new Button { Text = \"Refresh\", Width = 70, Height = 25 };");
        sb.AppendLine("                var expandBtn = new Button { Text = \"Expand All\", Width = 80, Height = 25 };");
        sb.AppendLine("                var collapseBtn = new Button { Text = \"Collapse\", Width = 70, Height = 25 };");
        sb.AppendLine("                ");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(refreshBtn);");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(expandBtn);");
        sb.AppendLine("                treeButtonsPanel.Controls.Add(collapseBtn);");
        sb.AppendLine();
        sb.AppendLine("                // Wire up events");
        sb.AppendLine("                refreshBtn.Click += (_, __) => LoadTree();");
        sb.AppendLine("                expandBtn.Click += (_, __) => tree.ExpandAll();");
        sb.AppendLine("                collapseBtn.Click += (_, __) => tree.CollapseAll();");
        return sb.ToString();
    }

    private static string GeneratePropertyChangeMonitoring(string viewModelVarName)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("                // Property change monitoring");
        sb.AppendLine($"                if ({viewModelVarName} is INotifyPropertyChanged inpc)");
        sb.AppendLine("                {");
        sb.AppendLine("                    inpc.PropertyChanged += (_, e) =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        try { LoadTree(); }");
        sb.AppendLine("                        catch { }");
        sb.AppendLine("                    };");
        sb.AppendLine("                }");
        return sb.ToString();
    }

    /// <summary>
    /// Makes a variable name safe by avoiding C# keywords and ensuring valid identifier format
    /// </summary>
    public static string MakeSafeVariableName(string name)
    {
        // Handle C# keywords
        var keywords = new HashSet<string>
        {
            "char", "int", "long", "bool", "byte", "short", "float", "double", "decimal",
            "string", "object", "class", "struct", "enum", "interface", "namespace",
            "using", "public", "private", "protected", "internal", "static", "readonly",
            "const", "void", "var", "new", "this", "base", "typeof", "sizeof", "null",
            "true", "false", "if", "else", "for", "while", "do", "switch", "case",
            "default", "break", "continue", "return", "throw", "try", "catch", "finally"
        };

        if (keywords.Contains(name.ToLower()))
        {
            return "@" + name; // Use verbatim identifier
        }

        // Ensure it starts with a letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            return "_" + name;
        }

        return name;
    }

    /// <summary>
    /// Makes a property access safe by ensuring valid identifier format
    /// </summary>
    private static string MakeSafePropertyAccess(string propertyName)
    {
        // For property access, we don't need the @ prefix, just ensure it's a valid identifier
        if (string.IsNullOrWhiteSpace(propertyName))
            return "UnknownProperty";
            
        // Remove any invalid characters and ensure it starts with a letter or underscore
        var cleaned = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (i == 0)
            {
                if (char.IsLetter(c) || c == '_')
                    cleaned.Append(c);
                else
                    cleaned.Append('_');
            }
            else
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    cleaned.Append(c);
            }
        }
        
        var result = cleaned.ToString();
        if (string.IsNullOrEmpty(result))
            return "UnknownProperty";
            
        return result;
    }
}

/// <summary>
/// Contains the analysis results of discovered properties
/// </summary>
public class PropertyAnalysis
{
    public List<PropertyInfo> SimpleProperties { get; } = new();
    public List<PropertyInfo> BooleanProperties { get; } = new();
    public List<PropertyInfo> EnumProperties { get; } = new();
    public List<PropertyInfo> CollectionProperties { get; } = new();
    public List<PropertyInfo> ComplexProperties { get; } = new(); // Classes with properties
}