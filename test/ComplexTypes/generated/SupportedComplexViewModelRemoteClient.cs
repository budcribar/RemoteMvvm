// Client Proxy ViewModel for SupportedComplexViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using ComplexTypes.Protos;
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

namespace ComplexTypes.ViewModels.RemoteClients
{
    public partial class SupportedComplexViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly ComplexTypes.Protos.SupportedComplexViewModelService.SupportedComplexViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private System.Collections.Generic.Dictionary<int, ComplexTypes.ViewModels.SecondLevel> _layers = default!;
        public System.Collections.Generic.Dictionary<int, ComplexTypes.ViewModels.SecondLevel> Layers
        {
            get => _layers;
            private set => SetProperty(ref _layers, value);
        }


        public SupportedComplexViewModelRemoteClient(ComplexTypes.Protos.SupportedComplexViewModelService.SupportedComplexViewModelServiceClient grpcClient)
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
                    if (response.Status == ComplexTypes.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.Layers = state.Layers.ToDictionary(k => k.Key, v => v.Value);
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
            Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Initial state received.");
                this.Layers = state.Layers.ToDictionary(k => k.Key, v => v.Value);
                _isInitialized = true;
                Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:SupportedComplexViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:SupportedComplexViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:SupportedComplexViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new ComplexTypes.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[SupportedComplexViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   case nameof(Layers):
                                       Debug.WriteLine($"[ClientProxy:SupportedComplexViewModel] Unpacking for Layers with WKT Any not fully implemented or is Any."); break;
                                   default: Debug.WriteLine($"[ClientProxy:SupportedComplexViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:SupportedComplexViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[SupportedComplexViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:SupportedComplexViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:SupportedComplexViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:SupportedComplexViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[SupportedComplexViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
