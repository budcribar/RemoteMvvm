using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConfigExample
{
    public partial class ConfigViewModel : ObservableObject
    {
        // This approach would cause an error with the current implementation
        // [ObservableProperty]
        // private List<Dictionary<string, string>> _settingsGroups = new();
        
        // Instead, we use a custom class approach which works:
        [ObservableProperty]
        private List<SettingsGroup> _settingsGroups = new();
    }

    // Custom class to replace Dictionary<string, string>
    public class SettingsGroup
    {
        public Dictionary<string, string> Entries { get; set; } = new();
    }
}