using RemoteMvvmTool.UIComponents;

namespace RemoteMvvmTool.Generators;

/// <summary>
/// Translates a <see cref="UIComponent"/> tree into framework specific code.
/// </summary>
public interface IUITranslator
{
    string Translate(UIComponent root);
}
