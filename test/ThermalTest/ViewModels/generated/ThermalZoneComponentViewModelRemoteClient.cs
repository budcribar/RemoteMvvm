// Client Proxy ViewModel for ThermalZoneComponentViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using ThermalTest.Protos;
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
        private readonly ThermalTest.Protos.ThermalZoneComponentViewModelService.ThermalZoneComponentViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private HP.Telemetry.Zone _zone = default!;
        public HP.Telemetry.Zone Zone
        {
            get => _zone;
            private set => SetProperty(ref _zone, value);
        }

        private bool _isActive = default!;
        public bool IsActive
        {
            get => _isActive;
            private set => SetProperty(ref _isActive, value);
        }

        private string _deviceName = default!;
        public string DeviceName
        {
            get => _deviceName;
            private set => SetProperty(ref _deviceName, value);
        }

        private int _temperature = default!;
        public int Temperature
        {
            get => _temperature;
            private set => SetProperty(ref _temperature, value);
        }

        private int _processorLoad = default!;
        public int ProcessorLoad
        {
            get => _processorLoad;
            private set => SetProperty(ref _processorLoad, value);
        }

        private int _fanSpeed = default!;
        public int FanSpeed
        {
            get => _fanSpeed;
            private set => SetProperty(ref _fanSpeed, value);
        }

        private int _secondsInState = default!;
        public int SecondsInState
        {
            get => _secondsInState;
            private set => SetProperty(ref _secondsInState, value);
        }

        private System.DateTime _firstSeenInState = default!;
        public System.DateTime FirstSeenInState
        {
            get => _firstSeenInState;
            private set => SetProperty(ref _firstSeenInState, value);
        }

        private int _progress = default!;
        public int Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }

        private string _background = default!;
        public string Background
        {
            get => _background;
            private set => SetProperty(ref _background, value);
        }

        private HPSystemsTools.ViewModels.ThermalStateEnum _status = default!;
        public HPSystemsTools.ViewModels.ThermalStateEnum Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        private HPSystemsTools.ViewModels.ThermalStateEnum _state = default!;
        public HPSystemsTools.ViewModels.ThermalStateEnum State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }


        public ThermalZoneComponentViewModelRemoteClient(ThermalTest.Protos.ThermalZoneComponentViewModelService.ThermalZoneComponentViewModelServiceClient grpcClient)
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
                    if (response.Status == ThermalTest.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.Zone = state.Zone;
                                this.IsActive = state.IsActive;
                                this.DeviceName = state.DeviceName;
                                this.Temperature = state.Temperature;
                                this.ProcessorLoad = state.ProcessorLoad;
                                this.FanSpeed = state.FanSpeed;
                                this.SecondsInState = state.SecondsInState;
                                this.FirstSeenInState = state.FirstSeenInState;
                                this.Progress = state.Progress;
                                this.Background = state.Background;
                                this.Status = state.Status;
                                this.State = state.State;
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
                this.Zone = state.Zone;
                this.IsActive = state.IsActive;
                this.DeviceName = state.DeviceName;
                this.Temperature = state.Temperature;
                this.ProcessorLoad = state.ProcessorLoad;
                this.FanSpeed = state.FanSpeed;
                this.SecondsInState = state.SecondsInState;
                this.FirstSeenInState = state.FirstSeenInState;
                this.Progress = state.Progress;
                this.Background = state.Background;
                this.Status = state.Status;
                this.State = state.State;
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
                    var subscribeRequest = new ThermalTest.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
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
                                   case nameof(Zone):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating Zone from {this.Zone} to {val}."); this.Zone = val; Debug.WriteLine($"After update, Zone is {this.Zone}."); } else { Debug.WriteLine($"Mismatched descriptor for Zone, expected Int32Value."); } break;
                                   case nameof(IsActive):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating IsActive from {this.IsActive} to {val}."); this.IsActive = val; Debug.WriteLine($"After update, IsActive is {this.IsActive}."); } else { Debug.WriteLine($"Mismatched descriptor for IsActive, expected BoolValue."); } break;
                                   case nameof(DeviceName):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating DeviceName from \"{this.DeviceName}\" to '\"{val}\"."); this.DeviceName = val; Debug.WriteLine($"After update, DeviceName is '\"{this.DeviceName}\"."); } else { Debug.WriteLine($"Mismatched descriptor for DeviceName, expected StringValue."); } break;
                                   case nameof(Temperature):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating Temperature from {this.Temperature} to {val}."); this.Temperature = val; Debug.WriteLine($"After update, Temperature is {this.Temperature}."); } else { Debug.WriteLine($"Mismatched descriptor for Temperature, expected Int32Value."); } break;
                                   case nameof(ProcessorLoad):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating ProcessorLoad from {this.ProcessorLoad} to {val}."); this.ProcessorLoad = val; Debug.WriteLine($"After update, ProcessorLoad is {this.ProcessorLoad}."); } else { Debug.WriteLine($"Mismatched descriptor for ProcessorLoad, expected Int32Value."); } break;
                                   case nameof(FanSpeed):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating FanSpeed from {this.FanSpeed} to {val}."); this.FanSpeed = val; Debug.WriteLine($"After update, FanSpeed is {this.FanSpeed}."); } else { Debug.WriteLine($"Mismatched descriptor for FanSpeed, expected Int32Value."); } break;
                                   case nameof(SecondsInState):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating SecondsInState from {this.SecondsInState} to {val}."); this.SecondsInState = val; Debug.WriteLine($"After update, SecondsInState is {this.SecondsInState}."); } else { Debug.WriteLine($"Mismatched descriptor for SecondsInState, expected Int32Value."); } break;
                                   case nameof(FirstSeenInState):
                                       Debug.WriteLine($"[ClientProxy:ThermalZoneComponentViewModel] Unpacking for FirstSeenInState with WKT Timestamp not fully implemented or is Any."); break;
                                   case nameof(Progress):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating Progress from {this.Progress} to {val}."); this.Progress = val; Debug.WriteLine($"After update, Progress is {this.Progress}."); } else { Debug.WriteLine($"Mismatched descriptor for Progress, expected Int32Value."); } break;
                                   case nameof(Background):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating Background from \"{this.Background}\" to '\"{val}\"."); this.Background = val; Debug.WriteLine($"After update, Background is '\"{this.Background}\"."); } else { Debug.WriteLine($"Mismatched descriptor for Background, expected StringValue."); } break;
                                   case nameof(Status):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating Status from {this.Status} to {val}."); this.Status = val; Debug.WriteLine($"After update, Status is {this.Status}."); } else { Debug.WriteLine($"Mismatched descriptor for Status, expected Int32Value."); } break;
                                   case nameof(State):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating State from {this.State} to {val}."); this.State = val; Debug.WriteLine($"After update, State is {this.State}."); } else { Debug.WriteLine($"Mismatched descriptor for State, expected Int32Value."); } break;
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
