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
            
            // Set initial status
            Status = "Initial";
            Debug.WriteLine("[TestViewModel] Set Status to 'Initial'");
            
            // Subscribe to client connection events to trigger property changes
            TestViewModelGrpcServiceImpl.ClientCountChanged += OnClientCountChanged;
        }
        
        private async void OnClientCountChanged(object? sender, int clientCount)
        {
            Debug.WriteLine($"[TestViewModel] Client count changed to: {clientCount}");
            
            if (clientCount > 0)
            {
                // Client connected - trigger property changes after a brief delay
                await Task.Delay(500); // Small delay to ensure subscription is fully established
                
                Debug.WriteLine("[TestViewModel] About to set Status to 'Updated'");
                Status = "Updated";
                Debug.WriteLine("[TestViewModel] Status set to 'Updated'");
                
                // Give time for the notification to be processed
                await Task.Delay(1000);
                
                Debug.WriteLine("[TestViewModel] About to set Status to 'Final'");
                Status = "Final";
                Debug.WriteLine("[TestViewModel] Status set to 'Final'");
            }
        }

        [ObservableProperty]
        private string _status = "Default";
    }
}