using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            StatusMap = new Dictionary<Status, string>
            {
                { Status.Active, "4" },
                { Status.Idle, "5" },
                { Status.Error, "6" }
            };
            CurrentStatus = Status.Maintenance;
        }

        [ObservableProperty]
        private Dictionary<Status, string> _statusMap = new();
        
        [ObservableProperty]
        private Status _currentStatus = Status.Maintenance;
    }

    public enum Status
    {
        Active = 1,
        Idle = 2,
        Error = 3,
        Maintenance = 7
    }
}