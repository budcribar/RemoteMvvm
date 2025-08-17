// Client Proxy ViewModel for HP3LSThermalTestViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using Generated.Protos;
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

namespace HPSystemsTools.ViewModels.RemoteClients
{
    public partial class HP3LSThermalTestViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly Generated.Protos.HP3LSThermalTestViewModelService.HP3LSThermalTestViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private System.Collections.Generic.Dictionary<Zone, HPSystemsTools.ViewModels.ThermalZoneComponentViewModel> _zones = default!;
        public System.Collections.Generic.Dictionary<Zone, HPSystemsTools.ViewModels.ThermalZoneComponentViewModel> Zones
        {
            get => _zones;
            private set => SetProperty(ref _zones, value);
        }

        private TestSettingsModel _testSettings = default!;
        public TestSettingsModel TestSettings
        {
            get => _testSettings;
            private set => SetProperty(ref _testSettings, value);
        }

        private bool _showDescription = default!;
        public bool ShowDescription
        {
            get => _showDescription;
            private set => SetProperty(ref _showDescription, value);
        }

        private bool _showReadme = default!;
        public bool ShowReadme
        {
            get => _showReadme;
            private set => SetProperty(ref _showReadme, value);
        }

        public IRelayCommand<ThermalStateEnum> StateChangedCommand { get; }
        public IRelayCommand CancelTestCommand { get; }

        public HP3LSThermalTestViewModelRemoteClient(Generated.Protos.HP3LSThermalTestViewModelService.HP3LSThermalTestViewModelServiceClient grpcClient)
        {
            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
            StateChangedCommand = new RelayCommand<ThermalStateEnum>(RemoteExecute_StateChanged);
            CancelTestCommand = new RelayCommand(RemoteExecute_CancelTest);
        }

        private async Task StartPingLoopAsync()
        {
            string lastStatus = ConnectionStatus;
            while (!_isDisposed)
            {
                try
                {
                    var response = await _grpcClient.PingAsync(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: _cts.Token);
                    if (response.Status == Generated.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.Zones = state.Zones;
                                this.TestSettings = state.TestSettings;
                                this.ShowDescription = state.ShowDescription;
                                this.ShowReadme = state.ShowReadme;
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
            Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Initial state received.");
                this.Zones = state.Zones;
                this.TestSettings = state.TestSettings;
                this.ShowDescription = state.ShowDescription;
                this.ShowReadme = state.ShowReadme;
                _isInitialized = true;
                Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void RemoteExecute_StateChanged(ThermalStateEnum state)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Not initialized or disposed, command StateChanged skipped."); return; }
            Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Executing command StateChanged remotely...");
            try
            {
                _ = _grpcClient.StateChangedAsync(new Generated.Protos.StateChangedRequest { State = state }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Error executing command StateChanged: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Command StateChanged cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Unexpected error executing command StateChanged: " + ex.Message); }
        }

        private void RemoteExecute_CancelTest()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Not initialized or disposed, command CancelTest skipped."); return; }
            Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Executing command CancelTest remotely...");
            try
            {
                _ = _grpcClient.CancelTestAsync(new Generated.Protos.CancelTestRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Error executing command CancelTest: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Command CancelTest cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Unexpected error executing command CancelTest: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new Generated.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[HP3LSThermalTestViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   case nameof(Zones):
                                       Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] Unpacking for Zones with WKT Any not fully implemented or is Any."); break;
                                   case nameof(TestSettings):
                                       Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] Unpacking for TestSettings with WKT Any not fully implemented or is Any."); break;
                                   case nameof(ShowDescription):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowDescription from {this.ShowDescription} to {val}."); this.ShowDescription = val; Debug.WriteLine($"After update, ShowDescription is {this.ShowDescription}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowDescription, expected BoolValue."); } break;
                                   case nameof(ShowReadme):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowReadme from {this.ShowReadme} to {val}."); this.ShowReadme = val; Debug.WriteLine($"After update, ShowReadme is {this.ShowReadme}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowReadme, expected BoolValue."); } break;
                                   default: Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:HP3LSThermalTestViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:HP3LSThermalTestViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[HP3LSThermalTestViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
