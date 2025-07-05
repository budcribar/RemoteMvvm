using Grpc.Core;
using MonsterClicker.ViewModels.Protos;
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

public partial class GameViewModelGrpcServiceImpl : GameViewModelService.GameViewModelServiceBase
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

    static GameViewModelGrpcServiceImpl()
    {
        ClientCount = 0;
    }

    private readonly GameViewModel _viewModel;
    private static readonly ConcurrentDictionary<IServerStreamWriter<MonsterClicker.ViewModels.Protos.PropertyChangeNotification>, Channel<MonsterClicker.ViewModels.Protos.PropertyChangeNotification>> _subscriberChannels = new ConcurrentDictionary<IServerStreamWriter<MonsterClicker.ViewModels.Protos.PropertyChangeNotification>, Channel<MonsterClicker.ViewModels.Protos.PropertyChangeNotification>>();
    private readonly Dispatcher _dispatcher;

    public GameViewModelGrpcServiceImpl(GameViewModel viewModel, Dispatcher dispatcher)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        if (_viewModel is INotifyPropertyChanged inpc) { inpc.PropertyChanged += ViewModel_PropertyChanged; }
    }

    public override Task<GameViewModelState> GetState(Empty request, ServerCallContext context)
    {
        var state = new GameViewModelState();
        // Mapping property: MonsterName to state.MonsterName
        try
        {
            var propValue = _viewModel.MonsterName;
            state.MonsterName = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property MonsterName to state.MonsterName: " + ex.Message); }
        // Mapping property: MonsterMaxHealth to state.MonsterMaxHealth
        try
        {
            var propValue = _viewModel.MonsterMaxHealth;
            state.MonsterMaxHealth = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property MonsterMaxHealth to state.MonsterMaxHealth: " + ex.Message); }
        // Mapping property: MonsterCurrentHealth to state.MonsterCurrentHealth
        try
        {
            var propValue = _viewModel.MonsterCurrentHealth;
            state.MonsterCurrentHealth = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property MonsterCurrentHealth to state.MonsterCurrentHealth: " + ex.Message); }
        // Mapping property: PlayerDamage to state.PlayerDamage
        try
        {
            var propValue = _viewModel.PlayerDamage;
            state.PlayerDamage = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property PlayerDamage to state.PlayerDamage: " + ex.Message); }
        // Mapping property: GameMessage to state.GameMessage
        try
        {
            var propValue = _viewModel.GameMessage;
            state.GameMessage = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property GameMessage to state.GameMessage: " + ex.Message); }
        // Mapping property: IsMonsterDefeated to state.IsMonsterDefeated
        try
        {
            var propValue = _viewModel.IsMonsterDefeated;
            state.IsMonsterDefeated = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property IsMonsterDefeated to state.IsMonsterDefeated: " + ex.Message); }
        // Mapping property: CanUseSpecialAttack to state.CanUseSpecialAttack
        try
        {
            var propValue = _viewModel.CanUseSpecialAttack;
            state.CanUseSpecialAttack = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property CanUseSpecialAttack to state.CanUseSpecialAttack: " + ex.Message); }
        // Mapping property: IsSpecialAttackOnCooldown to state.IsSpecialAttackOnCooldown
        try
        {
            var propValue = _viewModel.IsSpecialAttackOnCooldown;
            state.IsSpecialAttackOnCooldown = propValue;
        }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error mapping property IsSpecialAttackOnCooldown to state.IsSpecialAttackOnCooldown: " + ex.Message); }
        return Task.FromResult(state);
    }

    public override async Task SubscribeToPropertyChanges(MonsterClicker.ViewModels.Protos.SubscribeRequest request, IServerStreamWriter<MonsterClicker.ViewModels.Protos.PropertyChangeNotification> responseStream, ServerCallContext context)
    {
        var channel = Channel.CreateUnbounded<MonsterClicker.ViewModels.Protos.PropertyChangeNotification>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
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

    public override Task<Empty> UpdatePropertyValue(MonsterClicker.ViewModels.Protos.UpdatePropertyValueRequest request, ServerCallContext context)
    {
        _dispatcher.Invoke(() => {
            var propertyInfo = _viewModel.GetType().GetProperty(request.PropertyName);
            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                try {
                    if (request.NewValue.Is(StringValue.Descriptor) && propertyInfo.PropertyType == typeof(string)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<StringValue>().Value);
                    else if (request.NewValue.Is(Int32Value.Descriptor) && propertyInfo.PropertyType == typeof(int)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<Int32Value>().Value);
                    else if (request.NewValue.Is(BoolValue.Descriptor) && propertyInfo.PropertyType == typeof(bool)) propertyInfo.SetValue(_viewModel, request.NewValue.Unpack<BoolValue>().Value);
                    else { Debug.WriteLine("[GrpcService:GameViewModel] UpdatePropertyValue: Unpacking not implemented for property " + request.PropertyName + " and type " + request.NewValue.TypeUrl + "."); }
                } catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error setting property " + request.PropertyName + ": " + ex.Message); }
            }
            else { Debug.WriteLine("[GrpcService:GameViewModel] UpdatePropertyValue: Property " + request.PropertyName + " not found or not writable."); }
        });
        return Task.FromResult(new Empty());
    }

    public override Task<ConnectionStatusResponse> Ping(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
    {
        return Task.FromResult(new ConnectionStatusResponse { Status = ConnectionStatus.Connected });
    }

    public override async Task<MonsterClicker.ViewModels.Protos.AttackMonsterResponse> AttackMonster(MonsterClicker.ViewModels.Protos.AttackMonsterRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            var command = _viewModel.AttackMonsterCommand as CommunityToolkit.Mvvm.Input.IRelayCommand;
            if (command != null)
            {
                command.Execute(null);
            }
            else { Debug.WriteLine("[GrpcService:GameViewModel] Command AttackMonsterCommand not found or not IRelayCommand."); }
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:GameViewModel] Exception during command execution for AttackMonster: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new MonsterClicker.ViewModels.Protos.AttackMonsterResponse();
    }

    public override async Task<MonsterClicker.ViewModels.Protos.SpecialAttackAsyncResponse> SpecialAttackAsync(MonsterClicker.ViewModels.Protos.SpecialAttackAsyncRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            var command = _viewModel.SpecialAttackCommand as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
            if (command != null)
            {
                await command.ExecuteAsync(null);
            }
            else { Debug.WriteLine("[GrpcService:GameViewModel] Command SpecialAttackCommand not found or not IAsyncRelayCommand."); }
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:GameViewModel] Exception during command execution for SpecialAttackAsync: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new MonsterClicker.ViewModels.Protos.SpecialAttackAsyncResponse();
    }

    public override async Task<MonsterClicker.ViewModels.Protos.ResetGameResponse> ResetGame(MonsterClicker.ViewModels.Protos.ResetGameRequest request, ServerCallContext context)
    {
        try { await await _dispatcher.InvokeAsync(async () => {
            var command = _viewModel.ResetGameCommand as CommunityToolkit.Mvvm.Input.IRelayCommand;
            if (command != null)
            {
                command.Execute(null);
            }
            else { Debug.WriteLine("[GrpcService:GameViewModel] Command ResetGameCommand not found or not IRelayCommand."); }
        }); } catch (Exception ex) {
        Debug.WriteLine("[GrpcService:GameViewModel] Exception during command execution for ResetGame: " + ex.ToString());
        throw new RpcException(new Status(StatusCode.Internal, "Error executing command on server: " + ex.Message));
        }
        return new MonsterClicker.ViewModels.Protos.ResetGameResponse();
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) return;
        object? newValue = null;
        try { newValue = sender?.GetType().GetProperty(e.PropertyName)?.GetValue(sender); }
        catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error getting property value for " + e.PropertyName + ": " + ex.Message); return; }

        var notification = new MonsterClicker.ViewModels.Protos.PropertyChangeNotification { PropertyName = e.PropertyName };
        if (newValue == null) notification.NewValue = Any.Pack(new Empty());
        else if (newValue is string s) notification.NewValue = Any.Pack(new StringValue { Value = s });
        else if (newValue is int i) notification.NewValue = Any.Pack(new Int32Value { Value = i });
        else if (newValue is bool b) notification.NewValue = Any.Pack(new BoolValue { Value = b });
        else if (newValue is double d) notification.NewValue = Any.Pack(new DoubleValue { Value = d });
        else if (newValue is float f) notification.NewValue = Any.Pack(new FloatValue { Value = f });
        else if (newValue is long l) notification.NewValue = Any.Pack(new Int64Value { Value = l });
        else if (newValue is DateTime dt) notification.NewValue = Any.Pack(Timestamp.FromDateTime(dt.ToUniversalTime()));
        else { Debug.WriteLine($"[GrpcService:GameViewModel] PropertyChanged: Packing not implemented for type {(newValue?.GetType().FullName ?? "null")} of property {e.PropertyName}."); notification.NewValue = Any.Pack(new StringValue { Value = newValue.ToString() }); }

        foreach (var channelWriter in _subscriberChannels.Values.Select(c => c.Writer))
        {
            try { await channelWriter.WriteAsync(notification); }
            catch (ChannelClosedException) { Debug.WriteLine("[GrpcService:GameViewModel] Channel closed for a subscriber, cannot write notification for '" + e.PropertyName + "'. Subscriber likely disconnected."); }
            catch (Exception ex) { Debug.WriteLine("[GrpcService:GameViewModel] Error writing to subscriber channel for '" + e.PropertyName + "': " + ex.Message); }
        }
    }
}
