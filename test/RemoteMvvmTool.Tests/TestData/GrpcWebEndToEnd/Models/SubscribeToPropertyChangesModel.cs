using System;
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
            Console.WriteLine("[TestViewModel] Constructor called");
            
            // Set initial status
            Status = "Initial";
            Console.WriteLine("[TestViewModel] Set Status to 'Initial'");
            
            // Subscribe to client connection events to trigger property changes
            TestViewModelGrpcServiceImpl.ClientCountChanged += OnClientCountChanged;
            Console.WriteLine("[TestViewModel] Subscribed to ClientCountChanged event");
        }
        
        private async void OnClientCountChanged(object? sender, int clientCount)
        {
            Console.WriteLine($"[TestViewModel] Client count changed to: {clientCount}");
            
            if (clientCount > 0)
            {
                // Client connected - trigger property changes after a brief delay
                await Task.Delay(500); // Small delay to ensure subscription is fully established
                
                Console.WriteLine("[TestViewModel] About to set Status to 'Updated'");
                Status = "Updated";
                Console.WriteLine("[TestViewModel] Status set to 'Updated'");
                
                // Give time for the notification to be processed
                await Task.Delay(1000);
                
                Console.WriteLine("[TestViewModel] About to set Status to 'Final'");
                Status = "Final";
                Console.WriteLine("[TestViewModel] Status set to 'Final'");
            }
        }

        [ObservableProperty]
        private string _status = "Default";
    }
}