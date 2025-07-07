using Grpc.Core;
using Pointer.ViewModels.Protos;
using HPSystemsTools;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;
using System.Windows.Threading;
using Channel = System.Threading.Channels.Channel;
using Microsoft.Extensions.Logging;

public partial class PointerViewModelGrpcServiceImpl : PointerViewModelService.PointerViewModelServiceBase
{
    public static event System.EventHandler<int>? ClientCountChanged;
    private static int _clientCount = -1;
    public static int ClientCount
    {
        get => _clientCount;
        private set
        {
            if (_clientCount != value)
            {
                _clientCount = value;
                ClientCountChanged?.Invoke(null, value);
            }
        }
    }

    static PointerViewModelGrpcServiceImpl()
    {
        ClientCount = 0;
    }

    private readonly PointerViewModel _viewModel;
    private static readonly ConcurrentDictionary<IServerStreamWriter<Pointer.ViewModels.Protos.PropertyChangeNotification>, Channel<Pointer.ViewModels.Protos.PropertyChangeNotification>> _subscriberChannels = new ConcurrentDictionary<IServerStreamWriter<Pointer.ViewModels.Protos.PropertyChangeNotification>, Channel<Pointer.ViewModels.Protos.PropertyChangeNotification>>();
    private readonly Dispatcher _dispatcher;
    private readonly ILogger? _logger;

    public PointerViewModelGrpcServiceImpl(PointerViewModel viewModel, Dispatcher dispatcher, ILogger<PointerViewModelGrpcServiceImpl>? logger = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
        if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }
    }

    public override Task<PointerViewModelState> GetState(Empty request, ServerCallContext context)
    {
        var state = new PointerViewModelState();
        return Task.FromResult(state);
    }

    public override async Task SubscribeToPropertyChanges(Pointer.ViewModels.Protos.SubscribeRequest request, IServerStreamWriter<Pointer.ViewModels.Protos.PropertyChangeNotification> responseStream, ServerCallContext context)
    {
        var channel = Channel.CreateUnbounded<Pointer.ViewModels.Protos.PropertyChangeNotification>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _subscriberChannels.TryAdd(responseStream, channel);
        ClientCount = _subscriberChannels.Count;
        try
        {
            await foreach (var notification in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(notification);
            }
        }
        finally
        {
            _subscriberChannels.TryRemove(responseStream, out _);
            channel.Writer.TryComplete();
            ClientCount = _subscriberChannels.Count;
        }
    }

    public override Task<Empty> UpdatePropertyValue(Pointer.ViewModels.Protos.UpdatePropertyValueRequest request, ServerCallContext context)
    {
        _dispatcher.Invoke(() => {
            var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);
            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                try {
                    if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);
                    else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);
                    else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);
                    else { Debug.WriteLine("[GrpcService:PointerViewModel] UpdatePropertyValue: Unpacking not implemented for property " + request.PropertyName + " and type " + request.NewValue.TypeUrl + "."); }
                } catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error setting property " + request.PropertyName + ": " + ex.Message); }
            }
            else { Debug.WriteLine("[GrpcService:PointerViewModel] UpdatePropertyValue: Property " + request.PropertyName + " not found or not writable."); }
        });
        return Task.FromResult(new Empty());
    }

    public override Task<ConnectionStatusResponse> Ping(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
    {
        return Task.FromResult(new ConnectionStatusResponse { Status = ConnectionStatus.Connected });
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) return;
        object? newValue = null;
        try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error getting property value for " + e.PropertyName + ": " + ex.Message); return; }

        var notification = new Pointer.ViewModels.Protos.PropertyChangeNotification { PropertyName = e.PropertyName };
        if (newValue == null) notification.NewValue = Any.Pack(new Empty());
        else if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });
        else if (newValue is int i) notification.NewValue = Any.Pack(new Int32Value { Value = i });
        else if (newValue is bool b) notification.NewValue = Any.Pack(new BoolValue { Value = b });
        else if (newValue is double d) notification.NewValue = Any.Pack(new DoubleValue { Value = d });
        else if (newValue is float f) notification.NewValue = Any.Pack(new FloatValue { Value = f });
        else if (newValue is long l) notification.NewValue = Any.Pack(new Int64Value { Value = l });
        else if (newValue is DateTime dt) notification.NewValue = Any.Pack(Timestamp.FromDateTime(dt.ToUniversalTime()));
        else { Debug.WriteLine($"[GrpcService:PointerViewModel] PropertyChanged: Packing not implemented for type {(newValue?.GetType().FullName ?? "null")} of property {e.PropertyName}."); notification.NewValue = Any.Pack(new StringValue { Value = newValue.ToString() }); }

        foreach (var channelWriter in _subscriberChannels.Values.Select(c => c.Writer))
        {
            try { await channelWriter.WriteAsync(notification); }
            catch (ChannelClosedException) { Debug.WriteLine("[GrpcService:PointerViewModel] Channel closed for a subscriber, cannot write notification for '" + e.PropertyName + "'. Subscriber likely disconnected."); }
            catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error writing to subscriber channel for '" + e.PropertyName + "': " + ex.Message); }
        }
    }
}
