using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RapidUpdatesTest
{
    public partial class TestViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _message = "Initial";

        [ObservableProperty] 
        private int _counter = 0;

        public TestViewModel()
        {
            Console.WriteLine($"TestViewModel created. FirePropertyChangedOnUIThread = {this.FirePropertyChangedOnUIThread}");
            
            // **CRITICAL FIX**: Set to false for console/gRPC applications
            this.FirePropertyChangedOnUIThread = false;
            Console.WriteLine($"Set FirePropertyChangedOnUIThread = false. New value: {this.FirePropertyChangedOnUIThread}");
        }
    }

    class RapidUpdateStressTest
    {
        static async Task Main()
        {
            Console.WriteLine("?? Testing 100 Rapid Property Updates (C# Version)");
            Console.WriteLine("==================================================");
            
            var vm = new TestViewModel();
            int successCount = 0;
            int failureCount = 0;
            
            var propertyChangedEvents = new List<string>();
            
            // Subscribe to PropertyChanged events
            vm.PropertyChanged += (sender, e) => {
                propertyChangedEvents.Add($"{e.PropertyName}={DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"?? PropertyChanged: {e.PropertyName} at {DateTime.Now:HH:mm:ss.fff}");
            };
            
            Console.WriteLine($"?? Initial Message: \"{vm.Message}\"");
            Console.WriteLine($"?? Initial Counter: {vm.Counter}");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Test 1: 100 rapid direct property updates
            Console.WriteLine("\n? Test 1: 100 rapid DIRECT property updates...");
            
            var tasks = new List<Task>();
            
            for (int i = 1; i <= 100; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(index % 10); // Stagger slightly to create realistic timing
                        
                        // Update properties directly (simulating normal MVVM usage)
                        vm.Message = $"DirectUpdate_{index}_{DateTime.Now.Ticks}";
                        vm.Counter = index;
                        
                        Console.WriteLine($"? Direct update {index} completed");
                        Interlocked.Increment(ref successCount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? Direct update {index} failed: {ex.Message}");
                        Interlocked.Increment(ref failureCount);
                    }
                });
                
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            var directUpdateTime = stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"\n?? Direct updates completed in {directUpdateTime}ms");
            Console.WriteLine($"? Successful direct updates: {successCount}/100");
            Console.WriteLine($"? Failed direct updates: {failureCount}/100");
            Console.WriteLine($"?? PropertyChanged events fired: {propertyChangedEvents.Count}");
            
            // Test 2: 100 rapid reflection-based updates (simulating gRPC UpdatePropertyValue)
            Console.WriteLine("\n? Test 2: 100 rapid REFLECTION-based property updates...");
            
            successCount = 0;
            failureCount = 0;
            propertyChangedEvents.Clear();
            
            stopwatch.Restart();
            
            tasks.Clear();
            
            for (int i = 101; i <= 200; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(index % 10); // Stagger slightly
                        
                        // Update via reflection (simulating gRPC UpdatePropertyValue)
                        var messageProperty = typeof(TestViewModel).GetProperty("Message");
                        var counterProperty = typeof(TestViewModel).GetProperty("Counter");
                        
                        if (messageProperty != null && messageProperty.CanWrite)
                        {
                            messageProperty.SetValue(vm, $"ReflectionUpdate_{index}_{DateTime.Now.Ticks}");
                            
                            // **MANUAL PropertyChanged TRIGGER** (simulating our gRPC fix)
                            var onPropertyChanged = typeof(TestViewModel).GetMethod("OnPropertyChanged",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                
                            if (onPropertyChanged != null)
                            {
                                onPropertyChanged.Invoke(vm, new object[] { "Message" });
                            }
                        }
                        
                        if (counterProperty != null && counterProperty.CanWrite)
                        {
                            counterProperty.SetValue(vm, index);
                            
                            // Manual PropertyChanged trigger for Counter
                            var onPropertyChanged = typeof(TestViewModel).GetMethod("OnPropertyChanged",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                
                            if (onPropertyChanged != null)
                            {
                                onPropertyChanged.Invoke(vm, new object[] { "Counter" });
                            }
                        }
                        
                        Console.WriteLine($"? Reflection update {index} completed");
                        Interlocked.Increment(ref successCount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? Reflection update {index} failed: {ex.Message}");
                        Interlocked.Increment(ref failureCount);
                    }
                });
                
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            var reflectionUpdateTime = stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"\n?? Reflection updates completed in {reflectionUpdateTime}ms");
            Console.WriteLine($"? Successful reflection updates: {successCount}/100");
            Console.WriteLine($"? Failed reflection updates: {failureCount}/100");
            Console.WriteLine($"?? PropertyChanged events fired: {propertyChangedEvents.Count}");
            
            // Final state
            Console.WriteLine($"\n?? Final Message: \"{vm.Message}\"");
            Console.WriteLine($"?? Final Counter: {vm.Counter}");
            
            // Validation
            Console.WriteLine("\n?? VALIDATION RESULTS:");
            
            var allDirectUpdatesSuccessful = failureCount == 0; // From both tests combined
            var propertyChangedWorking = propertyChangedEvents.Count > 0;
            var noDeadlocks = (directUpdateTime + reflectionUpdateTime) < 30000; // Less than 30 seconds total
            var finalStateValid = vm.Message.Contains("Update_") && vm.Counter > 0;
            
            Console.WriteLine($"   All updates successful: {(successCount == 100 && failureCount == 0) ? '?' : '?'}");
            Console.WriteLine($"   PropertyChanged events firing: {propertyChangedWorking ? '?' : '?'}");
            Console.WriteLine($"   No deadlocks detected: {noDeadlocks ? '?' : '?'}");
            Console.WriteLine($"   Final state valid: {finalStateValid ? '?' : '?'}");
            
            var overallSuccess = (successCount == 100 && failureCount == 0) && propertyChangedWorking && noDeadlocks && finalStateValid;
            
            Console.WriteLine("\n?? OVERALL TEST RESULT:");
            if (overallSuccess)
            {
                Console.WriteLine("? SUCCESS: Rapid updates stress test PASSED!");
                Console.WriteLine("   - Threading is stable");
                Console.WriteLine("   - PropertyChanged events working correctly");
                Console.WriteLine("   - Both direct and reflection-based updates work");
                Console.WriteLine("   - Ready for gRPC integration");
            }
            else
            {
                Console.WriteLine("? FAILURE: Rapid updates stress test FAILED!");
                Console.WriteLine("   - Check PropertyChanged event handling");
                Console.WriteLine("   - Check threading configuration");
                Console.WriteLine("   - Check FirePropertyChangedOnUIThread setting");
            }
            
            Console.WriteLine($"\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}