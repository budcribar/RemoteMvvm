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
        // Mapping property: Show to state.Show
        try
        {
            var propValue = _viewModel.Show;
            state.Show = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property Show to state.Show: " + ex.Message); }
        // Mapping property: ShowSpinner to state.ShowSpinner
        try
        {
            var propValue = _viewModel.ShowSpinner;
            state.ShowSpinner = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowSpinner to state.ShowSpinner: " + ex.Message); }
        // Mapping property: ClicksToPass to state.ClicksToPass
        try
        {
            var propValue = _viewModel.ClicksToPass;
            state.ClicksToPass = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ClicksToPass to state.ClicksToPass: " + ex.Message); }
        // Mapping property: Is3Btn to state.Is3Btn
        try
        {
            var propValue = _viewModel.Is3Btn;
            state.Is3Btn = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property Is3Btn to state.Is3Btn: " + ex.Message); }
        // Mapping property: TestTimeoutSec to state.TestTimeoutSec
        try
        {
            var propValue = _viewModel.TestTimeoutSec;
            state.TestTimeoutSec = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property TestTimeoutSec to state.TestTimeoutSec: " + ex.Message); }
        // Mapping property: Instructions to state.Instructions
        try
        {
            var propValue = _viewModel.Instructions;
            state.Instructions = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property Instructions to state.Instructions: " + ex.Message); }
        // Mapping property: ShowCursorTest to state.ShowCursorTest
        try
        {
            var propValue = _viewModel.ShowCursorTest;
            state.ShowCursorTest = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowCursorTest to state.ShowCursorTest: " + ex.Message); }
        // Mapping property: ShowConfigSelection to state.ShowConfigSelection
        try
        {
            var propValue = _viewModel.ShowConfigSelection;
            state.ShowConfigSelection = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowConfigSelection to state.ShowConfigSelection: " + ex.Message); }
        // Mapping property: ShowClickInstructions to state.ShowClickInstructions
        try
        {
            var propValue = _viewModel.ShowClickInstructions;
            state.ShowClickInstructions = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowClickInstructions to state.ShowClickInstructions: " + ex.Message); }
        // Mapping property: ShowTimer to state.ShowTimer
        try
        {
            var propValue = _viewModel.ShowTimer;
            state.ShowTimer = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowTimer to state.ShowTimer: " + ex.Message); }
        // Mapping property: ShowBottom to state.ShowBottom
        try
        {
            var propValue = _viewModel.ShowBottom;
            state.ShowBottom = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property ShowBottom to state.ShowBottom: " + ex.Message); }
        // Mapping property: TimerText to state.TimerText
        try
        {
            var propValue = _viewModel.TimerText;
            state.TimerText = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property TimerText to state.TimerText: " + ex.Message); }
        // Mapping property: SelectedDevice to state.SelectedDevice
        try
        {
            var propValue = _viewModel.SelectedDevice;
            state.SelectedDevice = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property SelectedDevice to state.SelectedDevice: " + ex.Message); }
        // Mapping property: LastClickCount to state.LastClickCount
        try
        {
            var propValue = _viewModel.LastClickCount;
            state.LastClickCount = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:PointerViewModel] Error mapping property LastClickCount to state.LastClickCount: " + ex.Message); }
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

    public override async Task<Pointer.ViewModels.Protos.InitializeResponse> Initialize(Pointer.ViewModels.Protos.InitializeRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.Initialize();
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for Initialize: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.InitializeResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.OnCursorTestResponse> OnCursorTest(Pointer.ViewModels.Protos.OnCursorTestRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.OnCursorTest();
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for OnCursorTest: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.OnCursorTestResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.OnClickTestResponse> OnClickTest(Pointer.ViewModels.Protos.OnClickTestRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.OnClickTest(request.Button);
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for OnClickTest: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.OnClickTestResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.OnSelectDeviceResponse> OnSelectDevice(Pointer.ViewModels.Protos.OnSelectDeviceRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.OnSelectDevice(request.Device);
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for OnSelectDevice: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.OnSelectDeviceResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.OnSelectNumButtonsResponse> OnSelectNumButtons(Pointer.ViewModels.Protos.OnSelectNumButtonsRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.OnSelectNumButtons(request.BtnCount);
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for OnSelectNumButtons: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.OnSelectNumButtonsResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.GetClicksWithoutNotificationResponse> GetClicksWithoutNotification(Pointer.ViewModels.Protos.GetClicksWithoutNotificationRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.GetClicksWithoutNotification(request.Button);
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for GetClicksWithoutNotification: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.GetClicksWithoutNotificationResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.ResetClicksResponse> ResetClicks(Pointer.ViewModels.Protos.ResetClicksRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.ResetClicks();
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for ResetClicks: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.ResetClicksResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.CancelTestResponse> CancelTest(Pointer.ViewModels.Protos.CancelTestRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.CancelTest();
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for CancelTest: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.CancelTestResponse();
    }

    public override async Task<Pointer.ViewModels.Protos.FinishTestResponse> FinishTest(Pointer.ViewModels.Protos.FinishTestRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            _viewModel.FinishTest();
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:PointerViewModel] Exception during command execution for FinishTest: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new Pointer.ViewModels.Protos.FinishTestResponse();
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
