using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Generates strongly typed hierarchical tree loading logic based on Roslyn analysis data.
/// </summary>
internal sealed class HierarchicalTreeGenerator
{
    private const int MaxDepth = 6;

    private readonly string _treeVariableName;
    private readonly string _viewModelVariableName;
    private readonly string _viewModelTypeName;
    private readonly string _rootNodeText;
    private readonly IReadOnlyList<PropertyInfo> _rootProperties;

    private readonly Dictionary<string, TypeDescriptor> _descriptors = new(StringComparer.Ordinal);
    private readonly List<TypeDescriptor> _orderedDescriptors = new();

    public HierarchicalTreeGenerator(
        string treeVariableName,
        string viewModelVariableName,
        string viewModelTypeName,
        string rootNodeText,
        IReadOnlyList<PropertyInfo> rootProperties)
    {
        _treeVariableName = treeVariableName;
        _viewModelVariableName = viewModelVariableName;
        _viewModelTypeName = viewModelTypeName;
        _rootNodeText = rootNodeText;
        _rootProperties = rootProperties;
    }

    public string Generate()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"const int MaxDepth = {MaxDepth};");
        sb.AppendLine();

        var rootDescriptor = CreateRootDescriptor();

        AppendLoadTree(sb, rootDescriptor);
        sb.AppendLine();

        foreach (var descriptor in _orderedDescriptors)
        {
            AppendBuilderMethod(sb, descriptor);
            sb.AppendLine();
        }

        if (_orderedDescriptors.Count > 0)
        {
            sb.Length -= Environment.NewLine.Length;
        }

        return sb.ToString();
    }

    private TypeDescriptor CreateRootDescriptor()
    {
        var rootDescriptor = new TypeDescriptor(
            key: "__root__",
            typeName: _viewModelTypeName,
            typeSymbol: null,
            methodName: "BuildNodesFor_" + SanitizeIdentifier(_viewModelTypeName),
            isRoot: true);

        _descriptors[rootDescriptor.Key] = rootDescriptor;
        _orderedDescriptors.Add(rootDescriptor);

        foreach (var prop in _rootProperties)
        {
            if (prop.FullTypeSymbol == null)
            {
                continue;
            }

            var entry = CreatePropertyEntry(prop.Name, prop.FullTypeSymbol, !prop.IsReadOnly);
            rootDescriptor.Properties.Add(entry);
        }

        return rootDescriptor;
    }

    private void AppendLoadTree(StringBuilder sb, TypeDescriptor rootDescriptor)
    {
        sb.AppendLine("void LoadTree()");
        sb.AppendLine("{");
        sb.AppendLine("    try");
        sb.AppendLine("    {");
        sb.AppendLine($"        {_treeVariableName}.BeginUpdate();");
        sb.AppendLine($"        {_treeVariableName}.Nodes.Clear();");
        sb.AppendLine();
        sb.AppendLine("        var visited = new HashSet<object>();");
        sb.AppendLine($"        var rootNode = new TreeNode(\"{_rootNodeText}\")");
        sb.AppendLine("        {");
        sb.AppendLine("            Tag = new PropertyNodeInfo");
        sb.AppendLine("            {");
        sb.AppendLine("                PropertyName = string.Empty,");
        sb.AppendLine("                PropertyPath = string.Empty,");
        sb.AppendLine($"                Object = {_viewModelVariableName},");
        sb.AppendLine("                IsComplexProperty = true");
        sb.AppendLine("            }");
        sb.AppendLine("        };");
        sb.AppendLine($"        {_treeVariableName}.Nodes.Add(rootNode);");
        sb.AppendLine();
        sb.AppendLine($"        if ({_viewModelVariableName} != null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var rootRef = (object){_viewModelVariableName};");
        sb.AppendLine("            if (!rootRef.GetType().IsValueType)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!visited.Add(rootRef))");
        sb.AppendLine("                {");
        sb.AppendLine("                    rootNode.Nodes.Add(new TreeNode(\"[Circular Reference]\"));");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {rootDescriptor.MethodName}(rootNode, {_viewModelVariableName}, string.Empty, visited, 0);");
        sb.AppendLine("                    visited.Remove(rootRef);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                {rootDescriptor.MethodName}(rootNode, {_viewModelVariableName}, string.Empty, visited, 0);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    catch (Exception ex)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {_treeVariableName}.Nodes.Clear();");
        sb.AppendLine($"        {_treeVariableName}.Nodes.Add(new TreeNode(\"Error loading properties: \" + ex.Message));");
        sb.AppendLine("    }");
        sb.AppendLine("    finally");
        sb.AppendLine("    {");
        sb.AppendLine($"        {_treeVariableName}.EndUpdate();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private void AppendBuilderMethod(StringBuilder sb, TypeDescriptor descriptor)
    {
        sb.AppendLine($"void {descriptor.MethodName}(TreeNode parentNode, {descriptor.TypeName} owner, string parentPath, HashSet<object> visited, int depth)");
        sb.AppendLine("{");
        sb.AppendLine("    if (owner == null) return;");
        sb.AppendLine("    if (depth > MaxDepth) return;");

        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in descriptor.Properties)
        {
            AppendPropertyNode(sb, property, usedNames);
            sb.AppendLine();
        }

        if (descriptor.Properties.Count > 0)
        {
            sb.Length -= Environment.NewLine.Length;
        }

        sb.AppendLine("}");
    }

    private void AppendPropertyNode(StringBuilder sb, PropertyEntry property, HashSet<string> usedNames)
    {
        var pathVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Path");
        sb.AppendLine($"    var {pathVar} = string.IsNullOrEmpty(parentPath) ? \"{property.Name}\" : parentPath + \".{property.Name}\";");

        var valueVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Value");
        sb.AppendLine($"    var {valueVar} = owner.{property.Name};");

        if (property.IsCollection)
        {
            AppendCollectionNode(sb, property, pathVar, valueVar, usedNames);
            return;
        }

        if (property.IsComplex)
        {
            AppendComplexNode(sb, property, pathVar, valueVar, usedNames);
            return;
        }

        AppendSimpleNode(sb, property, pathVar, valueVar, usedNames);
    }

    private void AppendSimpleNode(StringBuilder sb, PropertyEntry property, string pathVar, string valueVar, HashSet<string> usedNames)
    {
        var textVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Text");
        sb.AppendLine($"    var {textVar} = GetDisplayValue({valueVar});");

        var nodeVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Node");
        sb.AppendLine($"    var {nodeVar} = new TreeNode(\"{property.Name}: \" + {textVar})");
        sb.AppendLine("    {");
        sb.AppendLine("        Tag = new PropertyNodeInfo");
        sb.AppendLine("        {");
        sb.AppendLine($"            PropertyName = \"{property.Name}\",");
        sb.AppendLine($"            PropertyPath = {pathVar},");
        sb.AppendLine("            Object = owner,");
        sb.AppendLine("            IsSimpleProperty = " + (property.IsBoolean || property.IsEnum ? "false" : "true") + ",");
        sb.AppendLine("            IsBooleanProperty = " + (property.IsBoolean ? "true" : "false") + ",");
        sb.AppendLine("            IsEnumProperty = " + (property.IsEnum ? "true" : "false"));
        sb.AppendLine("        }");
        sb.AppendLine("    };");
        sb.AppendLine($"    parentNode.Nodes.Add({nodeVar});");
    }

    private void AppendComplexNode(StringBuilder sb, PropertyEntry property, string pathVar, string valueVar, HashSet<string> usedNames)
    {
        var nodeVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Node");
        sb.AppendLine($"    TreeNode {nodeVar};");
        sb.AppendLine($"    if ({valueVar} == null)");
        sb.AppendLine("    {");
        sb.AppendLine($"        {nodeVar} = new TreeNode(\"{property.Name} [null]\")");
        sb.AppendLine("        {");
        sb.AppendLine("            Tag = new PropertyNodeInfo");
        sb.AppendLine("            {");
        sb.AppendLine($"                PropertyName = \"{property.Name}\",");
        sb.AppendLine($"                PropertyPath = {pathVar},");
        sb.AppendLine("                Object = owner,");
        sb.AppendLine("                IsComplexProperty = true");
        sb.AppendLine("            }");
        sb.AppendLine("        };");
        sb.AppendLine("        parentNode.Nodes.Add(" + nodeVar + ");");
        sb.AppendLine("    }");
        sb.AppendLine("    else");
        sb.AppendLine("    {");
        sb.AppendLine($"        {nodeVar} = new TreeNode(\"{property.Name} (\" + {valueVar}.GetType().Name + \")\")");
        sb.AppendLine("        {");
        sb.AppendLine("            Tag = new PropertyNodeInfo");
        sb.AppendLine("            {");
        sb.AppendLine($"                PropertyName = \"{property.Name}\",");
        sb.AppendLine($"                PropertyPath = {pathVar},");
        sb.AppendLine("                Object = owner,");
        sb.AppendLine("                IsComplexProperty = true");
        sb.AppendLine("            }");
        sb.AppendLine("        };");
        sb.AppendLine("        parentNode.Nodes.Add(" + nodeVar + ");");

        if (property.ComplexDescriptor != null)
        {
            sb.AppendLine("        if (depth < MaxDepth)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var complexRef = (object){valueVar};");
            sb.AppendLine("            if (!complexRef.GetType().IsValueType)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (!visited.Add(complexRef))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {nodeVar}.Nodes.Add(new TreeNode(\"[Circular Reference]\"));");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {property.ComplexDescriptor.MethodName}({nodeVar}, {valueVar}, {pathVar}, visited, depth + 1);");
            sb.AppendLine("                    visited.Remove(complexRef);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine($"                {property.ComplexDescriptor.MethodName}({nodeVar}, {valueVar}, {pathVar}, visited, depth + 1);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
    }

    private void AppendCollectionNode(StringBuilder sb, PropertyEntry property, string pathVar, string valueVar, HashSet<string> usedNames)
    {
        var countVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Count");
        sb.AppendLine($"    var {countVar} = GetCollectionCount({valueVar});");

        var textVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "NodeText");
        sb.AppendLine($"    var {textVar} = \"{property.Name} [\" + ({countVar}?.ToString() ?? \"?\") + \" items]\";");

        var nodeVar = MakeUniqueVariableName(usedNames, ToCamelCase(property.Name) + "Node");
        sb.AppendLine($"    var {nodeVar} = new TreeNode({textVar})");
        sb.AppendLine("    {");
        sb.AppendLine("        Tag = new PropertyNodeInfo");
        sb.AppendLine("        {");
        sb.AppendLine($"            PropertyName = \"{property.Name}\",");
        sb.AppendLine($"            PropertyPath = {pathVar},");
        sb.AppendLine("            Object = owner,");
        sb.AppendLine("            IsCollectionProperty = true");
        sb.AppendLine("        }");
        sb.AppendLine("    };");
        sb.AppendLine($"    parentNode.Nodes.Add({nodeVar});");
        sb.AppendLine();
        sb.AppendLine($"    if ({valueVar} != null)");
        sb.AppendLine("    {");
        sb.AppendLine("        int index = 0;");
        if (property.Dictionary != null)
        {
            AppendDictionaryIteration(sb, property, pathVar, valueVar, nodeVar, usedNames);
        }
        else
        {
            AppendEnumerableIteration(sb, property, pathVar, valueVar, nodeVar, usedNames);
        }
        sb.AppendLine("    }");
    }

    private void AppendEnumerableIteration(StringBuilder sb, PropertyEntry property, string pathVar, string valueVar, string nodeVar, HashSet<string> usedNames)
    {
        var itemVar = MakeUniqueVariableName(usedNames, "item");
        sb.AppendLine($"        foreach (var {itemVar} in {valueVar})");
        sb.AppendLine("        {");
        sb.AppendLine("            if (index >= 5) break;");
        sb.AppendLine($"            var itemPath = {pathVar} + \"[\" + index + \"]\";");
        sb.AppendLine("            TreeNode childNode;");
        sb.AppendLine($"            if ({itemVar} == null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                childNode = new TreeNode(\"[\" + index + \"] <null>\")");
        sb.AppendLine("                {");
        sb.AppendLine("                    Tag = new PropertyNodeInfo");
        sb.AppendLine("                    {");
        sb.AppendLine("                        PropertyName = \"[\" + index + \"]\",");
        sb.AppendLine("                        PropertyPath = itemPath,");
        sb.AppendLine("                        Object = null,");
        sb.AppendLine("                        IsCollectionItem = true,");
        sb.AppendLine("                        CollectionIndex = index");
        sb.AppendLine("                    }");
        sb.AppendLine("                };");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        if (property.Collection != null && property.Collection.ElementDescriptor != null)
        {
            var descriptor = property.Collection.ElementDescriptor;
            sb.AppendLine($"                childNode = new TreeNode(\"[\" + index + \"] (\" + {itemVar}.GetType().Name + \")\")");
            sb.AppendLine("                {");
            sb.AppendLine("                    Tag = new PropertyNodeInfo");
            sb.AppendLine("                    {");
            sb.AppendLine("                        PropertyName = \"[\" + index + \"]\",");
            sb.AppendLine("                        PropertyPath = itemPath,");
            sb.AppendLine($"                        Object = {itemVar},");
            sb.AppendLine("                        IsComplexProperty = true,");
            sb.AppendLine("                        IsCollectionItem = true,");
            sb.AppendLine("                        CollectionIndex = index");
            sb.AppendLine("                    }");
            sb.AppendLine("                };");
            sb.AppendLine($"                if (depth < MaxDepth)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var itemRef = (object){itemVar};");
            sb.AppendLine("                    if (!itemRef.GetType().IsValueType)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (!visited.Add(itemRef))");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            childNode.Nodes.Add(new TreeNode(\"[Circular Reference]\"));");
            sb.AppendLine("                        }");
            sb.AppendLine("                        else");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            {descriptor.MethodName}(childNode, {itemVar}, itemPath, visited, depth + 1);");
            sb.AppendLine("                            visited.Remove(itemRef);");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        {descriptor.MethodName}(childNode, {itemVar}, itemPath, visited, depth + 1);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
        }
        else
        {
            var textVar = MakeUniqueVariableName(usedNames, "itemText");
            sb.AppendLine($"                var {textVar} = GetDisplayValue({itemVar});");
            sb.AppendLine($"                childNode = new TreeNode(\"[\" + index + \"] \" + {textVar})");
            sb.AppendLine("                {");
            sb.AppendLine("                    Tag = new PropertyNodeInfo");
            sb.AppendLine("                    {");
            sb.AppendLine("                        PropertyName = \"[\" + index + \"]\",");
            sb.AppendLine("                        PropertyPath = itemPath,");
            sb.AppendLine($"                        Object = {itemVar},");
            sb.AppendLine("                        IsSimpleProperty = " + ((property.Collection?.ElementIsBoolean ?? false) || (property.Collection?.ElementIsEnum ?? false) ? "false" : "true") + ",");
            sb.AppendLine("                        IsBooleanProperty = " + (property.Collection?.ElementIsBoolean ?? false ? "true" : "false") + ",");
            sb.AppendLine("                        IsEnumProperty = " + (property.Collection?.ElementIsEnum ?? false ? "true" : "false") + ",");
            sb.AppendLine("                        IsCollectionItem = true,");
            sb.AppendLine("                        CollectionIndex = index");
            sb.AppendLine("                    }");
            sb.AppendLine("                };");
        }
        sb.AppendLine("            }");
        sb.AppendLine($"            {nodeVar}.Nodes.Add(childNode);");
        sb.AppendLine("            index++;");
        sb.AppendLine("        }");
    }

    private void AppendDictionaryIteration(StringBuilder sb, PropertyEntry property, string pathVar, string valueVar, string nodeVar, HashSet<string> usedNames)
    {
        var kvpVar = MakeUniqueVariableName(usedNames, "entry");
        sb.AppendLine($"        foreach (var {kvpVar} in {valueVar})");
        sb.AppendLine("        {");
        sb.AppendLine("            if (index >= 5) break;");
        sb.AppendLine($"            var keyText = {kvpVar}.Key?.ToString() ?? \"<null>\";");
        sb.AppendLine($"            var itemPath = {pathVar} + \"[\" + keyText + \"]\";");
        sb.AppendLine("            TreeNode childNode;");
        sb.AppendLine($"            if ({kvpVar}.Value == null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                childNode = new TreeNode(\"[\" + keyText + \"] <null>\")");
        sb.AppendLine("                {");
        sb.AppendLine("                    Tag = new PropertyNodeInfo");
        sb.AppendLine("                    {");
        sb.AppendLine("                        PropertyName = \"[\" + keyText + \"]\",");
        sb.AppendLine("                        PropertyPath = itemPath,");
        sb.AppendLine("                        Object = null,");
        sb.AppendLine("                        IsCollectionItem = true,");
        sb.AppendLine("                        CollectionIndex = index");
        sb.AppendLine("                    }");
        sb.AppendLine("                };");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        if (property.Dictionary != null && property.Dictionary.ValueDescriptor != null)
        {
            var descriptor = property.Dictionary.ValueDescriptor;
            sb.AppendLine($"                childNode = new TreeNode(\"[\" + keyText + \"] (\" + {kvpVar}.Value.GetType().Name + \")\")");
            sb.AppendLine("                {");
            sb.AppendLine("                    Tag = new PropertyNodeInfo");
            sb.AppendLine("                    {");
            sb.AppendLine("                        PropertyName = \"[\" + keyText + \"]\",");
            sb.AppendLine("                        PropertyPath = itemPath,");
            sb.AppendLine($"                        Object = {kvpVar}.Value,");
            sb.AppendLine("                        IsComplexProperty = true,");
            sb.AppendLine("                        IsCollectionItem = true,");
            sb.AppendLine("                        CollectionIndex = index");
            sb.AppendLine("                    }");
            sb.AppendLine("                };");
            sb.AppendLine("                if (depth < MaxDepth)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var valueRef = (object){kvpVar}.Value;");
            sb.AppendLine("                    if (!valueRef.GetType().IsValueType)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (!visited.Add(valueRef))");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            childNode.Nodes.Add(new TreeNode(\"[Circular Reference]\"));");
            sb.AppendLine("                        }");
            sb.AppendLine("                        else");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            {descriptor.MethodName}(childNode, {kvpVar}.Value, itemPath, visited, depth + 1);");
            sb.AppendLine("                            visited.Remove(valueRef);");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        {descriptor.MethodName}(childNode, {kvpVar}.Value, itemPath, visited, depth + 1);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
        }
        else
        {
            var textVar = MakeUniqueVariableName(usedNames, "valueText");
            sb.AppendLine($"                var {textVar} = GetDisplayValue({kvpVar}.Value);");
            sb.AppendLine($"                childNode = new TreeNode(\"[\" + keyText + \"] \" + {textVar})");
            sb.AppendLine("                {");
            sb.AppendLine("                    Tag = new PropertyNodeInfo");
            sb.AppendLine("                    {");
            sb.AppendLine("                        PropertyName = \"[\" + keyText + \"]\",");
            sb.AppendLine("                        PropertyPath = itemPath,");
            sb.AppendLine($"                        Object = {kvpVar}.Value,");
            sb.AppendLine("                        IsSimpleProperty = " + ((property.Dictionary?.ValueIsBoolean ?? false) || (property.Dictionary?.ValueIsEnum ?? false) ? "false" : "true") + ",");
            sb.AppendLine("                        IsBooleanProperty = " + (property.Dictionary?.ValueIsBoolean ?? false ? "true" : "false") + ",");
            sb.AppendLine("                        IsEnumProperty = " + (property.Dictionary?.ValueIsEnum ?? false ? "true" : "false") + ",");
            sb.AppendLine("                        IsCollectionItem = true,");
            sb.AppendLine("                        CollectionIndex = index");
            sb.AppendLine("                    }");
            sb.AppendLine("                };");
        }
        sb.AppendLine("            }");
        sb.AppendLine($"            {nodeVar}.Nodes.Add(childNode);");
        sb.AppendLine("            index++;");
        sb.AppendLine("        }");
    }

    private PropertyEntry CreatePropertyEntry(string name, ITypeSymbol typeSymbol, bool hasSetter)
    {
        var entry = new PropertyEntry
        {
            Name = name,
            TypeSymbol = typeSymbol,
            IsWritable = hasSetter
        };

        var underlying = UnwrapNullable(typeSymbol);
        entry.IsBoolean = IsBoolean(underlying);
        entry.IsEnum = underlying.TypeKind == TypeKind.Enum;
        entry.IsValueType = underlying.IsValueType;

        if (entry.IsBoolean || entry.IsEnum)
        {
            entry.IsSimple = false;
        }
        else if (IsMemoryLike(typeSymbol))
        {
            entry.IsSimple = true;
        }
        else if (IsSimple(underlying))
        {
            entry.IsSimple = true;
        }

        if (!entry.IsSimple && !entry.IsBoolean && !entry.IsEnum)
        {
            if (GeneratorHelpers.TryGetDictionaryTypeArgs(typeSymbol, out var keyType, out var valueType) && keyType != null && valueType != null)
            {
                entry.IsCollection = true;
                entry.IsDictionary = true;
                entry.Dictionary = CreateDictionaryInfo(keyType, valueType);
            }
            else if (GeneratorHelpers.TryGetEnumerableElementType(typeSymbol, out var elementType) && elementType != null)
            {
                entry.IsCollection = true;
                entry.Collection = CreateCollectionInfo(elementType);
            }
            else if (IsComplex(underlying))
            {
                entry.IsComplex = true;
                entry.ComplexDescriptor = EnsureDescriptorFor(underlying);
            }
            else
            {
                entry.IsSimple = true;
            }
        }

        return entry;
    }

    private DictionaryInfo? CreateDictionaryInfo(ITypeSymbol keyType, ITypeSymbol valueType)
    {
        var info = new DictionaryInfo
        {
            KeyType = keyType,
            KeyIsValueType = UnwrapNullable(keyType).IsValueType,
            ValueType = valueType,
            ValueIsBoolean = IsBoolean(valueType),
            ValueIsEnum = UnwrapNullable(valueType).TypeKind == TypeKind.Enum,
            ValueIsSimple = IsSimple(UnwrapNullable(valueType)) && !IsBoolean(valueType) && UnwrapNullable(valueType).TypeKind != TypeKind.Enum,
            ValueIsValueType = UnwrapNullable(valueType).IsValueType
        };

        if (!info.ValueIsSimple && !info.ValueIsBoolean && !info.ValueIsEnum)
        {
            info.ValueDescriptor = EnsureDescriptorFor(UnwrapNullable(valueType));
        }

        return info;
    }

    private CollectionInfo? CreateCollectionInfo(ITypeSymbol elementType)
    {
        var underlying = UnwrapNullable(elementType);
        var info = new CollectionInfo
        {
            ElementType = underlying,
            ElementIsBoolean = IsBoolean(underlying),
            ElementIsEnum = underlying.TypeKind == TypeKind.Enum,
            ElementIsSimple = IsSimple(underlying) && !IsBoolean(underlying) && underlying.TypeKind != TypeKind.Enum,
            ElementIsValueType = underlying.IsValueType
        };

        if (!info.ElementIsSimple && !info.ElementIsBoolean && !info.ElementIsEnum)
        {
            info.ElementDescriptor = EnsureDescriptorFor(underlying);
        }

        return info;
    }

    private TypeDescriptor EnsureDescriptorFor(ITypeSymbol typeSymbol)
    {
        var normalized = UnwrapNullable(typeSymbol);
        var key = normalized.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (_descriptors.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var typeName = normalized.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        var descriptor = new TypeDescriptor(
            key,
            typeName,
            normalized,
            "BuildNodesFor_" + SanitizeIdentifier(typeName),
            isRoot: false);

        _descriptors[key] = descriptor;
        _orderedDescriptors.Add(descriptor);

        foreach (var prop in normalized.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.IsStatic) continue;
            if (prop.GetMethod == null) continue;
            if (prop.Parameters.Length > 0) continue;

            var entry = CreatePropertyEntry(prop.Name, prop.Type, prop.SetMethod != null && prop.SetMethod.DeclaredAccessibility == Accessibility.Public);
            descriptor.Properties.Add(entry);
        }

        return descriptor;
    }

    private static string MakeUniqueVariableName(HashSet<string> used, string baseName)
    {
        var name = ToCamelCase(baseName);
        if (!used.Add(name))
        {
            var index = 1;
            while (!used.Add(name + index))
            {
                index++;
            }
            name += index;
        }
        return name;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        if (char.IsLower(value[0]))
        {
            return value;
        }
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string SanitizeIdentifier(string typeName)
    {
        var builder = new StringBuilder(typeName.Length);
        foreach (var ch in typeName.Replace("global::", string.Empty))
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }
        var result = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "Type" : result;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return named.TypeArguments[0];
        }
        return type;
    }

    private static bool IsBoolean(ITypeSymbol type)
    {
        var underlying = UnwrapNullable(type);
        return underlying.SpecialType == SpecialType.System_Boolean;
    }

    private static bool IsSimple(ITypeSymbol type)
    {
        var underlying = UnwrapNullable(type);
        if (underlying.SpecialType != SpecialType.None)
        {
            switch (underlying.SpecialType)
            {
                case SpecialType.System_String:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                    return true;
            }
        }

        var fullName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName is "global::System.Guid"
            or "global::System.DateTime"
            or "global::System.DateTimeOffset"
            or "global::System.TimeSpan";
    }

    private static bool IsMemoryLike(ITypeSymbol type)
    {
        return GeneratorHelpers.TryGetMemoryElementType(type, out _);
    }

    private static bool IsComplex(ITypeSymbol type)
    {
        var underlying = UnwrapNullable(type);
        if (underlying.SpecialType == SpecialType.System_String)
        {
            return false;
        }
        return underlying.TypeKind == TypeKind.Class || underlying.TypeKind == TypeKind.Struct;
    }

    private sealed class TypeDescriptor
    {
        public TypeDescriptor(string key, string typeName, ITypeSymbol? typeSymbol, string methodName, bool isRoot)
        {
            Key = key;
            TypeName = typeName;
            TypeSymbol = typeSymbol;
            MethodName = methodName;
            IsRoot = isRoot;
        }

        public string Key { get; }
        public string TypeName { get; }
        public ITypeSymbol? TypeSymbol { get; }
        public string MethodName { get; }
        public bool IsRoot { get; }
        public List<PropertyEntry> Properties { get; } = new();
    }

    private sealed class PropertyEntry
    {
        public string Name { get; init; } = string.Empty;
        public ITypeSymbol TypeSymbol { get; init; } = default!;
        public bool IsWritable { get; init; }
        public bool IsCollection { get; set; }
        public bool IsDictionary { get; set; }
        public bool IsComplex { get; set; }
        public bool IsSimple { get; set; }
        public bool IsBoolean { get; set; }
        public bool IsEnum { get; set; }
        public bool IsValueType { get; set; }
        public TypeDescriptor? ComplexDescriptor { get; set; }
        public CollectionInfo? Collection { get; set; }
        public DictionaryInfo? Dictionary { get; set; }
    }

    private sealed class CollectionInfo
    {
        public ITypeSymbol ElementType { get; init; } = default!;
        public bool ElementIsSimple { get; init; }
        public bool ElementIsBoolean { get; init; }
        public bool ElementIsEnum { get; init; }
        public bool ElementIsValueType { get; init; }
        public TypeDescriptor? ElementDescriptor { get; set; }
    }

    private sealed class DictionaryInfo
    {
        public ITypeSymbol KeyType { get; init; } = default!;
        public bool KeyIsValueType { get; init; }
        public ITypeSymbol ValueType { get; init; } = default!;
        public bool ValueIsSimple { get; init; }
        public bool ValueIsBoolean { get; init; }
        public bool ValueIsEnum { get; init; }
        public bool ValueIsValueType { get; init; }
        public TypeDescriptor? ValueDescriptor { get; set; }
    }
}
