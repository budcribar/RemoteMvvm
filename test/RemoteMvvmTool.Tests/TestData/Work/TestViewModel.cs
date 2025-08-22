public class TestObservablePropertyAttribute : System.Attribute {}
public class TestRelayCommandAttribute : System.Attribute {}
namespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }
namespace Generated.ViewModels {
  public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } public int Temperature { get; set; } }
  public partial class TestViewModel : ObservableObject {
    [TestObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel> zoneList;
  }
}
public class ObservableObject {}
