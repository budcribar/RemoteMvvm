namespace RemoteMvvmTool.Generators;

using System.Collections.Generic;

/// <summary>
/// Represents a simple UI component in an abstract DSL used to describe
/// UI elements in a platform agnostic way.
/// </summary>
public class UIComponent
{
    public string Type { get; }
    public string? Name { get; }
    public string? Content { get; }
    public Dictionary<string, string> Attributes { get; } = new();
    public List<UIComponent> Children { get; } = new();

    public UIComponent(string type, string? name = null, string? content = null)
    {
        Type = type;
        Name = name;
        Content = content;
    }
}
