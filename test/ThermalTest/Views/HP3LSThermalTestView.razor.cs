using HP.Telemetry;
using HPSystemsTools.ViewModels;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HPSystemsTools.Views
{
    public partial class HP3LSThermalTestView : IObserver<IEnumerable<ITelemetryReading>>, IDisposable
    {

        private IDisposable? _thermalMonitorHandle;
        private IDisposable? _cpuLoadMonitorHandle;


        [Inject] private IBiosNumericSensorMonitor? ThermalMonitor { get; set; }
        [Inject] private IProcessorLoadMonitor? ProcessorLoadMonitor { get; set; }
        /// <summary>
        /// The test instance to be displayed.
        /// </summary>
        [Parameter]
        public HP3LSThermalTest Test { get; set; }

        /// <summary>
        /// The view model for the test.
        /// </summary>
        public HP3LSThermalTestViewModel ViewModel => Test?.ViewModel;

        protected override async Task OnInitializedAsync()
        {
            await InitializeViewModelAsync();
            _thermalMonitorHandle = ThermalMonitor?.Subscribe(this);
            _cpuLoadMonitorHandle = ProcessorLoadMonitor?.Subscribe(this);
            await base.OnInitializedAsync();
        }

        private async Task InitializeViewModelAsync()
        {
            try
            {
                if (Test == null || ViewModel == null)
                {
                    Console.WriteLine("Error: Test or ViewModel is null in HP3LSThermalTestView.");
                    return;
                }

                await ViewModel.OnInitializedAsync(Test);
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitializeViewModelAsync: {ex.Message}");
            }
        }

        private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PropertyChanged handler: {ex.Message}");
            }
        }

        #region Observer
        public void OnNext(IEnumerable<ITelemetryReading> reading)
        {
            foreach (ITelemetryReading telemetry in reading)
            {
                if ((telemetry.Zone != Zone.CPUZ_0) && (telemetry.Zone != Zone.CPUZ_1))
                    continue;
                ViewModel.OnNext(telemetry);
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        #endregion

        public void Dispose()
        {
            _thermalMonitorHandle?.Dispose();
            _cpuLoadMonitorHandle?.Dispose();
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }
    }
}