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
            Status = "Initial";
            Debug.WriteLine("[TestViewModel] Set Status to 'Initial'");
            
            // Subscribe to our own PropertyChanged event for testing
            this.PropertyChanged += TestViewModel_PropertyChanged;
        }

        private void TestViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[TestViewModel] PropertyChanged fired for: {e.PropertyName}");
        }

        [ObservableProperty]
        private string _status = "Default";
    }
}