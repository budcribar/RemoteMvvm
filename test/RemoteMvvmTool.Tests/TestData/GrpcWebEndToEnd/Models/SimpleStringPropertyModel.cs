using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            Message = "44";
            Counter = 42;
            IsEnabled = true;
        }

        [ObservableProperty]
        private string _message = "";
        
        [ObservableProperty]
        private int _counter = 0;

        [ObservableProperty]
        private bool _isEnabled = false;

        [RelayCommand]
        private void Increment() => Counter++;
    }
}