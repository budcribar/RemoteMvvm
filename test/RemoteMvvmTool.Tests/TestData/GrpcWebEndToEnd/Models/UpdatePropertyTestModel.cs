using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Diagnostics;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Debug.WriteLine("[TestViewModel] Constructor called");
            Message = "Initial Value";
            Counter = 100;
            IsEnabled = false;
        }

        [ObservableProperty]
        private string _message = "";
        
        [ObservableProperty]
        private int _counter = 0;

        [ObservableProperty]
        private bool _isEnabled = false;
    }
}