using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using HP.Telemetry;
using System.Linq;

namespace HP.Telemetry
{
    public enum Zone { CPUZ_0, CPUZ_1, CPUZ_2 }
}

namespace Generated.ViewModels
{
    public class ZoneCollection : ObservableCollection<ThermalZoneComponentViewModel>
    {
     public ThermalZoneComponentViewModel this[Zone zone]
     {
         get
         {
             var match = this.FirstOrDefault(z => z.Zone == zone);
             if (match == null)
                 throw new KeyNotFoundException($"Zone '{zone}' not found.");
             return match;
         }
     }

     public bool ContainsKey(Zone zone) => this.FirstOrDefault(z => z.Zone == zone) != null;

    }

    public class ThermalZoneComponentViewModel
    {
        public HP.Telemetry.Zone Zone { get; set; }
        public int Temperature { get; set; }
    }

    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel() 
        {
            ZoneList.Add(new ThermalZoneComponentViewModel
            {
                Zone = HP.Telemetry.Zone.CPUZ_1,
                Temperature = 42
            });
            ZoneList.Add(new ThermalZoneComponentViewModel
            {
                Zone = HP.Telemetry.Zone.CPUZ_2,
                Temperature = 43
            });
        }



        [ObservableProperty]
        private ZoneCollection _zoneList = new();

        [ObservableProperty]
        private string _status = "Ready";
    }

}
