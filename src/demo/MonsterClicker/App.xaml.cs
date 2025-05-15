using MonsterClicker.ViewModels;
using System.Windows;

using MonsterClicker.ViewModels.Protos; // Assuming this is where your generated gRPC code would live
using MonsterClicker.GrpcServices; // Assuming this is where your generated GameServiceGrpcImpl would be
using MonsterClicker.RemoteClients; // Assuming this is where your GameViewModelRemoteClient would be

using Grpc.Core;
using Grpc.Net.Client;

namespace MonsterClicker
{
    public partial class App : Application
    {
        private const string ServerAddress = "http://localhost:50051"; // Example gRPC server address

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            string mode = e.Args.FirstOrDefault()?.ToLowerInvariant() ?? "local"; // Default to local

            try
            {
                switch (mode)
                {
                    case "server":
                        Console.WriteLine("Starting in SERVER mode...");
                        var gameViewModelForServer = new GameViewModel();
                        // The generated GameServiceGrpcImpl would take gameViewModelForServer as a dependency
                        // Example: var grpcService = new GameServiceGrpcImpl(gameViewModelForServer);

                        Server server = new Server
                        {
                            Services = { GameViewModelService.BindService(new GameServiceGrpcImpl(gameViewModelForServer)) }, // GameServiceGrpcImpl would be your generated class
                            Ports = { new ServerPort("localhost", 50051, ServerCredentials.Insecure) }
                        };
                        server.Start();
                        Console.WriteLine($"gRPC Server listening on port 50051");

                        // In server mode, the UI can still run locally, bound to the same VM instance
                        mainWindow.DataContext = gameViewModelForServer;
                        mainWindow.Title += " (Server Mode - Hosting Game)";
                        break;

                    case "client":
                        Console.WriteLine("Starting in CLIENT mode...");
                        var channel = GrpcChannel.ForAddress(ServerAddress);
                        var grpcClient = new GameViewModelService.GameViewModelServiceClient(channel);

                        //// GameViewModelRemoteClient would be your generated client-side proxy ViewModel
                        var remoteViewModel = new GameViewModelRemoteClient(grpcClient);
                        await remoteViewModel.InitializeRemoteAsync(); // Method to fetch initial state and subscribe

                        mainWindow.DataContext = remoteViewModel;
                        mainWindow.Title += $" (Client Mode - Connected to {ServerAddress})";
                        break;

                    default: // "local" or any other argument
                        Console.WriteLine("Starting in LOCAL mode...");
                        mainWindow.DataContext = new GameViewModel();
                        mainWindow.Title += " (Local Mode)";
                        break;
                }
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}\n\nMode: {mode}\nMake sure the gRPC server is running if in client mode.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Optionally, log the full exception
                Console.WriteLine($"Detailed error: {ex}");
                Current.Shutdown(); // Exit if critical error
            }
        }
    }
}
