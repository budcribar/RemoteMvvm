// Client Proxy ViewModel for SampleViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using SampleApp.ViewModels.Protos;
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

namespace SampleApp.ViewModels.RemoteClients
{
    public partial class SampleViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly SampleApp.ViewModels.Protos.CounterService.CounterServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private string _name = default!;
        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        private int _count = default!;
        public int Count
        {
            get => _count;
            private set => SetProperty(ref _count, value);
        }

        public IRelayCommand IncrementCountCommand { get; }
        public IAsyncRelayCommand<int> DelayedIncrementCommand { get; }
        public IRelayCommand<string?> SetNameToValueCommand { get; }

        public SampleViewModelRemoteClient(SampleApp.ViewModels.Protos.CounterService.CounterServiceClient grpcClient)
        {
            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
            IncrementCountCommand = new RelayCommand(RemoteExecute_IncrementCount);
            DelayedIncrementCommand = new AsyncRelayCommand<int>(RemoteExecute_DelayedIncrementAsyncAsync);
            SetNameToValueCommand = new RelayCommand<string?>(RemoteExecute_SetNameToValue);
        }

        private async Task StartPingLoopAsync()
        {
            string lastStatus = ConnectionStatus;
            while (!_isDisposed)
            {
                try
                {
                    var response = await _grpcClient.PingAsync(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: _cts.Token);
                    if (response.Status == SampleApp.ViewModels.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.Name = state.Name;
                                this.Count = state.Count;
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
            Debug.WriteLine("[SampleViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[SampleViewModelRemoteClient] Initial state received.");
                this.Name = state.Name;
                this.Count = state.Count;
                _isInitialized = true;
                Debug.WriteLine("[SampleViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:SampleViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void RemoteExecute_IncrementCount()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:SampleViewModel] Not initialized or disposed, command IncrementCount skipped."); return; }
            Debug.WriteLine("[ClientProxy:SampleViewModel] Executing command IncrementCount remotely...");
            try
            {
                _ = _grpcClient.IncrementCountAsync(new SampleApp.ViewModels.Protos.IncrementCountRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Error executing command IncrementCount: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:SampleViewModel] Command IncrementCount cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Unexpected error executing command IncrementCount: " + ex.Message); }
        }

        private async Task RemoteExecute_DelayedIncrementAsyncAsync(int delayMilliseconds)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:SampleViewModel] Not initialized or disposed, command DelayedIncrementAsync skipped."); return; }
            Debug.WriteLine("[ClientProxy:SampleViewModel] Executing command DelayedIncrementAsync remotely...");
            try
            {
                await _grpcClient.DelayedIncrementAsyncAsync(new SampleApp.ViewModels.Protos.DelayedIncrementAsyncRequest { DelayMilliseconds = delayMilliseconds }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Error executing command DelayedIncrementAsync: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:SampleViewModel] Command DelayedIncrementAsync cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Unexpected error executing command DelayedIncrementAsync: " + ex.Message); }
        }

        private void RemoteExecute_SetNameToValue(string? value)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:SampleViewModel] Not initialized or disposed, command SetNameToValue skipped."); return; }
            Debug.WriteLine("[ClientProxy:SampleViewModel] Executing command SetNameToValue remotely...");
            try
            {
                _ = _grpcClient.SetNameToValueAsync(new SampleApp.ViewModels.Protos.SetNameToValueRequest { Value = value }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Error executing command SetNameToValue: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:SampleViewModel] Command SetNameToValue cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:SampleViewModel] Unexpected error executing command SetNameToValue: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[SampleViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new SampleApp.ViewModels.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[SampleViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[SampleViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[SampleViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[SampleViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   case nameof(Name):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating Name from \"{this.Name}\" to '\"{val}\"."); this.Name = val; Debug.WriteLine($"After update, Name is '\"{this.Name}\"."); } else { Debug.WriteLine($"Mismatched descriptor for Name, expected StringValue."); } break;
                                   case nameof(Count):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating Count from {this.Count} to {val}."); this.Count = val; Debug.WriteLine($"After update, Count is {this.Count}."); } else { Debug.WriteLine($"Mismatched descriptor for Count, expected Int32Value."); } break;
                                   default: Debug.WriteLine($"[ClientProxy:SampleViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:SampleViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[SampleViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[SampleViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:SampleViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:SampleViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:SampleViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[SampleViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[SampleViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
