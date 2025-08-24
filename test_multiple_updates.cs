using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MultipleUpdatesTest
{
    public partial class TestViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _status = "Initial";

        [ObservableProperty]
        private int _counter = 0;

        [ObservableProperty]
        private DateTime _lastUpdated = DateTime.Now;
    }

    class Program
    {
        static async Task Main()
        {
            var vm = new TestViewModel();
            
            Console.WriteLine("=== Testing Multiple Rapid Updates ===");
            
            // Simulate multiple rapid UpdatePropertyValue calls
            var tasks = new Task[10];
            
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    await Task.Delay(index * 10); // Stagger slightly
                    
                    // Simulate what UpdatePropertyValue does
                    try
                    {
                        Console.WriteLine($"Update {index}: Setting Status to 'Status_{index}'");
                        vm.Status = $"Status_{index}";
                        
                        Console.WriteLine($"Update {index}: Setting Counter to {index}");
                        vm.Counter = index;
                        
                        Console.WriteLine($"Update {index}: Setting LastUpdated");
                        vm.LastUpdated = DateTime.Now;
                        
                        Console.WriteLine($"? Update {index} completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? Update {index} failed: {ex.Message}");
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            
            Console.WriteLine("\n=== Final State ===");
            Console.WriteLine($"Status: {vm.Status}");
            Console.WriteLine($"Counter: {vm.Counter}");
            Console.WriteLine($"LastUpdated: {vm.LastUpdated}");
            Console.WriteLine("\n? Multiple updates test completed!");
        }
    }
}