using CommunityToolkit.Mvvm.ComponentModel;
using HP.Telemetry;
using HPSystemsTools.Models;
using HPSystemsTools.Services;
//using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HPSystemsTools.ViewModels
{
    public partial class ThermalZoneComponentViewModel : ObservableObject
    {
        private ThermalZoneService? _thermalZoneService;
        private TestSettingsModel _testSettings = new();

        [ObservableProperty]
        public partial Zone Zone { get; set; }

        partial void OnZoneChanged(Zone value)
        {
            _thermalZoneService = new ThermalZoneService(value);
        }

        [ObservableProperty]
        public partial bool IsActive { get; private set; }
                [ObservableProperty]
        public partial string DeviceName { get; private set; }

        public ThermalZoneComponentViewModel()
        {
            DeviceName = "";
        }
        [ObservableProperty]
        public partial int Temperature { get; private set; }
        [ObservableProperty]
        public partial int ProcessorLoad { get; private set; }
        [ObservableProperty]
        public partial int FanSpeed { get; private set; }
        [ObservableProperty]
        public partial int SecondsInState { get; private set; }
        [ObservableProperty]
        public partial DateTime FirstSeenInState { get; private set; }
        [ObservableProperty]
        public partial int Progress { get; private set; }
        [ObservableProperty]
        public partial string Background { get; private set; }
        [ObservableProperty]
        public partial ThermalStateEnum Status { get; private set; }
        [ObservableProperty]
        public partial ThermalStateEnum State { get; private set; }

        private string _stateDescription = "Unknown state";
        public string StateDescription
        {
            get => _stateDescription;
            private set => SetProperty(ref _stateDescription, value);
        }

        partial void OnStateChanged(ThermalStateEnum oldValue, ThermalStateEnum newValue)
        {
            StateDescription = StateDescriptions.TryGetValue(newValue, out var desc)
                ? desc
                : "Unknown state";
        }

        private string _statusDescription = "Unknown status";
        public string StatusDescription
        {
            get => _statusDescription;
            private set => SetProperty(ref _statusDescription, value);
        }

        private static readonly Dictionary<ThermalStateEnum, string> StateDescriptions = new()
        {
            {ThermalStateEnum.Unknown, "Initializing" },
            {ThermalStateEnum.MaybeRunningHot, "Unsupported processor. The unit may be running hot." },
            {ThermalStateEnum.MaybeOk, "Unsupported processor. The thermal mechanism appears functional." },
            {ThermalStateEnum.RunningHot, "The unit may be running hot." },
            {ThermalStateEnum.Ok, "The thermal mechanism appears functional." },
            {ThermalStateEnum.StressLevelExceeded, "The operational conditions for the test are outside the allowed boundaries. Try closing some applications to reduce the processor load." },
            {ThermalStateEnum.Pass, "The thermal mechanism appears functional." },
            {ThermalStateEnum.Fail, "The unit is running hot. Please check the processor cooling solution." },
            {ThermalStateEnum.MaybePass, "Unsupported processor. The thermal mechanism appears functional." },
            {ThermalStateEnum.MaybeFail, "Unsupported processor. The unit may be running hot." },
            {ThermalStateEnum.CheckInProgress, "Please wait for the test to complete." },
            {ThermalStateEnum.Reset, "The operational conditions for the test are outside the allowed boundaries. Try closing some applications to reduce the processor load. The test will resume once operational conditions are met." }
        };

        partial void OnStatusChanged(ThermalStateEnum oldValue, ThermalStateEnum newValue)
        {
            StatusDescription = StateDescriptions.TryGetValue(newValue, out var desc)
                ? desc
                : "Unknown status";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThermalZoneComponentViewModel"/> class.
        /// </summary>
        //public ThermalZoneComponentViewModel(Zone zone)
        //{
        //    Zone = zone;
        //    _thermalZoneService = new ThermalZoneService(zone);
        //}

        /// <summary>
        /// Called when the component is initialized.
        /// </summary>
        internal Task OnInitializedAsync() => Task.CompletedTask;

        /// <summary>
        /// Adds a telemetry reading and updates the view model.
        /// </summary>
        public void Add(ITelemetryReading reading)
        {
            var thermalZone = _thermalZoneService.Add(reading);
            if (thermalZone == null) return;
            IsActive = thermalZone.IsActive;
            DeviceName = thermalZone.DeviceName;
            Temperature = thermalZone.Temperature;
            ProcessorLoad = thermalZone.ProcessorLoad;
            FanSpeed = thermalZone.FanSpeed;
            Update(_testSettings);
        }

        /// <summary>
        /// Updates the view model state based on the provided settings.
        /// </summary>
        public void Update(TestSettingsModel settings)
        {
            _testSettings = settings ?? new TestSettingsModel();
            DateTime firstSeenInState;
            State = _thermalZoneService.GetState(_testSettings, out firstSeenInState);
            FirstSeenInState = firstSeenInState;
            SecondsInState = Convert.ToInt32((DateTime.Now - firstSeenInState).TotalSeconds);
            Status = _thermalZoneService.GetStatus(_testSettings, State, SecondsInState);
            Progress = _thermalZoneService.GetProgress(_testSettings, State, SecondsInState);
            Background = GetBackground(Status);
        }

        private static string GetBackground(ThermalStateEnum status) => status switch
        {
            ThermalStateEnum.Pass or ThermalStateEnum.MaybePass => "#EEFFEE",
            ThermalStateEnum.Fail or ThermalStateEnum.MaybeFail => "#FFEEEE",
            _ => "#FFFFFF"
        };
    }
}
