using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Debug.WriteLine("[TestViewModel] Constructor called");
            ItemList = new ObservableCollection<string> { "Item1", "Item2" };
            Debug.WriteLine("[TestViewModel] Initial items: " + string.Join(", ", ItemList));
        }

        [ObservableProperty]
        private ObservableCollection<string> _itemList = new();
    }
}