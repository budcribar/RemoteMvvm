using HPSystemsTools.Models;
using HPSystemsTools.ViewModels;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HPSystemsTools.Views.Components
{
    public partial class ThermalZoneCompoonent : ComponentBase, IDisposable
    {
        [Parameter]
        public EventCallback<ThermalStateEnum> StateChangedEvent { get; set; }
        [Parameter] public ThermalZoneComponentViewModel ViewModel { get; set; }
        [Parameter] public TestSettingsModel? Settings { get; set; }
        [Parameter] public string? ProcessorLoadName { get; set; }

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

        protected override async Task OnInitializedAsync()
        {
            try
            {
                if (ViewModel == null)
                {
                    Console.WriteLine("Error: ViewModel is null in ThermalZoneCompoonent.OnInitializedAsync");
                    return;
                }

                await ViewModel.OnInitializedAsync();
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnInitializedAsync: {ex.Message}");
            }
            await base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            if (ViewModel != null)
            {
                ViewModel.Update(Settings);
            }
            else
            {
                Console.WriteLine("Warning: ViewModel is null in ThermalZoneCompoonent.OnParametersSet");
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

        int GetTemperatureThreshold()
        {
            if ((Settings == null) || (ViewModel == null))
                return 0;
            return Settings.GetTemperatureThreshold(ViewModel.DeviceName);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            switch (ViewModel.Status)
            {
                case ThermalStateEnum.MaybePass:
                case ThermalStateEnum.Pass:
                case ThermalStateEnum.MaybeFail:
                case ThermalStateEnum.Fail:
                    await StateChangedEvent.InvokeAsync(ViewModel.Status).ConfigureAwait(false);
                    break;
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public void Dispose()
        {
            Settings = null;
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            ViewModel = null;
        }
    }
}
