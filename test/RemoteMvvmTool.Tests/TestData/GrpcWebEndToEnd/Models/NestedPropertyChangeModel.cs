using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Temperature = 1; // Simple property for testing
            ZoneList.CollectionChanged += ZoneList_CollectionChanged;
            ZoneList.Add(new ThermalZoneComponentViewModel { Temperature = 1 });
        }

        [ObservableProperty]
        private int _temperature;
        
        [ObservableProperty]
        private ObservableCollection<ThermalZoneComponentViewModel> _zoneList = new();

        private void ZoneList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ThermalZoneComponentViewModel item in e.NewItems)
                    item.PropertyChanged += Zone_PropertyChanged;
            if (e.OldItems != null)
                foreach (ThermalZoneComponentViewModel item in e.OldItems)
                    item.PropertyChanged -= Zone_PropertyChanged;
        }

        private void Zone_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var index = ZoneList.IndexOf((ThermalZoneComponentViewModel)sender!);
            OnPropertyChanged($"ZoneList[{index}].{e.PropertyName}");
        }
    }

    public partial class ThermalZoneComponentViewModel : ObservableObject
    {
        [ObservableProperty] private int _temperature;
    }
}