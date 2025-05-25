using MonsterClicker.ViewModels;
using System.Windows;
using MonsterClicker.ViewModels.Protos;
using MonsterClicker.RemoteClients;
using Grpc.Net.Client;
using PeakSWC.Mvvm.Remote;
namespace MonsterClicker
{
    public partial class App : Application
    {
        private const string ServerAddress = "https://localhost:50052";

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string mode = e.Args.FirstOrDefault()?.ToLowerInvariant() ?? "local";
            MainWindow mainWindow;

            try
            {
                switch (mode)
                {
                    case "server":
                        Console.WriteLine("Starting in SERVER mode...");
                        var gameVm = new GameViewModel(new ServerOptions { Port=50052 });
                        mainWindow = new MainWindow(AppModeUtil.AppMode.Server);

                       

                        mainWindow.DataContext = gameVm;
                        mainWindow.Title += " (Server Mode – Hosting Game on :50052)";
                        break;

                    case "client":
                        Console.WriteLine("Starting in CLIENT mode...");

                        var clientVm = new GameViewModel(new ClientOptions {  Address = ServerAddress });
                        //var channel = GrpcChannel.ForAddress(ServerAddress);
                        //var grpcClient = new GameViewModelService.GameViewModelServiceClient(channel);

                        //var remoteViewModel = new GameViewModelRemoteClient(grpcClient);
                        //await remoteViewModel.InitializeRemoteAsync();

                        mainWindow = new MainWindow(AppModeUtil.AppMode.Client);
                        mainWindow.DataContext = await clientVm.GetRemoteModel();
                        mainWindow.Title += $" (Client Mode - Connected to {ServerAddress})";
                        break;

                    default: // "local"
                        Console.WriteLine("Starting in LOCAL mode...");
                        mainWindow = new MainWindow(AppModeUtil.AppMode.Local);
                        mainWindow.DataContext = new GameViewModel();
                        mainWindow.Title += " (Local Mode)";
                        break;
                }

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}\n\nMode: {mode}\n\n{ex.StackTrace}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Detailed error: {ex}");
                Current.Shutdown();
            }
        }
    }
}