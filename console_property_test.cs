using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Diagnostics;

namespace ConsolePropertyChangedTest
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            // This is the key fix for console applications!
            this.FirePropertyChangedOnUIThread = false;
            
            Status = "Initial";
            this.PropertyChanged += TestViewModel_PropertyChanged;
        }

        private void TestViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine($"? PropertyChanged fired for: {e.PropertyName}");
        }

        [ObservableProperty]
        private string _status = "Default";
        
        public void TestReflectionPropertySet()
        {
            Console.WriteLine("Testing reflection-based property setting...");
            
            // Simulate what UpdatePropertyValue does with reflection
            var prop = this.GetType().GetProperty("Status");
            prop?.SetValue(this, "UpdatedViaReflection");
            
            // Then manually trigger PropertyChanged
            this.OnPropertyChanged("Status");
        }
    }
    
    class Program
    {
        static void Main()
        {
            var vm = new TestViewModel();
            
            Console.WriteLine("=== Test 1: Normal property setting ===");
            vm.Status = "NormalUpdate";
            
            Console.WriteLine("=== Test 2: Reflection + manual PropertyChanged ===");
            vm.TestReflectionPropertySet();
            
            Console.WriteLine("Test completed!");
        }
    }
}