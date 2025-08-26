using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            // Test edge case primitives and extreme values
            PreciseValue = 99999.99999m; // decimal
            TinyValue = (nuint)42; // nuint
            BigValue = ulong.MaxValue; // Massive number
            
            BirthDate = new DateOnly(1990, 5, 15);
            StartTime = new TimeOnly(14, 30, 45);
            
            NegativeShort = short.MinValue;
            PositiveByte = byte.MaxValue;
            
            UnicodeChar = '?'; // Unicode star character
            EmptyGuid = Guid.Empty;
        }

        [ObservableProperty]
        private decimal _preciseValue;

        [ObservableProperty]
        private nuint _tinyValue;

        [ObservableProperty]
        private ulong _bigValue;

        [ObservableProperty]
        private DateOnly _birthDate;

        [ObservableProperty]
        private TimeOnly _startTime;

        [ObservableProperty]
        private short _negativeShort;

        [ObservableProperty]
        private byte _positiveByte;

        [ObservableProperty]
        private char _unicodeChar;

        [ObservableProperty]
        private Guid _emptyGuid = Guid.Empty;
    }
}