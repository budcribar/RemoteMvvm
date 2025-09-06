using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class ViewModelPartialGenerator
{
    private const string HandlerSuffix = "_RemoteMvvm"; // ensure uniqueness vs. user code

    private static void GenerateNestedPropertyChangeHandlers(StringBuilder sb, List<PropertyInfo> properties)
    {
        var collectionsNeedingHandlers = GetCollectionsNeedingEventHandlers(properties);
        var generatedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var collection in collectionsNeedingHandlers)
        {
            var propName = collection.Name;
            var elementTypeName = GetElementTypeName(collection.FullTypeSymbol!);
            var handlerName = propName + HandlerSuffix + "_CollectionChanged";
            var itemHandlerName = propName + HandlerSuffix + "_ItemPropertyChanged";
            if (!generatedNames.Add(handlerName)) continue; // already emitted handlers for this property
            generatedNames.Add(itemHandlerName);

            sb.AppendLine($"        // Auto-generated nested property change handlers for {propName}");
            sb.AppendLine($"        private void {handlerName}(object? sender, NotifyCollectionChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (e.NewItems != null)");
            sb.AppendLine($"                foreach ({elementTypeName} item in e.NewItems)");
            sb.AppendLine($"                    item.PropertyChanged += {itemHandlerName};");
            sb.AppendLine("            if (e.OldItems != null)");
            sb.AppendLine($"                foreach ({elementTypeName} item in e.OldItems)");
            sb.AppendLine($"                    item.PropertyChanged -= {itemHandlerName};");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private void {itemHandlerName}(object? sender, PropertyChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var index = {propName}.IndexOf(({elementTypeName})sender!);");
            sb.AppendLine($"            OnPropertyChanged(\"{propName}[{{index}}].{{e.PropertyName}}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }

    private static void GenerateConstructorEventWiring(StringBuilder sb, List<PropertyInfo> properties)
    {
        var collectionsNeedingHandlers = GetCollectionsNeedingEventHandlers(properties);
        var wired = new HashSet<string>(StringComparer.Ordinal);

        if (collectionsNeedingHandlers.Count > 0)
        {
            sb.AppendLine("            // Auto-generated event wiring for nested property changes");
            foreach (var collection in collectionsNeedingHandlers)
            {
                var propName = collection.Name;
                if (!wired.Add(propName)) continue; // avoid duplicate wiring
                var elementTypeName = GetElementTypeName(collection.FullTypeSymbol!);
                sb.AppendLine($"            {propName}.CollectionChanged += {propName}{HandlerSuffix}_CollectionChanged;");
                sb.AppendLine($"            foreach ({elementTypeName} item in {propName}) item.PropertyChanged += {propName}{HandlerSuffix}_ItemPropertyChanged;");
            }
            sb.AppendLine();
        }
    }

    private static List<PropertyInfo> GetCollectionsNeedingEventHandlers(List<PropertyInfo> properties)
    {
        // De-duplicate by property name to avoid duplicate handler generation
        var result = properties
            .Where(p => p.FullTypeSymbol != null && IsObservableCollectionOfNotifyingElements(p.FullTypeSymbol!))
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();
        return result;
    }

    private static bool IsObservableCollectionOfNotifyingElements(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        // Check if it's ObservableCollection<T>
        if (!namedType.IsGenericType) return false;
        var genericTypeDefinition = namedType.ConstructedFrom.ToDisplayString();
        if (genericTypeDefinition != "System.Collections.ObjectModel.ObservableCollection<T>") return false;

        // Check if T implements INotifyPropertyChanged
        var elementType = namedType.TypeArguments[0];
        return ImplementsINotifyPropertyChanged(elementType);
    }

    private static bool ImplementsINotifyPropertyChanged(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        // Check all interfaces
        var allInterfaces = namedType.AllInterfaces;
        return allInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged");
    }

    private static string GetElementTypeName(ITypeSymbol collectionType)
    {
        if (collectionType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var elementType = namedType.TypeArguments[0];
            return elementType.ToDisplayString();
        }
        return "object";
    }

    public static string Generate(string vmName, string protoNs, string serviceName, string vmNamespace, string clientNamespace, string baseClass, string runType = "wpf", bool hasParameterlessConstructor = true, List<PropertyInfo>? properties = null)
    {
        var sb = new StringBuilder();
        GeneratorHelpers.AppendAutoGeneratedHeader(sb);

        // Extract variables for raw string interpolation
        var vmNameVar = vmName;
        var protoNsVar = protoNs;
        var serviceNameVar = serviceName;
        var vmNamespaceVar = vmNamespace;
        var clientNamespaceVar = clientNamespace;
        var baseClassVar = baseClass;
        var runTypeVar = runType;
        var hasParameterlessConstructorVar = hasParameterlessConstructor;

        // Determine base clause
        var baseClause = string.IsNullOrWhiteSpace(baseClassVar) ? "" : baseClassVar;
        if (!string.IsNullOrWhiteSpace(baseClause))
            baseClass += ", IDisposable";
        else
            baseClass = "IDisposable";

        // Determine constructor suffixes
        var serverCtorSuffix = hasParameterlessConstructorVar ? " : this()" : string.Empty;
        var clientCtorSuffix = hasParameterlessConstructorVar ? " : this()" : string.Empty;

        // Determine dispatcher field
        var dispatcherField = runTypeVar switch
        {
            "wpf" => "        private readonly Dispatcher _dispatcher;",
            "winforms" => "        private readonly Control _dispatcher;",
            _ => ""
        };

        // Determine dispatcher initialization
        var dispatcherInit = runTypeVar switch
        {
            "wpf" => "            _dispatcher = Dispatcher.CurrentDispatcher;",
            "winforms" => $$"""
            _dispatcher = new Control();
            _dispatcher.CreateControl();
""",
            _ => ""
        };

        // Determine client dispatcher initialization
        var clientDispatcherInit = (runTypeVar == "wpf" || runTypeVar == "winforms") ? "            _dispatcher = null!;" : "";

        // Determine WPF/WinForms using statement
        var platformUsing = runTypeVar switch
        {
            "wpf" => "using System.Windows.Threading;",
            "winforms" => "using System.Windows.Forms;",
            _ => ""
        };

        // Generate nested property change handlers and constructor wiring if needed
        var nestedPropertyHandlers = "";
        var constructorEventWiring = "";

        if (properties != null && properties.Count > 0)
        {
            var handlersSb = new StringBuilder();
            GenerateNestedPropertyChangeHandlers(handlersSb, properties);
            nestedPropertyHandlers = handlersSb.ToString();

            var wiringSb = new StringBuilder();
            GenerateConstructorEventWiring(wiringSb, properties);
            constructorEventWiring = wiringSb.ToString();
        }

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {clientNamespace};");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine("using System.Collections.Specialized;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Hosting;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using Microsoft.Extensions.Hosting;");
        sb.AppendLine("using System.Diagnostics;");
        if (!string.IsNullOrEmpty(platformUsing)) sb.AppendLine(platformUsing);
        sb.AppendLine("using PeakSWC.Mvvm.Remote;");
        sb.AppendLine();
        sb.AppendLine($"namespace {vmNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {vmName} : {baseClass}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private {vmName}GrpcServiceImpl? _grpcService;");
        if (!string.IsNullOrEmpty(dispatcherField)) sb.AppendLine(dispatcherField);
        sb.AppendLine("        private IHost? _aspNetCoreHost;");
        sb.AppendLine("        private GrpcChannel? _channel;");
        sb.AppendLine($"        private {clientNamespace}.{vmName}RemoteClient? _remoteClient;");
        if (!string.IsNullOrEmpty(nestedPropertyHandlers)) sb.Append(nestedPropertyHandlers);
        sb.AppendLine($"        public {vmName}(ServerOptions options){serverCtorSuffix}");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options == null) throw new ArgumentNullException(nameof(options));");
        sb.AppendLine($"{dispatcherInit}            _grpcService = new {vmName}GrpcServiceImpl(this);");
        if (!string.IsNullOrEmpty(constructorEventWiring)) sb.Append(constructorEventWiring);
        sb.AppendLine("            StartAspNetCoreServer(options);");
        sb.AppendLine("        }");
        sb.AppendLine("        private void StartAspNetCoreServer(ServerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            var builder = WebApplication.CreateBuilder();");
        sb.AppendLine("            builder.Services.AddGrpc();");
        sb.AppendLine("            builder.Services.AddCors(o => o.AddPolicy(\"AllowAll\", builder =>\n            {\n                builder.AllowAnyOrigin()\n                       .AllowAnyMethod()\n                       .AllowAnyHeader()\n                       .WithExposedHeaders(\"Grpc-Status\", \"Grpc-Message\", \"Grpc-Encoding\", \"Grpc-Accept-Encoding\");\n            }));");
        sb.AppendLine("            builder.Services.AddSingleton(_grpcService!);");
        sb.AppendLine("            builder.WebHost.ConfigureKestrel(kestrelOptions =>\n            {\n                kestrelOptions.ListenLocalhost(options.Port, listenOptions =>\n                {\n                    if (options.UseHttps)\n                    {\n                        listenOptions.UseHttps();\n                        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;\n                    }\n                    else\n                    {\n                        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;\n                    }\n                });\n            });");
        sb.AppendLine("            var app = builder.Build();");
        sb.AppendLine("            app.UseRouting();");
        sb.AppendLine("            app.UseCors(\"AllowAll\");");
        sb.AppendLine("            app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });");
        sb.AppendLine("            app.MapGet(\"/status\", () => \"Server is running.\");");
        sb.AppendLine($"            app.MapGrpcService<{vmName}GrpcServiceImpl>()\n               .EnableGrpcWeb()\n               .RequireCors(\"AllowAll\");");
        sb.AppendLine("            _aspNetCoreHost = app;\n            Task.Run(() => app.RunAsync());");
        sb.AppendLine("        }");
        sb.AppendLine($"        public {vmName}(ClientOptions options){clientCtorSuffix}");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options == null) throw new ArgumentNullException(nameof(options));");
        if (!string.IsNullOrEmpty(clientDispatcherInit)) sb.AppendLine(clientDispatcherInit);
        sb.AppendLine("            _channel = GrpcChannel.ForAddress(options.Address);");
        sb.AppendLine($"            var client = new {protoNs}.{serviceName}.{serviceName}Client(_channel);");
        sb.AppendLine($"            _remoteClient = new {vmName}RemoteClient(client);");
        sb.AppendLine("        }");
        sb.AppendLine($"        public async Task<{vmName}RemoteClient> GetRemoteModel()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_remoteClient == null) throw new InvalidOperationException(\"Client options not provided\");");
        sb.AppendLine("            await _remoteClient.InitializeRemoteAsync();");
        sb.AppendLine("            return _remoteClient;");
        sb.AppendLine("        }");
        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            _channel?.ShutdownAsync().GetAwaiter().GetResult();");
        sb.AppendLine("            _aspNetCoreHost?.StopAsync().GetAwaiter().GetResult();");
        sb.AppendLine("            _aspNetCoreHost?.Dispose();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
