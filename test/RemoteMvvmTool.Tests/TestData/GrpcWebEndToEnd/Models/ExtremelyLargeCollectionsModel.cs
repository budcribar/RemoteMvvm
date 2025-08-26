using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            // Test with larger collections to stress test serialization
            LargeNumberList = new ObservableCollection<int>(Enumerable.Range(1, 1000)); // 1000 items
            
            // Large dictionary
            LargeStringDict = new Dictionary<string, int>();
            for (int i = 0; i < 100; i++) // 100 key-value pairs
            {
                LargeStringDict[$"key_{i:D3}"] = i * 10;
            }
            
            CollectionCount = LargeNumberList.Count;
            DictionarySize = LargeStringDict.Count;
            MaxValue = LargeNumberList.Max();
            MinValue = LargeNumberList.Min();
        }

        [ObservableProperty]
        private ObservableCollection<int> _largeNumberList = new();

        [ObservableProperty]
        private Dictionary<string, int> _largeStringDict = new();

        [ObservableProperty]
        private int _collectionCount = 0;

        [ObservableProperty]
        private int _dictionarySize = 0;

        [ObservableProperty]
        private int _maxValue = 0;

        [ObservableProperty]
        private int _minValue = 0;
    }
}