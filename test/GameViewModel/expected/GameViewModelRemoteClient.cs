// Client Proxy ViewModel for GameViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using MonsterClicker.ViewModels.Protos;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
#if WPF_DISPATCHER
using System.Windows;
#endif

namespace MonsterClicker.ViewModels.RemoteClients
{
    public partial class GameViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly MonsterClicker.ViewModels.Protos.GameViewModelService.GameViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private string _monsterName = default!;
        public string MonsterName
        {
            get => _monsterName;
            private set => SetProperty(ref _monsterName, value);
        }

        private int _monsterMaxHealth = default!;
        public int MonsterMaxHealth
        {
            get => _monsterMaxHealth;
            private set => SetProperty(ref _monsterMaxHealth, value);
        }

        private int _monsterCurrentHealth = default!;
        public int MonsterCurrentHealth
        {
            get => _monsterCurrentHealth;
            private set => SetProperty(ref _monsterCurrentHealth, value);
        }

        private int _playerDamage = default!;
        public int PlayerDamage
        {
            get => _playerDamage;
            private set => SetProperty(ref _playerDamage, value);
        }

        private string _gameMessage = default!;
        public string GameMessage
        {
            get => _gameMessage;
            private set => SetProperty(ref _gameMessage, value);
        }

        private bool _isMonsterDefeated = default!;
        public bool IsMonsterDefeated
        {
            get => _isMonsterDefeated;
            private set => SetProperty(ref _isMonsterDefeated, value);
        }

        private bool _canUseSpecialAttack = default!;
        public bool CanUseSpecialAttack
        {
            get => _canUseSpecialAttack;
            private set => SetProperty(ref _canUseSpecialAttack, value);
        }

        private bool _isSpecialAttackOnCooldown = default!;
        public bool IsSpecialAttackOnCooldown
        {
            get => _isSpecialAttackOnCooldown;
            private set => SetProperty(ref _isSpecialAttackOnCooldown, value);
        }

        public IRelayCommand AttackMonsterCommand { get; }
        public IAsyncRelayCommand SpecialAttackCommand { get; }
        public IRelayCommand ResetGameCommand { get; }

        public GameViewModelRemoteClient(MonsterClicker.ViewModels.Protos.GameViewModelService.GameViewModelServiceClient grpcClient)
        {
            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
            AttackMonsterCommand = new RelayCommand(RemoteExecute_AttackMonster);
            SpecialAttackCommand = new AsyncRelayCommand(RemoteExecute_SpecialAttackAsyncAsync);
            ResetGameCommand = new RelayCommand(RemoteExecute_ResetGame);
        }

        private async Task StartPingLoopAsync()
        {
            string lastStatus = ConnectionStatus;
            while (!_isDisposed)
            {
                try
                {
                    var response = await _grpcClient.PingAsync(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: _cts.Token);
                    if (response.Status == MonsterClicker.ViewModels.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.MonsterName = state.MonsterName;
                                this.MonsterMaxHealth = state.MonsterMaxHealth;
                                this.MonsterCurrentHealth = state.MonsterCurrentHealth;
                                this.PlayerDamage = state.PlayerDamage;
                                this.GameMessage = state.GameMessage;
                                this.IsMonsterDefeated = state.IsMonsterDefeated;
                                this.CanUseSpecialAttack = state.CanUseSpecialAttack;
                                this.IsSpecialAttackOnCooldown = state.IsSpecialAttackOnCooldown;
                                Debug.WriteLine("[ClientProxy] State re-synced after reconnect.");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ClientProxy] Error re-syncing state after reconnect: {ex.Message}");
                            }
                        }
                        ConnectionStatus = "Connected";
                        lastStatus = "Connected";
                    }
                    else
                    {
                        ConnectionStatus = "Disconnected";
                        lastStatus = "Disconnected";
                    }
                }
                catch (Exception ex)
                {
                    ConnectionStatus = "Disconnected";
                    lastStatus = "Disconnected";
                    Debug.WriteLine($"[ClientProxy] Ping failed: {ex.Message}. Attempting to reconnect...");
                }
                await Task.Delay(5000);
            }
        }

        public async Task InitializeRemoteAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized || _isDisposed) return;
            Debug.WriteLine("[GameViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[GameViewModelRemoteClient] Initial state received.");
                this.MonsterName = state.MonsterName;
                this.MonsterMaxHealth = state.MonsterMaxHealth;
                this.MonsterCurrentHealth = state.MonsterCurrentHealth;
                this.PlayerDamage = state.PlayerDamage;
                this.GameMessage = state.GameMessage;
                this.IsMonsterDefeated = state.IsMonsterDefeated;
                this.CanUseSpecialAttack = state.CanUseSpecialAttack;
                this.IsSpecialAttackOnCooldown = state.IsSpecialAttackOnCooldown;
                _isInitialized = true;
                Debug.WriteLine("[GameViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:GameViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void RemoteExecute_AttackMonster()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:GameViewModel] Not initialized or disposed, command AttackMonster skipped."); return; }
            Debug.WriteLine("[ClientProxy:GameViewModel] Executing command AttackMonster remotely...");
            try
            {
                _ = _grpcClient.AttackMonsterAsync(new MonsterClicker.ViewModels.Protos.AttackMonsterRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Error executing command AttackMonster: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:GameViewModel] Command AttackMonster cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Unexpected error executing command AttackMonster: " + ex.Message); }
        }

        private async Task RemoteExecute_SpecialAttackAsyncAsync()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:GameViewModel] Not initialized or disposed, command SpecialAttackAsync skipped."); return; }
            Debug.WriteLine("[ClientProxy:GameViewModel] Executing command SpecialAttackAsync remotely...");
            try
            {
                await _grpcClient.SpecialAttackAsyncAsync(new MonsterClicker.ViewModels.Protos.SpecialAttackAsyncRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Error executing command SpecialAttackAsync: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:GameViewModel] Command SpecialAttackAsync cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Unexpected error executing command SpecialAttackAsync: " + ex.Message); }
        }

        private void RemoteExecute_ResetGame()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:GameViewModel] Not initialized or disposed, command ResetGame skipped."); return; }
            Debug.WriteLine("[ClientProxy:GameViewModel] Executing command ResetGame remotely...");
            try
            {
                _ = _grpcClient.ResetGameAsync(new MonsterClicker.ViewModels.Protos.ResetGameRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Error executing command ResetGame: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:GameViewModel] Command ResetGame cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:GameViewModel] Unexpected error executing command ResetGame: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[GameViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new MonsterClicker.ViewModels.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[GameViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[GameViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[GameViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[GameViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   case nameof(MonsterName):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating MonsterName from \"{this.MonsterName}\" to '\"{val}\"."); this.MonsterName = val; Debug.WriteLine($"After update, MonsterName is '\"{this.MonsterName}\"."); } else { Debug.WriteLine($"Mismatched descriptor for MonsterName, expected StringValue."); } break;
                                   case nameof(MonsterMaxHealth):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating MonsterMaxHealth from {this.MonsterMaxHealth} to {val}."); this.MonsterMaxHealth = val; Debug.WriteLine($"After update, MonsterMaxHealth is {this.MonsterMaxHealth}."); } else { Debug.WriteLine($"Mismatched descriptor for MonsterMaxHealth, expected Int32Value."); } break;
                                   case nameof(MonsterCurrentHealth):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating MonsterCurrentHealth from {this.MonsterCurrentHealth} to {val}."); this.MonsterCurrentHealth = val; Debug.WriteLine($"After update, MonsterCurrentHealth is {this.MonsterCurrentHealth}."); } else { Debug.WriteLine($"Mismatched descriptor for MonsterCurrentHealth, expected Int32Value."); } break;
                                   case nameof(PlayerDamage):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating PlayerDamage from {this.PlayerDamage} to {val}."); this.PlayerDamage = val; Debug.WriteLine($"After update, PlayerDamage is {this.PlayerDamage}."); } else { Debug.WriteLine($"Mismatched descriptor for PlayerDamage, expected Int32Value."); } break;
                                   case nameof(GameMessage):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating GameMessage from \"{this.GameMessage}\" to '\"{val}\"."); this.GameMessage = val; Debug.WriteLine($"After update, GameMessage is '\"{this.GameMessage}\"."); } else { Debug.WriteLine($"Mismatched descriptor for GameMessage, expected StringValue."); } break;
                                   case nameof(IsMonsterDefeated):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating IsMonsterDefeated from {this.IsMonsterDefeated} to {val}."); this.IsMonsterDefeated = val; Debug.WriteLine($"After update, IsMonsterDefeated is {this.IsMonsterDefeated}."); } else { Debug.WriteLine($"Mismatched descriptor for IsMonsterDefeated, expected BoolValue."); } break;
                                   case nameof(CanUseSpecialAttack):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating CanUseSpecialAttack from {this.CanUseSpecialAttack} to {val}."); this.CanUseSpecialAttack = val; Debug.WriteLine($"After update, CanUseSpecialAttack is {this.CanUseSpecialAttack}."); } else { Debug.WriteLine($"Mismatched descriptor for CanUseSpecialAttack, expected BoolValue."); } break;
                                   case nameof(IsSpecialAttackOnCooldown):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating IsSpecialAttackOnCooldown from {this.IsSpecialAttackOnCooldown} to {val}."); this.IsSpecialAttackOnCooldown = val; Debug.WriteLine($"After update, IsSpecialAttackOnCooldown is {this.IsSpecialAttackOnCooldown}."); } else { Debug.WriteLine($"Mismatched descriptor for IsSpecialAttackOnCooldown, expected BoolValue."); } break;
                                   default: Debug.WriteLine($"[ClientProxy:GameViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:GameViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[GameViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[GameViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:GameViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:GameViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:GameViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[GameViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[GameViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
