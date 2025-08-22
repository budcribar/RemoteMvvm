using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace HP.Telemetry 
{ 
    public enum Zone { CPUZ_0, CPUZ_1 } 
}

namespace Generated.ViewModels 
{
    public class ThermalZoneComponentViewModel 
    { 
        public HP.Telemetry.Zone Zone { get; set; } 
        public int Temperature { get; set; } 
    }

    public partial class TestViewModel : ObservableObject 
    { 
        [ObservableProperty] 
        private ObservableCollection<ThermalZoneComponentViewModel> _zoneList = new();
        
        [ObservableProperty]
        private string _status = "Ready";
    }
}
