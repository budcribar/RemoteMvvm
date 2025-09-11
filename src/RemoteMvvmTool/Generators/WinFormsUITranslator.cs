using System.Text;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Converts <see cref="UIComponent"/> definitions into WinForms C# code.
/// </summary>
public class WinFormsUITranslator : IUiTranslator
{
    public string Translate(UIComponent component, string indent = "")
    {
        var sb = new StringBuilder();
        Translate(component, sb, indent, null);
        return sb.ToString();
    }

    private void Translate(UIComponent comp, StringBuilder sb, string indent, string? parent)
    {
        switch (comp.Type)
        {
            case "StackPanel":
                foreach (var child in comp.Children)
                    Translate(child, sb, indent, parent);
                return;
            case "TreeView":
                sb.AppendLine($"{indent}var {comp.Name} = new TreeView();");
                break;
            case "Button":
                sb.AppendLine($"{indent}var {comp.Name} = new Button();");
                if (!string.IsNullOrEmpty(comp.Content))
                    sb.AppendLine($"{indent}{comp.Name}.Text = \"{comp.Content}\";");
                break;
            case "Panel":
                sb.AppendLine($"{indent}var {comp.Name} = new Panel();");
                break;
            case "TableLayoutPanel":
                sb.AppendLine($"{indent}var {comp.Name} = new TableLayoutPanel();");
                break;
            default:
                sb.AppendLine($"{indent}// Unknown component: {comp.Type}");
                break;
        }

        if (parent != null && comp.Name != null)
        {
            sb.AppendLine($"{indent}{parent}.Controls.Add({comp.Name});");
        }

        foreach (var child in comp.Children)
        {
            Translate(child, sb, indent, comp.Name ?? parent);
        }
    }
}
