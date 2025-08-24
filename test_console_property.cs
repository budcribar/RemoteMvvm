using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Diagnostics;

namespace ConsoleTest
{
    public partial class TestViewModel : ObservableObject
    {
        public TestViewModel()
        {
            Console.WriteLine($"Initial FirePropertyChangedOnUIThread: {this.FirePropertyChangedOnUIThread}");
            
            // **CRITICAL FIX**: Set to false for console applications
            this.FirePropertyChangedOnUIThread = false;
            
            Console.WriteLine($"After setting to false: {this.FirePropertyChangedOnUIThread}");
            
            Status = "Initial";
            this.PropertyChanged += TestViewModel_PropertyChanged;
        }

        private void TestViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine($"? PropertyChanged fired for: {e.PropertyName}");
        }

        [ObservableProperty]
        private string _status = "Default";
        
        public void TestReflectionAndManualTrigger()
        {
            Console.WriteLine("\n=== Testing reflection-based property setting + manual PropertyChanged ===");
            
            // Use reflection to set the property (simulating UpdatePropertyValue)
            var prop = this.GetType().GetProperty("Status");
            prop?.SetValue(this, "SetViaReflection");
            
            // Manually trigger PropertyChanged (simulating our fix)
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
            
            Console.WriteLine("\n=== Test 2: Reflection + manual PropertyChanged trigger ===");
            vm.TestReflectionAndManualTrigger();
            
            Console.WriteLine("\nTest completed!");
            Console.WriteLine("If you see PropertyChanged events firing, the fix works!");
        }
    }
}