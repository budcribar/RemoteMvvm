// Required using statements for CommunityToolkit.Mvvm and your custom attribute
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote; // Assuming your GenerateGrpcRemoteAttribute is in this namespace


namespace SampleApp.ViewModels
{
    // Attribute to mark this ViewModel for gRPC remote generation
    // Parameters: proto C# namespace, gRPC service name
    // Optional named parameters: ServerImplNamespace, ClientProxyNamespace
    [GenerateGrpcRemote("SampleApp.ViewModels.Protos", "CounterService",
        ServerImplNamespace = "SampleApp.GrpcServices",
        ClientProxyNamespace = "SampleApp.RemoteClients")]
    public partial class SampleViewModel : ObservableObject
    {
        // An observable property for a string value (e.g., a name)
        // The generator will create a 'Name' property from this '_name' field.
        [ObservableProperty]
        private string _name = "Initial Name";

        // An observable property for an integer value (e.g., a counter)
        // The generator will create a 'Count' property from this '_count' field.
        [ObservableProperty]
        private int _count;

        // A synchronous command that increments the count and updates the name.
        [RelayCommand]
        private void IncrementCount()
        {
            Count++;
            Name = $"Count is now {Count}";
        }

        // An asynchronous command that simulates a delay and then updates the count.
        [RelayCommand]
        private async Task DelayedIncrementAsync(int delayMilliseconds)
        {
            if (delayMilliseconds < 0)
            {
                // Example of handling invalid input for a command
                Console.WriteLine("Delay cannot be negative."); // Or throw an ArgumentOutOfRangeException
                return;
            }
            await Task.Delay(delayMilliseconds);
            Count += 5;
            Name = $"Count updated to {Count} after delay.";
        }

        // A command that might take a parameter
        [RelayCommand]
        private void SetNameToValue(string? value)
        {
            Name = value ?? "Default Name from Command";
        }


        // Example of a property that is not marked with [ObservableProperty]
        // This property will not be automatically included in the gRPC generation
        // by the [ObservableProperty] part of the source generator,
        // but if your .proto definition includes it, you'd handle it manually.
        public string NonObservableStatus { get; set; } = "Ready";

        // Constructor
        public SampleViewModel()
        {
            // Initialize properties if needed, or perform other setup.
        }
    }
}
