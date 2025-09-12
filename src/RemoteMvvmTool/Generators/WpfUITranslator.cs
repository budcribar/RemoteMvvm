using System.Text;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Translates <see cref="UIComponent"/> trees into basic XAML snippets.
/// </summary>
public class WpfUITranslator : IUiTranslator
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
            "StackPanel" => "StackPanel",
            "TreeView" => "TreeView",
            "Button" => "Button",
            "TextBlock" => "TextBlock",
            _ => comp.Type
        };
        sb.Append(indent).Append('<').Append(tag);
        if (!string.IsNullOrEmpty(comp.Name))
            sb.Append($" x:Name=\"{comp.Name}\"");
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
