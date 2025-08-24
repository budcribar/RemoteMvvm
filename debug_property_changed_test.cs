using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DebugPropertyChanged
{
    public class TestClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private string _status = "Default";
        
        public string Status 
        { 
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            Console.WriteLine($"OnPropertyChanged called for: {propertyName}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    class Program
    {
        static void Main()
        {
            var obj = new TestClass();
            
            // Subscribe to PropertyChanged
            obj.PropertyChanged += (sender, e) =>
            {
                Console.WriteLine($"PropertyChanged event received: {e.PropertyName}");
            };
            
            // Test normal property setting
            Console.WriteLine("=== Test 1: Normal Property Setting ===");
            obj.Status = "Updated1";
            
            // Test reflection-based property setting
            Console.WriteLine("=== Test 2: Reflection Property Setting ===");
            var propertyInfo = obj.GetType().GetProperty("Status");
            propertyInfo.SetValue(obj, "Updated2");
            Console.WriteLine("After reflection set - no PropertyChanged should fire");
            
            // Test manual OnPropertyChanged trigger
            Console.WriteLine("=== Test 3: Manual OnPropertyChanged Trigger ===");
            var onPropertyChangedMethod = obj.GetType().GetMethod("OnPropertyChanged", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new Type[] { typeof(string) },
                null);
                
            if (onPropertyChangedMethod != null)
            {
                onPropertyChangedMethod.Invoke(obj, new object[] { "Status" });
            }
        }
    }
}