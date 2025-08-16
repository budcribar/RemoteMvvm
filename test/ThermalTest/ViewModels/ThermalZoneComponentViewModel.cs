using CommunityToolkit.Mvvm.ComponentModel;
using HP.Telemetry;
using HPSystemsTools.Models;
using HPSystemsTools.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace HPSystemsTools.ViewModels
{
    public partial class ThermalZoneComponentViewModel : ObservableObject
    {
        private readonly ThermalZoneService _thermalZoneService;
        private TestSettingsModel _testSettings = new();

        [ObservableProperty]
        public partial Zone Zone { get; set; }
        [ObservableProperty]
        public partial bool IsActive { get; set; }
        [ObservableProperty]
        public partial string DeviceName { get; set; }
        [ObservableProperty]
        public partial int Temperature { get; set; }
        [ObservableProperty]
        public partial int ProcessorLoad { get; set; }
        [ObservableProperty]
        public partial int FanSpeed { get; set; }
        [ObservableProperty]
        public partial int SecondsInState { get; set; }
        [ObservableProperty]
        public partial DateTime FirstSeenInState { get; set; }
        [ObservableProperty]
        public partial int Progress { get; set; }
        [ObservableProperty]
        public partial string Background { get; set; }
        [ObservableProperty]
        public partial ThermalStateEnum Status { get; set; }
        [ObservableProperty]
        public partial ThermalStateEnum State { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThermalZoneComponentViewModel"/> class.
        /// </summary>
        public ThermalZoneComponentViewModel(Zone zone)
        {
            Zone = zone;
            _thermalZoneService = new ThermalZoneService(zone);
        }

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
