using System.Text;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Produces simple Razor/HTML markup from <see cref="UIComponent"/> trees
/// for Blazor frontends.
/// </summary>
public class RazorUITranslator : IUITranslator
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
                var tag = container.ContainerType == "StackPanel" ? "div" : "div";
                sb.Append(indent).Append('<').Append(tag);
                if (!string.IsNullOrEmpty(container.Name))
                    sb.Append($" id=\"{container.Name}\"");
                if (container.Children.Count > 0)
                {
                    sb.AppendLine(">");
                    foreach (var child in container.Children)
                        Translate(child, sb, indent + "    ");
                    sb.Append(indent).Append("</").Append(tag).AppendLine(">");
                }
                else
                {
                    sb.AppendLine("</" + tag + ">");
                }
                break;
            case TreeViewComponent tree:
                sb.Append(indent).Append("<ul");
                if (!string.IsNullOrEmpty(tree.Name))
                    sb.Append($" id=\"{tree.Name}\"");
                sb.AppendLine("></ul>");
                break;
            case ButtonComponent button:
                sb.Append(indent).Append("<button");
                if (!string.IsNullOrEmpty(button.Name))
                    sb.Append($" id=\"{button.Name}\"");
                sb.Append('>').Append(button.Content ?? string.Empty).AppendLine("</button>");
                break;
            case TextBlockComponent text:
                sb.Append(indent).Append("<span");
                if (!string.IsNullOrEmpty(text.Name))
                    sb.Append($" id=\"{text.Name}\"");
                sb.Append('>').Append(text.Text).AppendLine("</span>");
                break;
            case CodeBlockComponent code:
                foreach (var line in code.Code.Split('\n'))
                    sb.Append(indent).AppendLine(line);
                break;
            default:
                sb.Append(indent).Append("<!-- Unknown component ").Append(comp.GetType().Name).AppendLine(" -->");
                break;
        }
    }
}
