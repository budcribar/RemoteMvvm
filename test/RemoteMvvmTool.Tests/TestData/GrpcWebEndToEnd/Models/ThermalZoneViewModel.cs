using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            ZoneList.Add(new ThermalZoneComponentViewModel 
            { 
                Zone = HP.Telemetry.Zone.CPUZ_0, 
                Temperature = 42 
            });
            ZoneList.Add(new ThermalZoneComponentViewModel 
            { 
                Zone = HP.Telemetry.Zone.CPUZ_1, 
                Temperature = 43 
            });
        }

        [ObservableProperty] 
        private ObservableCollection<ThermalZoneComponentViewModel> _zoneList = new();
        
        [ObservableProperty]
        private string _status = "Ready";
    }

    public class ThermalZoneComponentViewModel 
    {
        public HP.Telemetry.Zone Zone { get; set; }
        public int Temperature { get; set; }
    }
}

namespace HP.Telemetry 
{
    public enum Zone { CPUZ_0, CPUZ_1 }
}