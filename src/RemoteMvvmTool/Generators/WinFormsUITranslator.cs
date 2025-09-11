using System.Text;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Converts <see cref="UIComponent"/> definitions into WinForms C# code.
/// </summary>
public class WinFormsUITranslator : IUITranslator
{
    public string Translate(UIComponent component)
    {
        var sb = new StringBuilder();
        Translate(component, sb, "", null);
        return sb.ToString();
    }

    private void Translate(UIComponent comp, StringBuilder sb, string indent, string? parent)
    {
        switch (comp)
        {
            case ContainerComponent container:
                if (container.ContainerType == "StackPanel")
                {
                    foreach (var child in container.Children)
                        Translate(child, sb, indent, parent);
                }
                else
                {
                    var name = container.Name ?? $"{container.ContainerType.ToLower()}1";
                    sb.AppendLine($"{indent}var {name} = new {container.ContainerType}();");
                    if (parent != null)
                        sb.AppendLine($"{indent}{parent}.Controls.Add({name});");
                    foreach (var child in container.Children)
                        Translate(child, sb, indent, name);
                }
                break;
            case TreeViewComponent tree:
                sb.AppendLine($"{indent}var {tree.Name} = new TreeView();");
                if (parent != null)
                    sb.AppendLine($"{indent}{parent}.Controls.Add({tree.Name});");
                break;
            case ButtonComponent button:
                sb.AppendLine($"{indent}var {button.Name} = new Button();");
                if (!string.IsNullOrEmpty(button.Content))
                    sb.AppendLine($"{indent}{button.Name}.Text = \"{button.Content}\";");
                if (parent != null)
                    sb.AppendLine($"{indent}{parent}.Controls.Add({button.Name});");
                break;
            case TextBlockComponent text:
                var lblName = text.Name ?? "label";
                sb.AppendLine($"{indent}var {lblName} = new Label();");
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
