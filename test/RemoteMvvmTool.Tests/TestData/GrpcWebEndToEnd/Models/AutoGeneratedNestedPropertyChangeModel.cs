using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Temperature = 1; // Simple property for testing
            // Auto-generated event handling will wire up ZoneList events
            ZoneList.Add(new ThermalZoneComponentViewModel { Temperature = 1 });
        }

        [ObservableProperty]
        private int _temperature;
        
        [ObservableProperty]
        private ObservableCollection<ThermalZoneComponentViewModel> _zoneList = new();

        // NO MANUAL EVENT HANDLERS - they should be auto-generated!
    }

    public partial class ThermalZoneComponentViewModel : ObservableObject
    {
        [ObservableProperty] private int _temperature;
    }
}