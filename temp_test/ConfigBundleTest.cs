using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConfigExample
{
    public partial class ConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<Dictionary<string, string>> _settingsGroups = new();
    }
}