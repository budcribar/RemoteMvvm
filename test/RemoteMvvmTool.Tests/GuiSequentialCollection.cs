using Xunit;

namespace RemoteMvvmTool.Tests
{
    // Collection definition to serialize all GUI (WPF + WinForms) tests
    [CollectionDefinition("GuiSequential", DisableParallelization = true)]
    public sealed class GuiSequentialCollection { }
}