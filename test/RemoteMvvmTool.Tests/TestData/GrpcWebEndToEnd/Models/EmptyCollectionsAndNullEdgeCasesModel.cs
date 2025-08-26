using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            // Test empty collections and edge cases
            EmptyList = new ObservableCollection<string>(); // Empty collection
            EmptyDict = new Dictionary<int, string>(); // Empty dictionary
            
            // Nullable with actual values
            NullableInt = 42;
            NullableDouble = null; // This should be interesting
            
            // Single item collections
            SingleItemList = new List<int> { 999 };
            SingleItemDict = new Dictionary<string, bool> { { "solo", true } };
            
            ZeroValues = new List<int> { 0, 0, 0 }; // Multiple zeros
            HasData = false; // Should be 0
        }

        [ObservableProperty]
        private ObservableCollection<string> _emptyList = new();

        [ObservableProperty]
        private Dictionary<int, string> _emptyDict = new();

        [ObservableProperty]
        private int? _nullableInt;

        [ObservableProperty]
        private double? _nullableDouble;

        [ObservableProperty]
        private List<int> _singleItemList = new();

        [ObservableProperty]
        private Dictionary<string, bool> _singleItemDict = new();

        [ObservableProperty]
        private List<int> _zeroValues = new();

        [ObservableProperty]
        private bool _hasData = true;
    }
}