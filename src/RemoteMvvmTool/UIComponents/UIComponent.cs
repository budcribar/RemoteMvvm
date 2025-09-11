namespace RemoteMvvmTool.UIComponents;

using System.Collections.Generic;

/// <summary>
/// Base type for all UI description nodes.
/// </summary>
public abstract class UIComponent
{
    public string? Name { get; }
    public List<UIComponent> Children { get; } = new();

    protected UIComponent(string? name = null)
    {
        Name = name;
    }
}

/// <summary>
/// Represents a generic container element such as StackPanel or Panel.
/// </summary>
public class ContainerComponent : UIComponent
{
    public string ContainerType { get; }

    public ContainerComponent(string containerType, string? name = null)
        : base(name)
    {
        ContainerType = containerType;
    }
}

/// <summary>
/// Represents a TreeView control.
/// </summary>
public class TreeViewComponent : UIComponent
{
    public TreeViewComponent(string name) : base(name) { }
}

/// <summary>
/// Represents a Button control with optional content text.
/// </summary>
public class ButtonComponent : UIComponent
{
    public string? Content { get; }

    public ButtonComponent(string name, string? content = null) : base(name)
    {
        Content = content;
    }
}

/// <summary>
/// Represents a TextBlock/Label element.
/// </summary>
public class TextBlockComponent : UIComponent
{
    public string Text { get; }

    public TextBlockComponent(string text, string? name = null) : base(name)
    {
        Text = text;
    }
}

/// <summary>
/// Represents raw code that should be injected directly without translation.
/// </summary>
public class CodeBlockComponent : UIComponent
{
    public string Code { get; }

    public CodeBlockComponent(string code) : base()
    {
        Code = code;
    }
}
