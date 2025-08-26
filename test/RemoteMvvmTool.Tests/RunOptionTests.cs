using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

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
    public async Task ServerGeneration_WinFormsMode_OmitsDispatcher()
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
        // Updated: ServerGenerator no longer includes dispatcher logic - MVVM Toolkit handles threading automatically
        Assert.DoesNotContain("Control _dispatcher", server);
        Assert.DoesNotContain("Dispatcher", server);
    }

    [Fact]
    public async Task ServerGeneration_WpfMode_OmitsDispatcher()
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
        // Updated: ServerGenerator no longer includes dispatcher logic - MVVM Toolkit handles threading automatically
        Assert.DoesNotContain("Dispatcher? _dispatcher", server);
        Assert.DoesNotContain("Dispatcher", server);
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
        var hasParameterlessCtor = sym?.Constructors.Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared) ?? false;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "wpf", hasParameterlessCtor, null);
        Assert.Contains("Dispatcher _dispatcher", partial);
        Assert.Contains("Dispatcher.CurrentDispatcher", partial);
        // Updated: ServerGenerator no longer takes dispatcher parameter - MVVM Toolkit handles threading automatically
        Assert.Contains($"new {name}GrpcServiceImpl(this)", partial);
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
        var hasParameterlessCtor = sym?.Constructors.Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared) ?? false;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "winforms", hasParameterlessCtor, null);
        Assert.Contains("Control _dispatcher", partial);
        Assert.Contains("new Control()", partial);
        // Updated: ServerGenerator no longer takes dispatcher parameter - MVVM Toolkit handles threading automatically
        Assert.Contains($"new {name}GrpcServiceImpl(this)", partial);
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
        var hasParameterlessCtor = sym?.Constructors.Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared) ?? false;
        var partial = ViewModelPartialGenerator.Generate(name, "Generated.Protos", name + "Service", vmNamespace, "Generated.Clients", baseClass, "console", hasParameterlessCtor, null);
        Assert.DoesNotContain("Dispatcher", partial);
        Assert.DoesNotContain("Control _dispatcher", partial);
        Assert.Contains($"new {name}GrpcServiceImpl(this)", partial);
    }

    [Fact]
    public void ViewModelPartialGeneration_UsesOptionsPort()
    {
        var partial = ViewModelPartialGenerator.Generate("TestViewModel", "Generated.Protos", "TestViewModelService", "Generated.ViewModels", "Generated.Clients", string.Empty, "console", true, null);
        Assert.Contains("kestrelOptions.ListenLocalhost(options.Port", partial);
        Assert.DoesNotContain("NetworkConfig.Port", partial);
    }

    [Fact]
    public void ViewModelPartialGeneration_NoParameterlessCtor_OmitsThisCall()
    {
        var partial = ViewModelPartialGenerator.Generate("NoDefaultViewModel", "Generated.Protos", "NoDefaultViewModelService", "Generated.ViewModels", "Generated.Clients", string.Empty, "console", false, null);
        Assert.DoesNotContain(": this()", partial);
    }

    [Fact]
    public void ViewModelPartialGenerator_WithoutNestedProperties_GeneratesBasicPartial()
    {
        var partial = ViewModelPartialGenerator.Generate("TestViewModel", "Test.Protos", "TestViewModelService", "Test.ViewModels", "Test.Clients", "ObservableObject", "wpf", true, null);

        // Should contain basic structure but no nested property change handlers
        Assert.Contains("public partial class TestViewModel", partial);
        Assert.Contains("StartAspNetCoreServer(options)", partial);
        Assert.DoesNotContain("// Auto-generated nested property change handlers", partial);
        Assert.DoesNotContain("_CollectionChanged", partial);
        Assert.DoesNotContain("_ItemPropertyChanged", partial);
    }

    [Fact] 
    public void ViewModelPartialGenerator_WithEmptyPropertiesList_GeneratesBasicPartial()
    {
        var properties = new List<PropertyInfo>();
        var partial = ViewModelPartialGenerator.Generate("TestViewModel", "Test.Protos", "TestViewModelService", "Test.ViewModels", "Test.Clients", "ObservableObject", "wpf", true, properties);

        // Should contain basic structure but no nested property change handlers
        Assert.Contains("public partial class TestViewModel", partial);
        Assert.DoesNotContain("// Auto-generated nested property change handlers", partial);
        Assert.DoesNotContain("_CollectionChanged", partial);
    }
}
