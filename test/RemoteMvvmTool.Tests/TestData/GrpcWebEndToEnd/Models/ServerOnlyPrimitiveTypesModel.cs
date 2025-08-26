using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public enum Mode { Idle = 1, Done = 2 }

    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Counter = (byte)1;
            PlayerLevel = (ushort)4;
            HasBonus = (sbyte)(-2);
            BonusMultiplier = (Half)1.5;
            IsEnabled = (nint)9;
            Status = '8';
            Message = new Guid("00000000-0000-0000-0000-000000000020");
            CurrentStatus = Mode.Done;
        }

        [ObservableProperty]
        private byte _counter;

        [ObservableProperty]
        private ushort _playerLevel;

        [ObservableProperty]
        private sbyte _hasBonus;

        [ObservableProperty]
        private Half _bonusMultiplier;

        [ObservableProperty]
        private nint _isEnabled;

        [ObservableProperty]
        private char _status;

        [ObservableProperty]
        private Guid _message = Guid.Empty;

        [ObservableProperty]
        private Mode _currentStatus = Mode.Idle;
    }
}