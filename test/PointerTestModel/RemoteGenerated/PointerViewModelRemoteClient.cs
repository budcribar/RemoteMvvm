// Client Proxy ViewModel for PointerViewModel

#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using Pointer.ViewModels.Protos;
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

namespace HPSystemsTools.RemoteClients
{
    public partial class PointerViewModelRemoteClient : ObservableObject, IDisposable
    {
        private readonly Pointer.ViewModels.Protos.PointerViewModelService.PointerViewModelServiceClient _grpcClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        private string _connectionStatus = "Unknown";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        private bool _show = default!;
        public bool Show
        {
            get => _show;
            private set => SetProperty(ref _show, value);
        }

        private bool _showSpinner = default!;
        public bool ShowSpinner
        {
            get => _showSpinner;
            private set => SetProperty(ref _showSpinner, value);
        }

        private int _clicksToPass = default!;
        public int ClicksToPass
        {
            get => _clicksToPass;
            private set => SetProperty(ref _clicksToPass, value);
        }

        private bool _is3Btn = default!;
        public bool Is3Btn
        {
            get => _is3Btn;
            private set => SetProperty(ref _is3Btn, value);
        }

        private int _testTimeoutSec = default!;
        public int TestTimeoutSec
        {
            get => _testTimeoutSec;
            private set => SetProperty(ref _testTimeoutSec, value);
        }

        private string _instructions = default!;
        public string Instructions
        {
            get => _instructions;
            private set => SetProperty(ref _instructions, value);
        }

        private bool _showCursorTest = default!;
        public bool ShowCursorTest
        {
            get => _showCursorTest;
            private set => SetProperty(ref _showCursorTest, value);
        }

        private bool _showConfigSelection = default!;
        public bool ShowConfigSelection
        {
            get => _showConfigSelection;
            private set => SetProperty(ref _showConfigSelection, value);
        }

        private bool _showClickInstructions = default!;
        public bool ShowClickInstructions
        {
            get => _showClickInstructions;
            private set => SetProperty(ref _showClickInstructions, value);
        }

        private bool _showTimer = default!;
        public bool ShowTimer
        {
            get => _showTimer;
            private set => SetProperty(ref _showTimer, value);
        }

        private bool _showBottom = default!;
        public bool ShowBottom
        {
            get => _showBottom;
            private set => SetProperty(ref _showBottom, value);
        }

        private string _timerText = default!;
        public string TimerText
        {
            get => _timerText;
            private set => SetProperty(ref _timerText, value);
        }

        private string _selectedDevice = default!;
        public string SelectedDevice
        {
            get => _selectedDevice;
            private set => SetProperty(ref _selectedDevice, value);
        }

        private int _lastClickCount = default!;
        public int LastClickCount
        {
            get => _lastClickCount;
            private set => SetProperty(ref _lastClickCount, value);
        }

        public IRelayCommand InitializeCommand { get; }
        public IRelayCommand OnCursorTestCommand { get; }
        public IRelayCommand<int> OnClickTestCommand { get; }
        public IRelayCommand<string> OnSelectDeviceCommand { get; }
        public IRelayCommand<int> OnSelectNumButtonsCommand { get; }
        public IRelayCommand<string> GetClicksWithoutNotificationCommand { get; }
        public IRelayCommand ResetClicksCommand { get; }
        public IRelayCommand CancelTestCommand { get; }
        public IRelayCommand FinishTestCommand { get; }

        public PointerViewModelRemoteClient(Pointer.ViewModels.Protos.PointerViewModelService.PointerViewModelServiceClient grpcClient)
        {
            _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
            InitializeCommand = new RelayCommand(RemoteExecute_Initialize);
            OnCursorTestCommand = new RelayCommand(RemoteExecute_OnCursorTest);
            OnClickTestCommand = new RelayCommand<int>(RemoteExecute_OnClickTest);
            OnSelectDeviceCommand = new RelayCommand<string>(RemoteExecute_OnSelectDevice);
            OnSelectNumButtonsCommand = new RelayCommand<int>(RemoteExecute_OnSelectNumButtons);
            GetClicksWithoutNotificationCommand = new RelayCommand<string>(RemoteExecute_GetClicksWithoutNotification);
            ResetClicksCommand = new RelayCommand(RemoteExecute_ResetClicks);
            CancelTestCommand = new RelayCommand(RemoteExecute_CancelTest);
            FinishTestCommand = new RelayCommand(RemoteExecute_FinishTest);
        }

        private async Task StartPingLoopAsync()
        {
            string lastStatus = ConnectionStatus;
            while (!_isDisposed)
            {
                try
                {
                    var response = await _grpcClient.PingAsync(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: _cts.Token);
                    if (response.Status == Pointer.ViewModels.Protos.ConnectionStatus.Connected)
                    {
                        if (lastStatus != "Connected")
                        {
                            try
                            {
                                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: _cts.Token);
                                this.Show = state.Show;
                                this.ShowSpinner = state.ShowSpinner;
                                this.ClicksToPass = state.ClicksToPass;
                                this.Is3Btn = state.Is3Btn;
                                this.TestTimeoutSec = state.TestTimeoutSec;
                                this.Instructions = state.Instructions;
                                this.ShowCursorTest = state.ShowCursorTest;
                                this.ShowConfigSelection = state.ShowConfigSelection;
                                this.ShowClickInstructions = state.ShowClickInstructions;
                                this.ShowTimer = state.ShowTimer;
                                this.ShowBottom = state.ShowBottom;
                                this.TimerText = state.TimerText;
                                this.SelectedDevice = state.SelectedDevice;
                                this.LastClickCount = state.LastClickCount;
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
            Debug.WriteLine("[PointerViewModelRemoteClient] Initializing...");
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
                var state = await _grpcClient.GetStateAsync(new Empty(), cancellationToken: linkedCts.Token);
                Debug.WriteLine("[PointerViewModelRemoteClient] Initial state received.");
                this.Show = state.Show;
                this.ShowSpinner = state.ShowSpinner;
                this.ClicksToPass = state.ClicksToPass;
                this.Is3Btn = state.Is3Btn;
                this.TestTimeoutSec = state.TestTimeoutSec;
                this.Instructions = state.Instructions;
                this.ShowCursorTest = state.ShowCursorTest;
                this.ShowConfigSelection = state.ShowConfigSelection;
                this.ShowClickInstructions = state.ShowClickInstructions;
                this.ShowTimer = state.ShowTimer;
                this.ShowBottom = state.ShowBottom;
                this.TimerText = state.TimerText;
                this.SelectedDevice = state.SelectedDevice;
                this.LastClickCount = state.LastClickCount;
                _isInitialized = true;
                Debug.WriteLine("[PointerViewModelRemoteClient] Initialized successfully.");
                StartListeningToPropertyChanges(_cts.Token);
                _ = StartPingLoopAsync();
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Failed to initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Initialization cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error during initialization: " + ex.Message); }
        }

        private void RemoteExecute_Initialize()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command Initialize skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command Initialize remotely...");
            try
            {
                _ = _grpcClient.InitializeAsync(new Pointer.ViewModels.Protos.InitializeRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command Initialize: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command Initialize cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command Initialize: " + ex.Message); }
        }

        private void RemoteExecute_OnCursorTest()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command OnCursorTest skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command OnCursorTest remotely...");
            try
            {
                _ = _grpcClient.OnCursorTestAsync(new Pointer.ViewModels.Protos.OnCursorTestRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command OnCursorTest: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command OnCursorTest cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command OnCursorTest: " + ex.Message); }
        }

        private void RemoteExecute_OnClickTest(int button)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command OnClickTest skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command OnClickTest remotely...");
            try
            {
                _ = _grpcClient.OnClickTestAsync(new Pointer.ViewModels.Protos.OnClickTestRequest { Button = button }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command OnClickTest: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command OnClickTest cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command OnClickTest: " + ex.Message); }
        }

        private void RemoteExecute_OnSelectDevice(string device)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command OnSelectDevice skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command OnSelectDevice remotely...");
            try
            {
                _ = _grpcClient.OnSelectDeviceAsync(new Pointer.ViewModels.Protos.OnSelectDeviceRequest { Device = device }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command OnSelectDevice: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command OnSelectDevice cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command OnSelectDevice: " + ex.Message); }
        }

        private void RemoteExecute_OnSelectNumButtons(int btnCount)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command OnSelectNumButtons skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command OnSelectNumButtons remotely...");
            try
            {
                _ = _grpcClient.OnSelectNumButtonsAsync(new Pointer.ViewModels.Protos.OnSelectNumButtonsRequest { BtnCount = btnCount }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command OnSelectNumButtons: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command OnSelectNumButtons cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command OnSelectNumButtons: " + ex.Message); }
        }

        private void RemoteExecute_GetClicksWithoutNotification(string button)
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command GetClicksWithoutNotification skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command GetClicksWithoutNotification remotely...");
            try
            {
                _ = _grpcClient.GetClicksWithoutNotificationAsync(new Pointer.ViewModels.Protos.GetClicksWithoutNotificationRequest { Button = button }, cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command GetClicksWithoutNotification: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command GetClicksWithoutNotification cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command GetClicksWithoutNotification: " + ex.Message); }
        }

        private void RemoteExecute_ResetClicks()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command ResetClicks skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command ResetClicks remotely...");
            try
            {
                _ = _grpcClient.ResetClicksAsync(new Pointer.ViewModels.Protos.ResetClicksRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command ResetClicks: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command ResetClicks cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command ResetClicks: " + ex.Message); }
        }

        private void RemoteExecute_CancelTest()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command CancelTest skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command CancelTest remotely...");
            try
            {
                _ = _grpcClient.CancelTestAsync(new Pointer.ViewModels.Protos.CancelTestRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command CancelTest: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command CancelTest cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command CancelTest: " + ex.Message); }
        }

        private void RemoteExecute_FinishTest()
        {
            if (!_isInitialized || _isDisposed) { Debug.WriteLine("[ClientProxy:PointerViewModel] Not initialized or disposed, command FinishTest skipped."); return; }
            Debug.WriteLine("[ClientProxy:PointerViewModel] Executing command FinishTest remotely...");
            try
            {
                _ = _grpcClient.FinishTestAsync(new Pointer.ViewModels.Protos.FinishTestRequest(), cancellationToken: _cts.Token);
            }
            catch (RpcException ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Error executing command FinishTest: " + ex.Status.StatusCode + " - " + ex.Status.Detail); }
            catch (OperationCanceledException) { Debug.WriteLine("[ClientProxy:PointerViewModel] Command FinishTest cancelled."); }
            catch (Exception ex) { Debug.WriteLine("[ClientProxy:PointerViewModel] Unexpected error executing command FinishTest: " + ex.Message); }
        }

        private void StartListeningToPropertyChanges(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => 
            {
                if (_isDisposed) return;
                Debug.WriteLine("[PointerViewModelRemoteClient] Starting property change listener...");
                try
                {
                    var subscribeRequest = new Pointer.ViewModels.Protos.SubscribeRequest { ClientId = Guid.NewGuid().ToString() };
                    using var call = _grpcClient.SubscribeToPropertyChanges(subscribeRequest, cancellationToken: cancellationToken);
                    Debug.WriteLine("[PointerViewModelRemoteClient] Subscribed to property changes. Waiting for updates...");
                    int updateCount = 0;
                    await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        updateCount++;
                        if (_isDisposed) { Debug.WriteLine("[PointerViewModelRemoteClient] Disposed during update " + updateCount + ", exiting property update loop."); break; }
                        Debug.WriteLine($"[PointerViewModelRemoteClient] RAW UPDATE #" + updateCount + " RECEIVED: PropertyName=\"" + update.PropertyName + "\", ValueTypeUrl=\"" + (update.NewValue?.TypeUrl ?? "null_type_url") + "\"");
                        Action updateAction = () => {
                           try {
                               Debug.WriteLine("[PointerViewModelRemoteClient] Dispatcher: Attempting to update \"" + update.PropertyName + "\" (Update #" + updateCount + ").");
                               switch (update.PropertyName)
                               {
                                   case nameof(Show):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating Show from {this.Show} to {val}."); this.Show = val; Debug.WriteLine($"After update, Show is {this.Show}."); } else { Debug.WriteLine($"Mismatched descriptor for Show, expected BoolValue."); } break;
                                   case nameof(ShowSpinner):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowSpinner from {this.ShowSpinner} to {val}."); this.ShowSpinner = val; Debug.WriteLine($"After update, ShowSpinner is {this.ShowSpinner}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowSpinner, expected BoolValue."); } break;
                                   case nameof(ClicksToPass):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating ClicksToPass from {this.ClicksToPass} to {val}."); this.ClicksToPass = val; Debug.WriteLine($"After update, ClicksToPass is {this.ClicksToPass}."); } else { Debug.WriteLine($"Mismatched descriptor for ClicksToPass, expected Int32Value."); } break;
                                   case nameof(Is3Btn):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating Is3Btn from {this.Is3Btn} to {val}."); this.Is3Btn = val; Debug.WriteLine($"After update, Is3Btn is {this.Is3Btn}."); } else { Debug.WriteLine($"Mismatched descriptor for Is3Btn, expected BoolValue."); } break;
                                   case nameof(TestTimeoutSec):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating TestTimeoutSec from {this.TestTimeoutSec} to {val}."); this.TestTimeoutSec = val; Debug.WriteLine($"After update, TestTimeoutSec is {this.TestTimeoutSec}."); } else { Debug.WriteLine($"Mismatched descriptor for TestTimeoutSec, expected Int32Value."); } break;
                                   case nameof(Instructions):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating Instructions from \"{this.Instructions}\" to '\"{val}\"."); this.Instructions = val; Debug.WriteLine($"After update, Instructions is '\"{this.Instructions}\"."); } else { Debug.WriteLine($"Mismatched descriptor for Instructions, expected StringValue."); } break;
                                   case nameof(ShowCursorTest):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowCursorTest from {this.ShowCursorTest} to {val}."); this.ShowCursorTest = val; Debug.WriteLine($"After update, ShowCursorTest is {this.ShowCursorTest}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowCursorTest, expected BoolValue."); } break;
                                   case nameof(ShowConfigSelection):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowConfigSelection from {this.ShowConfigSelection} to {val}."); this.ShowConfigSelection = val; Debug.WriteLine($"After update, ShowConfigSelection is {this.ShowConfigSelection}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowConfigSelection, expected BoolValue."); } break;
                                   case nameof(ShowClickInstructions):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowClickInstructions from {this.ShowClickInstructions} to {val}."); this.ShowClickInstructions = val; Debug.WriteLine($"After update, ShowClickInstructions is {this.ShowClickInstructions}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowClickInstructions, expected BoolValue."); } break;
                                   case nameof(ShowTimer):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowTimer from {this.ShowTimer} to {val}."); this.ShowTimer = val; Debug.WriteLine($"After update, ShowTimer is {this.ShowTimer}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowTimer, expected BoolValue."); } break;
                                   case nameof(ShowBottom):
                    if (update.NewValue!.Is(BoolValue.Descriptor)) { var val = update.NewValue.Unpack<BoolValue>().Value; Debug.WriteLine($"Updating ShowBottom from {this.ShowBottom} to {val}."); this.ShowBottom = val; Debug.WriteLine($"After update, ShowBottom is {this.ShowBottom}."); } else { Debug.WriteLine($"Mismatched descriptor for ShowBottom, expected BoolValue."); } break;
                                   case nameof(TimerText):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating TimerText from \"{this.TimerText}\" to '\"{val}\"."); this.TimerText = val; Debug.WriteLine($"After update, TimerText is '\"{this.TimerText}\"."); } else { Debug.WriteLine($"Mismatched descriptor for TimerText, expected StringValue."); } break;
                                   case nameof(SelectedDevice):
                 if (update.NewValue!.Is(StringValue.Descriptor)) { var val = update.NewValue.Unpack<StringValue>().Value; Debug.WriteLine($"Updating SelectedDevice from \"{this.SelectedDevice}\" to '\"{val}\"."); this.SelectedDevice = val; Debug.WriteLine($"After update, SelectedDevice is '\"{this.SelectedDevice}\"."); } else { Debug.WriteLine($"Mismatched descriptor for SelectedDevice, expected StringValue."); } break;
                                   case nameof(LastClickCount):
                     if (update.NewValue!.Is(Int32Value.Descriptor)) { var val = update.NewValue.Unpack<Int32Value>().Value; Debug.WriteLine($"Updating LastClickCount from {this.LastClickCount} to {val}."); this.LastClickCount = val; Debug.WriteLine($"After update, LastClickCount is {this.LastClickCount}."); } else { Debug.WriteLine($"Mismatched descriptor for LastClickCount, expected Int32Value."); } break;
                                   default: Debug.WriteLine($"[ClientProxy:PointerViewModel] Unknown property in notification: \"{update.PropertyName}\""); break;
                               }
                           } catch (Exception exInAction) { Debug.WriteLine($"[ClientProxy:PointerViewModel] EXCEPTION INSIDE updateAction for \"{update.PropertyName}\": " + exInAction.ToString()); }
                        };
                        #if WPF_DISPATCHER
                        Application.Current?.Dispatcher.Invoke(updateAction);
                        #else
                        updateAction();
                        #endif
                        Debug.WriteLine("[PointerViewModelRemoteClient] Processed update #" + updateCount + " for \"" + update.PropertyName + "\". Still listening...");
                    }
                    Debug.WriteLine("[PointerViewModelRemoteClient] ReadAllAsync completed or cancelled after " + updateCount + " updates.");
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { Debug.WriteLine("[ClientProxy:PointerViewModel] Property subscription RpcException Cancelled."); }
                catch (OperationCanceledException) { Debug.WriteLine($"[ClientProxy:PointerViewModel] Property subscription OperationCanceledException."); }
                catch (Exception ex) { if (!_isDisposed) Debug.WriteLine($"[ClientProxy:PointerViewModel] Error in property listener: " + ex.GetType().Name + " - " + ex.Message + "\nStackTrace: " + ex.StackTrace); }
                Debug.WriteLine("[PointerViewModelRemoteClient] Property change listener task finished.");
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Debug.WriteLine("[PointerViewModelRemoteClient] Disposing...");
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
