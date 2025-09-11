namespace RemoteMvvmTool.Generators;

/// <summary>
/// Translates an abstract <see cref="UIComponent"/> tree into a concrete UI
/// representation for a specific platform.
/// </summary>
public interface IUiTranslator
{
    string Translate(UIComponent component, string indent = "");
}
