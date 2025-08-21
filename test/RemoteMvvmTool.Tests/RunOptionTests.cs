using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
namespace ToolExecution;
public class RunOptionTests
{
    [Fact]
    public async Task ServerGeneration_ConsoleMode_OmitsDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, vmNamespace, "console");
        Assert.DoesNotContain("Dispatcher", server);
    }

    [Fact]
    public async Task ServerGeneration_WinFormsMode_UsesDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, vmNamespace, "winforms");
        Assert.Contains("Control _dispatcher", server);
    }

    [Fact]
    public async Task ServerGeneration_WpfMode_UsesDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var server = ServerGenerator.Generate(name, "Generated.Protos", name + "Service", props, cmds, vmNamespace, "wpf");
        Assert.Contains("Dispatcher _dispatcher", server);
    }

    [Fact]
    public async Task ViewModelPartialGeneration_WpfMode_UsesDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var baseClass = sym?.BaseType?.ToDisplayString() ?? string.Empty;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "wpf");
        Assert.Contains("Dispatcher _dispatcher", partial);
        Assert.Contains("Dispatcher.CurrentDispatcher", partial);
        Assert.Contains($"new {name}GrpcServiceImpl(this, _dispatcher)", partial);
    }

    [Fact]
    public async Task ViewModelPartialGeneration_WinFormsMode_UsesControlDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var baseClass = sym?.BaseType?.ToDisplayString() ?? string.Empty;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "winforms");
        Assert.Contains("Control _dispatcher", partial);
        Assert.Contains("new Control()", partial);
        Assert.Contains($"new {name}GrpcServiceImpl(this, _dispatcher)", partial);
    }

    [Fact]
    public async Task ViewModelPartialGeneration_ConsoleMode_OmitsDispatcher()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var vmDir = Path.Combine(root, "test", "SimpleViewModelTest", "ViewModels");
        var vmFile = Path.Combine(vmDir, "MainViewModel.cs");
        var refs = new System.Collections.Generic.List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) refs.Add(p);
        }
        var (sym, name, props, cmds, comp) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile },
            "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
            "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
            refs);
        var vmNamespace = sym?.ContainingNamespace.ToDisplayString() ?? string.Empty;
        var baseClass = sym?.BaseType?.ToDisplayString() ?? string.Empty;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "console");
        Assert.DoesNotContain("Dispatcher", partial);
        Assert.DoesNotContain("Control _dispatcher", partial);
        Assert.Contains($"new {name}GrpcServiceImpl(this)", partial);
    }

    [Fact]
    public void ViewModelPartialGeneration_UsesOptionsPort()
    {
        var partial = ViewModelPartialGenerator.Generate("TestViewModel", "Generated.Protos", "TestViewModelService", "Generated.ViewModels", "Generated.Clients", string.Empty, "console");
        Assert.Contains("kestrelOptions.ListenLocalhost(options.Port", partial);
        Assert.DoesNotContain("NetworkConfig.Port", partial);
    }
}
