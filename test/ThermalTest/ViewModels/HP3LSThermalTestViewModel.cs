using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HP.Telemetry;
using HPSystemsTools.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HPSystemsTools.ViewModels
{
    public partial class HP3LSThermalTestViewModel : ObservableObject
    {
        public Dictionary<Zone, ThermalZoneComponentViewModel> Zones = [];
        private HP3LSThermalTest _test = default!;

        public HP3LSThermalTestViewModel()
        {
        }

        [ObservableProperty]
        public partial int CpuTemperatureThreshold { get; set; }

        [ObservableProperty]
        public partial int CpuLoadThreshold { get; set; }

        [ObservableProperty]
        public partial int CpuLoadTimeSpan { get; set; }

        partial void OnCpuTemperatureThresholdChanged(int value)
        {
            if (TestSettings != null)
                TestSettings.CpuTemperatureThreshold = value;
        }

        partial void OnCpuLoadThresholdChanged(int value)
        {
            if (TestSettings != null)
                TestSettings.CpuLoadThreshold = value;
        }

        partial void OnCpuLoadTimeSpanChanged(int value)
        {
            if (TestSettings != null)
                TestSettings.CpuLoadTimeSpan = value;
        }



        [ObservableProperty]
        public partial ObservableCollection<ThermalZoneComponentViewModel> ZoneList { get; set; } = [];

        [ObservableProperty]
        internal partial TestSettingsModel TestSettings { get; set; } = default!;

        /// <summary>
        /// Controls the visibility of the description section.
        /// </summary>
        [ObservableProperty]
        public partial bool ShowDescription { get; set; }

        /// <summary>
        /// Controls the visibility of the readme section.
        /// </summary>
        [ObservableProperty]
        public partial bool ShowReadme { get; set; }

        internal Task OnInitializedAsync(HP3LSThermalTest test)
        {
            _test = test;

            TestSettings = new TestSettingsModel();

            var zones = new[]
            {
                new ThermalZoneComponentViewModel(Zone.CPUZ_0),
                new ThermalZoneComponentViewModel(Zone.CPUZ_1)
            };
            Zones = zones.ToDictionary(z => z.Zone, z => z);
            ZoneList = new ObservableCollection<ThermalZoneComponentViewModel>(zones);

            CpuLoadThreshold = TestSettings.CpuLoadThreshold;
            CpuTemperatureThreshold = TestSettings.CpuTemperatureThreshold;
            CpuLoadTimeSpan = TestSettings.CpuLoadTimeSpan;
            return Task.CompletedTask;
        }

        public void OnNext(ITelemetryReading telemetry)
        {
            if (telemetry == null) return;
            Zones[telemetry.Zone].Add(telemetry);
        }

        [RelayCommand]
        public void StateChanged(ThermalStateEnum state)
        {
            switch (state)
            {
                case ThermalStateEnum.MaybePass:
                case ThermalStateEnum.Pass:
                    _test.FinishTest(true);
                    break;
                case ThermalStateEnum.MaybeFail:
                case ThermalStateEnum.Fail:
                case ThermalStateEnum.Unknown:
                    _test.FinishTest(false);
                    break;
                default:
                    break;
            }
        }

        [RelayCommand]
        public void CancelTest()
        {
            _test.CancelTest();
        }
    }
}
