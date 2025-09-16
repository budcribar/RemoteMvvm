using System.Text;
using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Translates <see cref="UIComponent"/> hierarchies into HTML markup tailored for
/// the generated TypeScript client.
/// </summary>
public class TypeScriptUITranslator : IUITranslator
{
    public string Translate(UIComponent component)
    {
        var sb = new StringBuilder();
        Translate(component, sb, "        ");
        return sb.ToString();
    }

    private static void Translate(UIComponent component, StringBuilder sb, string indent)
    {
        switch (component)
        {
            case ContainerComponent container:
                var tag = ResolveTag(container.ContainerType);
                sb.Append(indent).Append('<').Append(tag);
                if (!string.IsNullOrEmpty(container.Name))
                    sb.Append($" id='{container.Name}'");
                if (!string.IsNullOrEmpty(container.CssClass))
                    sb.Append($" class='{container.CssClass}'");
                if (container.Children.Count > 0)
                {
                    sb.AppendLine(">");
                    foreach (var child in container.Children)
                        Translate(child, sb, indent + "    ");
                    sb.Append(indent).Append("</").Append(tag).AppendLine(">");
                }
                else
                {
                    sb.AppendLine($"></{tag}>");
                }
                break;

            case PlaceholderComponent placeholder:
                sb.Append(indent).Append('<').Append(placeholder.TagName);
                if (!string.IsNullOrEmpty(placeholder.Name))
                    sb.Append($" id='{placeholder.Name}'");
                if (!string.IsNullOrEmpty(placeholder.CssClass))
                    sb.Append($" class='{placeholder.CssClass}'");
                sb.AppendLine($"></{placeholder.TagName}>");
                break;

            case HeadingComponent heading:
                var level = heading.Level < 1 ? 1 : heading.Level;
                sb.Append(indent).Append('<').Append('h').Append(level).Append('>');
                sb.Append(heading.Text);
                sb.AppendLine($"</h{level}>");
                break;

            case ButtonComponent button:
                sb.Append(indent).Append("<button");
                if (!string.IsNullOrEmpty(button.Name))
                    sb.Append($" id='{button.Name}'");
                sb.Append('>');
                if (!string.IsNullOrEmpty(button.Content))
                    sb.Append(button.Content);
                sb.AppendLine("</button>");
                break;

            case TextBlockComponent text:
                sb.Append(indent).Append("<span");
                if (!string.IsNullOrEmpty(text.Name))
                    sb.Append($" id='{text.Name}'");
                sb.Append('>').Append(text.Text).AppendLine("</span>");
                break;

            case CodeBlockComponent code:
                foreach (var line in code.Code.Split('\n'))
                    sb.Append(indent).AppendLine(line);
                break;

            default:
                sb.Append(indent)
                  .Append("<!-- Unsupported component: ")
                  .Append(component.GetType().Name)
                  .AppendLine(" -->");
                break;
        }
    }

    private static string ResolveTag(string containerType)
    {
        return containerType switch
        {
            "StackPanel" => "div",
            "DockPanel" => "div",
            "Grid" => "div",
            _ => string.IsNullOrWhiteSpace(containerType) ? "div" : containerType
        };
    }
}
