using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace SimpleViewModelTest.ViewModels
{
    public partial class BuggyCommandViewModel : ObservableObject
    {
        [RelayCommand]
        private void UseGuid(Guid id) { }

        [RelayCommand]
        private void UseTimestamp(DateTime timestamp) { }
    }
}
