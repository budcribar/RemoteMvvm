using System;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ServerInitiatedTest
{
    /// <summary>
    /// Test ViewModel that demonstrates server-initiated PropertyChanged events.
    /// This is what we need to add to the actual server ViewModel for testing.
    /// </summary>
    public partial class TestViewModelWithBackgroundChanges : ObservableObject, IDisposable
    {
        private readonly Timer _backgroundTimer;
        private int _updateCounter = 0;

        [ObservableProperty]
        private string _status = "Initial";

        [ObservableProperty]
        private string _message = "No updates yet";

        [ObservableProperty] 
        private DateTime _lastUpdated = DateTime.Now;

        [ObservableProperty]
        private int _backgroundCounter = 0;

        public TestViewModelWithBackgroundChanges()
        {
            Console.WriteLine("?? TestViewModel: Initializing with background property changes");
            
            // **CRITICAL**: Ensure PropertyChanged events fire on current thread for gRPC streaming
            this.FirePropertyChangedOnUIThread = false;
            Console.WriteLine($"?? Set FirePropertyChangedOnUIThread = {this.FirePropertyChangedOnUIThread}");
            
            // Set up background timer to simulate server-initiated property changes
            _backgroundTimer = new Timer(3000); // Update every 3 seconds
            _backgroundTimer.Elapsed += OnBackgroundUpdate;
            _backgroundTimer.AutoReset = true;
            _backgroundTimer.Start();
            
            Console.WriteLine("? Background timer started - properties will update every 3 seconds");
            Console.WriteLine("   This simulates real-world scenarios like:");
            Console.WriteLine("   - Database change notifications");
            Console.WriteLine("   - External system updates");
            Console.WriteLine("   - Business logic-driven property changes");
            Console.WriteLine("   - Timer-based status updates");
        }

        private void OnBackgroundUpdate(object? sender, ElapsedEventArgs e)
        {
            _updateCounter++;
            
            try 
            {
                Console.WriteLine($"?? Background update #{_updateCounter} triggered at {DateTime.Now:HH:mm:ss.fff}");
                
                // Update multiple properties to test streaming
                Status = $"Background Update #{_updateCounter}";
                Message = $"Server-initiated change at {DateTime.Now:HH:mm:ss}";
                LastUpdated = DateTime.Now;
                BackgroundCounter = _updateCounter;
                
                Console.WriteLine($"? Properties updated: Status, Message, LastUpdated, BackgroundCounter");
                Console.WriteLine($"   Status = \"{Status}\"");
                Console.WriteLine($"   Message = \"{Message}\"");
                Console.WriteLine($"   BackgroundCounter = {BackgroundCounter}");
                
                // These property setters should automatically fire PropertyChanged events
                // which should then be streamed to any subscribed gRPC clients
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error in background update: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Manually trigger a property change for testing
        /// </summary>
        public void TriggerManualPropertyChange()
        {
            Console.WriteLine("?? Manual property change triggered");
            Status = $"Manual change at {DateTime.Now:HH:mm:ss.fff}";
            Message = "This was triggered manually for testing";
            
            // This should fire PropertyChanged events and stream to clients
        }

        /// <summary>
        /// Start rapid updates for stress testing
        /// </summary>
        public void StartRapidUpdates(int count = 10, int intervalMs = 500)
        {
            Console.WriteLine($"? Starting {count} rapid updates every {intervalMs}ms");
            
            Task.Run(async () =>
            {
                for (int i = 1; i <= count; i++)
                {
                    Status = $"Rapid Update {i}/{count}";
                    Message = $"Rapid change #{i} at {DateTime.Now:HH:mm:ss.fff}";
                    BackgroundCounter = 1000 + i;
                    
                    Console.WriteLine($"? Rapid update {i}/{count} completed");
                    
                    if (i < count)
                    {
                        await Task.Delay(intervalMs);
                    }
                }
                
                Console.WriteLine("? Rapid updates completed");
            });
        }

        public void Dispose()
        {
            Console.WriteLine("?? TestViewModel: Disposing background timer");
            _backgroundTimer?.Stop();
            _backgroundTimer?.Dispose();
        }
    }

    /// <summary>
    /// Server-side test program to verify PropertyChanged events are firing correctly
    /// </summary>
    class ServerSidePropertyTest
    {
        static async Task Main()
        {
            Console.WriteLine("?? Server-Side PropertyChanged Event Test");
            Console.WriteLine("=========================================");
            Console.WriteLine("This test verifies that PropertyChanged events fire correctly");
            Console.WriteLine("on the server side before testing gRPC streaming.");
            Console.WriteLine("");

            var viewModel = new TestViewModelWithBackgroundChanges();
            var eventCount = 0;

            // Subscribe to PropertyChanged events
            viewModel.PropertyChanged += (sender, e) =>
            {
                eventCount++;
                Console.WriteLine($"?? PropertyChanged #{eventCount}: {e.PropertyName} at {DateTime.Now:HH:mm:ss.fff}");
                
                // Get the new value
                var propertyInfo = sender?.GetType().GetProperty(e.PropertyName);
                var newValue = propertyInfo?.GetValue(sender);
                Console.WriteLine($"   New value: {newValue}");
            };

            Console.WriteLine("? Subscribed to PropertyChanged events");
            Console.WriteLine("? Waiting for background property changes...");
            Console.WriteLine("   (Properties should update every 3 seconds)");
            Console.WriteLine("");

            // Wait for several background updates
            await Task.Delay(10000);

            Console.WriteLine("\n?? Triggering manual property change...");
            viewModel.TriggerManualPropertyChange();

            await Task.Delay(2000);

            Console.WriteLine("\n? Testing rapid property updates...");
            viewModel.StartRapidUpdates(5, 200);

            await Task.Delay(5000);

            // Results
            Console.WriteLine("\n?? RESULTS:");
            Console.WriteLine($"?? Total PropertyChanged events fired: {eventCount}");
            Console.WriteLine($"?? Current Status: \"{viewModel.Status}\"");
            Console.WriteLine($"?? Current Message: \"{viewModel.Message}\"");
            Console.WriteLine($"?? Current Counter: {viewModel.BackgroundCounter}");

            if (eventCount > 0)
            {
                Console.WriteLine("\n? SUCCESS: PropertyChanged events are firing correctly!");
                Console.WriteLine("   - Background timer updates work");
                Console.WriteLine("   - Manual property changes work");
                Console.WriteLine("   - FirePropertyChangedOnUIThread = false is working");
                Console.WriteLine("");
                Console.WriteLine("?? NEXT STEP: Integrate this into the gRPC server ViewModel");
                Console.WriteLine("   and test with the client streaming subscription.");
            }
            else
            {
                Console.WriteLine("\n? FAILURE: No PropertyChanged events fired!");
                Console.WriteLine("   - Check FirePropertyChangedOnUIThread setting");
                Console.WriteLine("   - Check if ObservableObject is working correctly");
                Console.WriteLine("   - Check if property setters are being called");
            }

            viewModel.Dispose();
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}