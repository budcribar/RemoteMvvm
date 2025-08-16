// Client Proxy ViewModel for ThermalZoneComponentViewModel

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
    public partial class ThermalZoneComponentViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly Generated.Protos.ThermalZoneComponentViewModelService.ThermalZoneComponentViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }


        public ThermalZoneComponentViewModelRemoteClient(Generated.Protos.ThermalZoneComponentViewModelService.ThermalZoneComponentViewModelServiceClient grpcClient)
        {
            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
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
            Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Initial state received.");
                _isInitialized = true;
                Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:ThermalZoneComponentViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:ThermalZoneComponentViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:ThermalZoneComponentViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new Generated.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[ThermalZoneComponentViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   default: Debug.WriteLine($"[ClientProxy:ThermalZoneComponentViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:ThermalZoneComponentViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:ThermalZoneComponentViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:ThermalZoneComponentViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:ThermalZoneComponentViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[ThermalZoneComponentViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
