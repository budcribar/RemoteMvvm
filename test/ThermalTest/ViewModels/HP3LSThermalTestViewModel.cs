using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HP.Telemetry;
using HPSystemsTools.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HPSystemsTools.ViewModels
{
    public partial class HP3LSThermalTestViewModel : ObservableObject
    {

        private HP3LSThermalTest _test;

        private int _cpuTemperatureThreshold;
        private int _cpuLoadThreshold;
        private int _cpuLoadTimeSpan;

        public HP3LSThermalTestViewModel()
        {
            TestSettings = new TestSettingsModel();
        }

        public int CpuTemperatureThreshold
        {
            get => _cpuTemperatureThreshold;
            set
            {
                TestSettings.CpuTemperatureThreshold = value;
                SetProperty(ref _cpuTemperatureThreshold, value);
            }
        }
        public int CpuLoadThreshold
        {
            get => _cpuLoadThreshold;
            set
            {
                TestSettings.CpuLoadThreshold = value;
                SetProperty(ref _cpuLoadThreshold, value);
            }
        }
        public int CpuLoadTimeSpan
        {
            get => _cpuLoadTimeSpan;
            set
            {
                TestSettings.CpuLoadTimeSpan = value;
                SetProperty(ref _cpuLoadTimeSpan, value);
            }
        }
        [ObservableProperty]
        public partial Dictionary<Zone, ThermalZoneComponentViewModel> Zones { get; set; }

        [ObservableProperty]
        internal partial TestSettingsModel TestSettings { get; set; }

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

            Zones = new Dictionary<Zone, ThermalZoneComponentViewModel>
            {
                { Zone.CPUZ_0, new ThermalZoneComponentViewModel(Zone.CPUZ_0) },
                { Zone.CPUZ_1, new ThermalZoneComponentViewModel(Zone.CPUZ_1) }
            };

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
