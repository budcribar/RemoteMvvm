using RemoteMvvmTool.Generators;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using System.Collections.Generic;

namespace ToolExecution;

public class CsProjectGeneratorTests
{
    [Fact]
    public void GenerateCsProj_UsesWpfFlag()
    {
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "wpf");
        Assert.Contains("<UseWPF>true</UseWPF>", proj);
        Assert.Contains("protos/Svc.proto", proj);
        Assert.DoesNotContain("UseWindowsForms", proj);
    }

    [Fact]
    public void GenerateCsProj_UsesWinFormsFlag()
    {
        string proj = CsProjectGenerator.GenerateCsProj("TestProj", "Svc", "winforms");
        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", proj);
        Assert.DoesNotContain("<UseWPF>", proj);
    }

    [Fact]
    public void GenerateProgramCs_WpfCreatesWindow()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateProgramCs("MyApp", "wpf", "Proto.Ns", "Svc", "Client.Ns", props, cmds);
        Assert.Contains("new Application()", prog);
        Assert.Contains("app.Run(window)", prog);
    }

    [Fact]
    public void GenerateProgramCs_WinFormsCreatesForm()
    {
        var props = new List<PropertyInfo>();
        var cmds = new List<CommandInfo>();
        string prog = CsProjectGenerator.GenerateProgramCs("MyApp", "winforms", "Proto.Ns", "Svc", "Client.Ns", props, cmds);
        Assert.Contains("Application.Run(form)", prog);
    }

    [Fact]
    public void GenerateProgramCs_IncludesPropertyAndCommand()
    {
        var props = new List<PropertyInfo> { new("IsEnabled", "bool", null!) };
        var cmds = new List<CommandInfo> { new("DoWork", "DoWorkCommand", new List<ParameterInfo>(), false) };
        string prog = CsProjectGenerator.GenerateProgramCs("Vm", "wpf", "Proto.Ns", "Svc", "Client.Ns", props, cmds);
        Assert.Contains("IsEnabled", prog);
        Assert.Contains("UpdatePropertyValueRequest", prog);
        Assert.Contains("DoWorkCommand", prog);
    }

    [Fact]
    public void GenerateProgramCs_WpfHandlesReadOnlyAndInt()
    {
        var props = new List<PropertyInfo>
        {
            new("Instructions", "string", null!, true),
            new("Threshold", "int", null!)
        };
        string prog = CsProjectGenerator.GenerateProgramCs("Vm", "wpf", "Proto.Ns", "Svc", "Client.Ns", props, new List<CommandInfo>());
        Assert.Contains("IsReadOnly = true", prog);
        Assert.Contains("Mode = BindingMode.OneWay", prog);
        Assert.Contains("int.TryParse(thresholdBox.Text, out var value)", prog);
        Assert.Contains("Any.Pack(new Int32Value", prog);
    }

    [Fact]
    public void GenerateProgramCs_WinFormsUsesOneWayBinding()
    {
        var props = new List<PropertyInfo> { new("Count", "int", null!) };
        string prog = CsProjectGenerator.GenerateProgramCs("Vm", "winforms", "Proto.Ns", "Svc", "Client.Ns", props, new List<CommandInfo>());
        Assert.Contains("DataSourceUpdateMode.Never", prog);
        Assert.Contains("int.TryParse(countBox.Text, out var value)", prog);
    }
}
