using System.Text;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Translates <see cref="UIComponent"/> trees into basic XAML snippets.
/// </summary>
public class WpfUITranslator : IUITranslator
{
    public string Translate(UIComponent component)
    {
        var sb = new StringBuilder();
        Translate(component, sb, "");
        return sb.ToString();
    }

    private void Translate(UIComponent comp, StringBuilder sb, string indent)
    {
        switch (comp)
        {
            case ContainerComponent container:
                var tag = container.ContainerType;
                sb.Append(indent).Append('<').Append(tag);
                if (!string.IsNullOrEmpty(container.Name))
                    sb.Append($" x:Name=\"{container.Name}\"");
                if (container.Children.Count > 0)
                {
                    sb.AppendLine(">");
                    foreach (var child in container.Children)
                        Translate(child, sb, indent + "    ");
                    sb.Append(indent).Append("</").Append(tag).AppendLine(">");
                }
                else
                {
                    sb.AppendLine(" />");
                }
                break;
            case TreeViewComponent tree:
                sb.Append(indent).Append("<TreeView");
                if (!string.IsNullOrEmpty(tree.Name))
                    sb.Append($" x:Name=\"{tree.Name}\"");
                sb.AppendLine(" />");
                break;
            case ButtonComponent button:
                sb.Append(indent).Append("<Button");
                if (!string.IsNullOrEmpty(button.Name))
                    sb.Append($" x:Name=\"{button.Name}\"");
                if (!string.IsNullOrEmpty(button.Content))
                {
                    sb.Append('>').Append(button.Content).AppendLine("</Button>");
                }
                else
                {
                    sb.AppendLine(" />");
                }
                break;
            case TextBlockComponent textBlock:
                sb.Append(indent).Append("<TextBlock");
                if (!string.IsNullOrEmpty(textBlock.Name))
                    sb.Append($" x:Name=\"{textBlock.Name}\"");
                sb.Append('>').Append(textBlock.Text).AppendLine("</TextBlock>");
                break;
            case CodeBlockComponent code:
                sb.Append(indent).Append("<!-- ").Append(code.Code).AppendLine(" -->");
                break;
            default:
                sb.Append(indent).Append("<!-- Unknown component ").Append(comp.GetType().Name).AppendLine(" -->");
                break;
        }
    }
}
