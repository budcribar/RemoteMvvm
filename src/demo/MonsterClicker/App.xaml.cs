using System.Windows;
using MonsterClicker.ViewModels;
using PeakSWC.Mvvm.Remote;

namespace MonsterClicker
{
    public partial class App : Application
    {
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
                        var gameVm = new GameViewModel(new ServerOptions { Port = NetworkConfig.Port });
                        mainWindow = new MainWindow(AppModeUtil.AppMode.Server)
                        {
                            DataContext = gameVm,
                            Title = $"Server Mode – Hosting Game on port:{NetworkConfig.Port}"
                        };
                        break;

                    case "client":
                        Console.WriteLine("Starting in CLIENT mode...");
                        var clientVm = new GameViewModel(new ClientOptions { Address = NetworkConfig.ServerAddress });
                        mainWindow = new MainWindow(AppModeUtil.AppMode.Client)
                        {
                            DataContext = await clientVm.GetRemoteModel(),
                            Title = $"Client Mode - Connected to {NetworkConfig.ServerAddress})"
                        };
                        break;

                    default: // "local"
                        Console.WriteLine("Starting in LOCAL mode...");
                        mainWindow = new MainWindow(AppModeUtil.AppMode.Local)
                        {
                            DataContext = new GameViewModel(),
                            Title = "Local Mode"
                        };
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