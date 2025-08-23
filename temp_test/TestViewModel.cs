using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TestNs
{
    public partial class TestViewModel : ObservableObject
    {
        [ObservableProperty]
        private Dictionary<string, int> _simpleDict = new();
        
        [ObservableProperty]
        private ObservableCollection<Dictionary<string, int>> _listOfDicts = new();
    }
}