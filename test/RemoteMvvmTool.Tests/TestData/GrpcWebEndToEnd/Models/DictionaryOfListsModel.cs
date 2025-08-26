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
            // Dictionary where values are lists - another complex nesting scenario
            ScoresByCategory = new Dictionary<string, List<double>>
            {
                { "speed", new List<double> { 10.5, 15.2, 8.7 } },
                { "accuracy", new List<double> { 95.5, 87.3 } },
                { "efficiency", new List<double> { 99.9 } } // Single value list - changed from 100.0 to avoid duplicate
            };
            
            CategoryCount = ScoresByCategory.Count;
            MaxScore = 100.0;
        }

        [ObservableProperty]
        private Dictionary<string, List<double>> _scoresByCategory = new();
        
        [ObservableProperty]
        private int _categoryCount = 0;

        [ObservableProperty]
        private double _maxScore = 0.0;
    }
}