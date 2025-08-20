using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace SimpleViewModelTest.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<DeviceInfo> devices = new();

        [RelayCommand]
        private void UpdateStatus(DeviceStatus status)
        {
            if (devices.Count > 0)
            {
                devices[0].Status = status;
            }
        }
    }
}
