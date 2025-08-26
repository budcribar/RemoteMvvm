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
            // Create a list of dictionaries - this should stress test the serialization
            MetricsByRegion = new ObservableCollection<Dictionary<string, int>>
            {
                new Dictionary<string, int> { { "cpu", 75 }, { "memory", 60 }, { "disk", 85 } },
                new Dictionary<string, int> { { "cpu", 42 }, { "memory", 78 }, { "disk", 92 } },
                new Dictionary<string, int> { { "cpu", 88 }, { "memory", 55 } } // Intentionally missing disk
            };
            
            TotalRegions = MetricsByRegion.Count;
            IsAnalysisComplete = true;
        }

        [ObservableProperty]
        private ObservableCollection<Dictionary<string, int>> _metricsByRegion = new();
        
        [ObservableProperty]
        private int _totalRegions = 0;

        [ObservableProperty]
        private bool _isAnalysisComplete = false;
    }
}