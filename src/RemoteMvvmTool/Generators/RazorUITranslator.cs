using System.Text;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Produces simple Razor/HTML markup from <see cref="UIComponent"/> trees
/// for Blazor frontends.
/// </summary>
public class RazorUITranslator : IUiTranslator
{
    public string Translate(UIComponent component, string indent = "")
    {
        var sb = new StringBuilder();
        Translate(component, sb, indent);
        return sb.ToString();
    }

    private void Translate(UIComponent comp, StringBuilder sb, string indent)
    {
        var tag = comp.Type switch
        {
            "StackPanel" => "div",
            "TreeView" => "ul",
            "Button" => "button",
            "TextBlock" => "span",
            _ => "div"
        };
        sb.Append(indent).Append('<').Append(tag);
        if (!string.IsNullOrEmpty(comp.Name))
            sb.Append($" id=\"{comp.Name}\"");
        sb.Append('>');
        if (!string.IsNullOrEmpty(comp.Content))
            sb.Append(comp.Content);
        if (comp.Children.Count > 0)
        {
            sb.AppendLine();
            foreach (var child in comp.Children)
                Translate(child, sb, indent + "    ");
            sb.Append(indent).Append("</").Append(tag).AppendLine(">");
        }
        else
        {
            sb.Append("</").Append(tag).AppendLine(">");
        }
    }
}
