using MonsterClicker.ViewModels;
using System.Windows;

using MonsterClicker.ViewModels.Protos; // Assuming this is where your generated gRPC code would live
using MonsterClicker.GrpcServices; // Assuming this is where your generated GameServiceGrpcImpl would be
using MonsterClicker.RemoteClients; // Assuming this is where your GameViewModelRemoteClient would be

using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grpc.AspNetCore.Web;
using Microsoft.AspNetCore.Cors.Infrastructure; // Ensure you have the correct NuGet package installed

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
                        

                        // … inside your switch("server") …

                        Console.WriteLine("Starting in SERVER mode…");
                        var gameVm = new GameViewModel();

                        var host = Host.CreateDefaultBuilder()
                            .ConfigureWebHostDefaults(webBuilder =>
                            {
                                webBuilder.UseKestrel(options =>
                                {
                                    // listen with HTTP/2 for "raw" gRPC clients
                                    options.ListenLocalhost(50051, o => o.Protocols = HttpProtocols.Http1AndHttp2);
                                    // listen with HTTP/1.1 for gRPC-Web (you can combine into one port if needed)
                                    //options.ListenLocalhost(50051, o => o.Protocols = HttpProtocols.Http1);
                                });

                                webBuilder.ConfigureServices(services =>
                                {
                                    services.AddSingleton(gameVm);
                                    services.AddGrpc();                            // classic gRPC
                                    
                                });

                                webBuilder.Configure(app =>
                                {
                                    app.UseRouting();

                                    // enable gRPC-Web for ANY gRPC endpoint on this host
                                    app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });  

                                    app.UseEndpoints(endpoints =>
                                    {
                                        // your generated service impl
                                        endpoints.MapGrpcService<GameViewModelGrpcServiceImpl>();

                                        // (optional) a simple fallback endpoint
                                        endpoints.MapGet("/", async ctx =>
                                        {
                                            await ctx.Response.WriteAsync("This server hosts gRPC + gRPC-Web.");
                                        });
                                    });
                                });
                            })
                            .Build();

                        await host.StartAsync();

                        Console.WriteLine("gRPC (HTTP/2) on port 50051, gRPC-Web (HTTP/1.1) on port 50051");
                        mainWindow.DataContext = gameVm;
                        mainWindow.Title += " (Server Mode – Hosting Game)";

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
