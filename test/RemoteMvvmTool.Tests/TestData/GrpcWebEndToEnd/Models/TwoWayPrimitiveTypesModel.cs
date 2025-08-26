using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Message = "123";
            IsEnabled = true;
            Counter = 9876543210;
            PlayerLevel = 4000000000;
            HasBonus = 3.14f;
            BonusMultiplier = 6.28;
        }

        [ObservableProperty]
        private string _message = "";

        [ObservableProperty]
        private bool _isEnabled = false;

        [ObservableProperty]
        private long _counter = 0;

        [ObservableProperty]
        private uint _playerLevel = 0;

        [ObservableProperty]
        private float _hasBonus = 0;

        [ObservableProperty]
        private double _bonusMultiplier = 0;
    }
}