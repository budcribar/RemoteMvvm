using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Debug.WriteLine("[TestViewModel] Constructor called");
            Message = "Initial Value";
            Debug.WriteLine("[TestViewModel] Set Message to 'Initial Value'");
        }

        [ObservableProperty]
        private string _message = "";
    }
}