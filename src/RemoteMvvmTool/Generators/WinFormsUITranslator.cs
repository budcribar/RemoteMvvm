using System.Text;
using System.Linq;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Converts <see cref="UIComponent"/> definitions into WinForms C# code.
/// </summary>
public class WinFormsUITranslator : IUITranslator
{
    private int _autoNameCounter = 1;

    public string Translate(UIComponent component) => Translate(component, "", null);

    public string Translate(UIComponent component, string indent = "", string? parent = null)
    {
        var sb = new StringBuilder();
        Translate(component, sb, indent, parent);
        return sb.ToString();
    }

    private void Translate(UIComponent comp, StringBuilder sb, string indent, string? parent)
    {
        switch (comp)
        {
            case ContainerComponent container:
                if (container.ContainerType == "StackPanel")
                {
                    if (parent == null)
                    {
                        var name = container.Name ?? $"panel{_autoNameCounter++}";
                        sb.AppendLine($"{indent}var {name} = new Panel {{ Dock = DockStyle.Fill }};");
                        parent = name;
                    }

                    // Add tree views first so they dock correctly beneath top-docked controls
                    foreach (var child in container.Children.Where(c => c is TreeViewComponent))
                        Translate(child, sb, indent, parent);
                    foreach (var child in container.Children.Where(c => c is not TreeViewComponent))
                        Translate(child, sb, indent, parent);
                }
                else
                {
                    var name = container.Name ?? $"{container.ContainerType.ToLower()}{_autoNameCounter++}";
                    sb.AppendLine($"{indent}var {name} = new {container.ContainerType} {{ Dock = DockStyle.Fill }};");
                    if (parent != null)
                        sb.AppendLine($"{indent}{parent}.Controls.Add({name});");
                    foreach (var child in container.Children)
                        Translate(child, sb, indent, name);
                }
                break;
            case TreeViewComponent tree:
                sb.AppendLine($"{indent}var {tree.Name} = new TreeView {{ Dock = DockStyle.Fill }};");
                if (parent != null)
                    sb.AppendLine($"{indent}{parent}.Controls.Add({tree.Name});");
                break;
            case ButtonComponent button:
                sb.AppendLine($"{indent}var {button.Name} = new Button();");
                if (!string.IsNullOrEmpty(button.Content))
                    sb.AppendLine($"{indent}{button.Name}.Text = \"{button.Content}\";");
                sb.AppendLine($"{indent}{button.Name}.Dock = DockStyle.Top;");
                sb.AppendLine($"{indent}{button.Name}.AutoSize = true;");
                if (parent != null)
                    sb.AppendLine($"{indent}{parent}.Controls.Add({button.Name});");
                break;
            case TextBlockComponent text:
                var lblName = text.Name ?? $"label{_autoNameCounter++}";
                sb.AppendLine($"{indent}var {lblName} = new Label {{ AutoSize = true, Dock = DockStyle.Top }};");
                sb.AppendLine($"{indent}{lblName}.Text = \"{text.Text}\";");
                if (parent != null)
                    sb.AppendLine($"{indent}{parent}.Controls.Add({lblName});");
                break;
            case CodeBlockComponent code:
                foreach (var line in code.Code.Split('\n'))
                    sb.AppendLine(indent + line);
                break;
            default:
                sb.AppendLine($"{indent}// Unknown component {comp.GetType().Name}");
                break;
        }
    }
}
