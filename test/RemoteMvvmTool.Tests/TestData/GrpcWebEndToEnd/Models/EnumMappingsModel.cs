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
            CurrentStatus = Status.Active;
            Priority = TaskPriority.High;
        }

        [ObservableProperty]
        private Status _currentStatus = Status.Active;
        
        [ObservableProperty]
        private TaskPriority _priority = TaskPriority.Low;

        [RelayCommand]
        private void ChangeStatus(Status newStatus) => CurrentStatus = newStatus;
    }

    public enum Status
    {
        Active = 1,
        Idle = 2, 
        Error = 3
    }

    public enum TaskPriority
    {
        Low = 10,
        Medium = 20,
        High = 30
    }
}