using GrpcRemoteMvvmModelUtil;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteMvvmTool.Generators;

public static class ServerGenerator
{
    public static string Generate(string vmName, string protoNs, string serviceName, List<PropertyInfo> props, List<CommandInfo> cmds, string viewModelNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine($"using {protoNs};");
        sb.AppendLine($"using {viewModelNamespace};");
        sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Concurrent;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using System.Threading.Channels;");
        sb.AppendLine("using System.Windows.Threading;");
        sb.AppendLine("using Channel = System.Threading.Channels.Channel;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();
        sb.AppendLine($"public partial class {vmName}GrpcServiceImpl : {serviceName}.{serviceName}Base");
        sb.AppendLine("{");
        sb.AppendLine("    public static event System.EventHandler<int>? ClientCountChanged;");
        sb.AppendLine("    private static int _clientCount = -1;");
        sb.AppendLine("    public static int ClientCount");
        sb.AppendLine("    {");
        sb.AppendLine("        get => _clientCount;");
        sb.AppendLine("        private set");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_clientCount != value)");
        sb.AppendLine("            {");
        sb.AppendLine("                _clientCount = value;");
        sb.AppendLine("                ClientCountChanged?.Invoke(null, value);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    static {vmName}GrpcServiceImpl()");
        sb.AppendLine("    {");
        sb.AppendLine("        ClientCount = 0;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    private readonly {vmName} _viewModel;");
        sb.AppendLine($"    private static readonly ConcurrentDictionary<IServerStreamWriter<{protoNs}.PropertyChangeNotification>, Channel<{protoNs}.PropertyChangeNotification>> _subscriberChannels = new ConcurrentDictionary<IServerStreamWriter<{protoNs}.PropertyChangeNotification>, Channel<{protoNs}.PropertyChangeNotification>>();");
        sb.AppendLine("    private readonly Dispatcher _dispatcher;");
        sb.AppendLine("    private readonly ILogger? _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {vmName}GrpcServiceImpl({vmName} viewModel, Dispatcher dispatcher, ILogger<{vmName}GrpcServiceImpl>? logger = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));");
        sb.AppendLine("        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("        if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override Task<{vmName}State> GetState(Empty request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var state = new {vmName}State();");
        foreach (var p in props)
        {
            sb.AppendLine($"        // Mapping property: {p.Name} to state.{p.Name}");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine($"            var propValue = _viewModel.{p.Name};");
            if (true)
            {
                sb.AppendLine($"            state.{p.Name} = propValue;");
            }
            sb.AppendLine("        }");
            sb.AppendLine($"        catch (Exception ex) {{ Debug.WriteLine(\"[GrpcService:{vmName}] Error mapping property {p.Name} to state.{p.Name}: \" + ex.Message); }}");
        }
        sb.AppendLine("        return Task.FromResult(state);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override async Task SubscribeToPropertyChanges({protoNs}.SubscribeRequest request, IServerStreamWriter<{protoNs}.PropertyChangeNotification> responseStream, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var channel = Channel.CreateUnbounded<{protoNs}.PropertyChangeNotification>(new UnboundedChannelOptions {{ SingleReader = true, SingleWriter = false }});");
        sb.AppendLine("        _subscriberChannels.TryAdd(responseStream, channel);");
        sb.AppendLine("        ClientCount = _subscriberChannels.Count;");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            await foreach (var notification in channel.Reader.ReadAllAsync(context.CancellationToken))");
        sb.AppendLine("            {");
        sb.AppendLine("                await responseStream.WriteAsync(notification);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            _subscriberChannels.TryRemove(responseStream, out _);");
        sb.AppendLine("            channel.Writer.TryComplete();");
        sb.AppendLine("            ClientCount = _subscriberChannels.Count;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override Task<Empty> UpdatePropertyValue({protoNs}.UpdatePropertyValueRequest request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        _dispatcher.Invoke(() => {");
        sb.AppendLine("            var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);");
        sb.AppendLine("            if (propertyInfo != null && propertyInfo.CanWrite)");
        sb.AppendLine("            {");
        sb.AppendLine("                try {");
        sb.AppendLine("                    if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);");
        sb.AppendLine("                    else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);");
        sb.AppendLine("                    else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);");
        sb.AppendLine("                    else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Unpacking not implemented for property \" + request.PropertyName + \" and type \" + request.NewValue.TypeUrl + \".\"); }");
        sb.AppendLine("                } catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error setting property \" + request.PropertyName + \": \" + ex.Message); }");
        sb.AppendLine("            }");
        sb.AppendLine("            else { Debug.WriteLine(\"[GrpcService:" + vmName + "] UpdatePropertyValue: Property \" + request.PropertyName + \" not found or not writable.\"); }");
        sb.AppendLine("        });");
        sb.AppendLine("        return Task.FromResult(new Empty());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override Task<ConnectionStatusResponse> Ping(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.FromResult(new ConnectionStatusResponse { Status = ConnectionStatus.Connected });");
        sb.AppendLine("    }");
        sb.AppendLine();
        foreach (var cmd in cmds)
        {
            sb.AppendLine($"    public override async Task<{protoNs}.{cmd.MethodName}Response> {cmd.MethodName}({protoNs}.{cmd.MethodName}Request request, ServerCallContext context)");
            sb.AppendLine("    {");
            sb.AppendLine("        try { await await _dispatcher.InvokeAsync(async () => {");
            string methodCall;
            if (cmd.Parameters.Count == 0)
            {
                methodCall = $"_viewModel.{cmd.MethodName}()";
            }
            else
            {
                var paramList = string.Join(", ", cmd.Parameters.Select(p => $"request.{GeneratorHelpers.ToPascalCase(p.Name)}"));
                methodCall = $"_viewModel.{cmd.MethodName}({paramList})";
            }
            if (cmd.IsAsync)
                sb.AppendLine($"            await {methodCall};");
            else
                sb.AppendLine($"            {methodCall};");
            sb.AppendLine("        }); } catch (Exception ex) {");
            sb.AppendLine($"        Debug.WriteLine(\"[GrpcService:{vmName}] Exception during command execution for {cmd.MethodName}: \" + ex.ToString());");
            sb.AppendLine("        throw new RpcException(new Status(StatusCode.Internal, \"Error executing command on server: \" + ex.Message));");
            sb.AppendLine("        }");
            sb.AppendLine($"        return new {protoNs}.{cmd.MethodName}Response();");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.AppendLine("    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrEmpty(e.PropertyName)) return;");
        sb.AppendLine("        object? newValue = null;");
        sb.AppendLine("        try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }");
        sb.AppendLine("        catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error getting property value for \" + e.PropertyName + \": \" + ex.Message); return; }");
        sb.AppendLine();
        sb.AppendLine($"        var notification = new {protoNs}.PropertyChangeNotification {{ PropertyName = e.PropertyName }};");
        sb.AppendLine("        if (newValue == null) notification.NewValue = Any.Pack(new Empty());");
        sb.AppendLine("        else if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });");
        sb.AppendLine("        else if (newValue is int i) notification.NewValue = Any.Pack(new Int32Value { Value = i });");
        sb.AppendLine("        else if (newValue is bool b) notification.NewValue = Any.Pack(new BoolValue { Value = b });");
        sb.AppendLine("        else if (newValue is double d) notification.NewValue = Any.Pack(new DoubleValue { Value = d });");
        sb.AppendLine("        else if (newValue is float f) notification.NewValue = Any.Pack(new FloatValue { Value = f });");
        sb.AppendLine("        else if (newValue is long l) notification.NewValue = Any.Pack(new Int64Value { Value = l });");
        sb.AppendLine("        else if (newValue is DateTime dt) notification.NewValue = Any.Pack(Timestamp.FromDateTime(dt.ToUniversalTime()));");
        sb.AppendLine($"        else {{ Debug.WriteLine($\"[GrpcService:{vmName}] PropertyChanged: Packing not implemented for type {{(newValue?.GetType().FullName ?? \"null\")}} of property {{e.PropertyName}}.\"); notification.NewValue = Any.Pack(new StringValue {{ Value = newValue.ToString() }}); }}");
        sb.AppendLine();
        sb.AppendLine("        foreach (var channelWriter in _subscriberChannels.Values.Select(c => c.Writer))");
        sb.AppendLine("        {");
        sb.AppendLine("            try { await channelWriter.WriteAsync(notification); }");
        sb.AppendLine("            catch (ChannelClosedException) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Channel closed for a subscriber, cannot write notification for '\" + e.PropertyName + \"'. Subscriber likely disconnected.\"); }");
        sb.AppendLine("            catch (Exception ex) { Debug.WriteLine(\"[GrpcService:" + vmName + "] Error writing to subscriber channel for '\" + e.PropertyName + \"': \" + ex.Message); }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

}
