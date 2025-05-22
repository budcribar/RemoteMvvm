using MonsterClicker.ViewModels;
using System.Windows;
using MonsterClicker.ViewModels.Protos;
using MonsterClicker.GrpcServices;
using MonsterClicker.RemoteClients;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grpc.AspNetCore.Web;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace MonsterClicker
{
    public partial class App : Application
    {
        private const string ServerAddress = "https://localhost:50051"; // Use HTTPS

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            string mode = e.Args.FirstOrDefault()?.ToLowerInvariant() ?? "local";

            try
            {
                switch (mode)
                {
                    case "server":
                        Console.WriteLine("Starting in SERVER mode...");
                        var gameVm = new GameViewModel();

                        var host = Host.CreateDefaultBuilder()
                            .ConfigureWebHostDefaults(webBuilder =>
                            {
                                webBuilder.UseKestrel(options =>
                                {
                                    // HTTP port for gRPC-Web (Blazor WASM)
                                    options.ListenLocalhost(50052, listenOptions =>
                                    {
                                        listenOptions.Protocols = HttpProtocols.Http1;
                                        // No HTTPS for simplicity - use HTTP for gRPC-Web
                                    });

                                    // HTTPS port for regular gRPC (optional)
                                    options.ListenLocalhost(50051, listenOptions =>
                                    {
                                        listenOptions.Protocols = HttpProtocols.Http2;
                                        listenOptions.UseHttps();
                                    });
                                });

                                webBuilder.ConfigureServices(services =>
                                {
                                    services.AddSingleton(gameVm);
                                    services.AddGrpc();

                                    // Configure CORS for gRPC-Web
                                    services.AddCors(options =>
                                    {
                                        options.AddPolicy("AllowBlazorApp", policy =>
                                        {
                                            policy.AllowAnyOrigin()
                                                  .AllowAnyMethod()
                                                  .AllowAnyHeader()
                                                  .WithExposedHeaders(
                                                      "Grpc-Status",
                                                      "Grpc-Message",
                                                      "Grpc-Encoding",
                                                      "Grpc-Accept-Encoding",
                                                      "Content-Grpc-Status",
                                                      "Content-Grpc-Message",
                                                      "Content-Grpc-Encoding");
                                        });
                                    });

                                    // Add logging for debugging
                                    services.AddLogging();
                                });

                                webBuilder.Configure(app =>
                                {
                                    // Add exception handling middleware
                                    app.UseExceptionHandler("/error");

                                    app.UseRouting();
                                    app.UseCors("AllowBlazorApp");

                                    // Enable gRPC-Web with default options
                                    app.UseGrpcWeb(new GrpcWebOptions
                                    {
                                        DefaultEnabled = true
                                    });

                                    app.UseEndpoints(endpoints =>
                                    {
                                        // Map gRPC service with explicit gRPC-Web and CORS
                                        endpoints.MapGrpcService<GameViewModelGrpcServiceImpl>()
                                                .EnableGrpcWeb()
                                                .RequireCors("AllowBlazorApp");

                                        // Health check endpoint
                                        endpoints.MapGet("/", async context =>
                                        {
                                            await context.Response.WriteAsync(
                                                "gRPC Server is running. " +
                                                "gRPC-Web available on HTTP :50052, " +
                                                "gRPC available on HTTPS :50051");
                                        });

                                        // Error handling endpoint
                                        endpoints.MapGet("/error", async context =>
                                        {
                                            await context.Response.WriteAsync("An error occurred");
                                        });
                                    });
                                });
                            })
                            .Build();

                        await host.StartAsync();

                        Console.WriteLine("gRPC Server started:");
                        Console.WriteLine("- gRPC-Web (HTTP): http://localhost:50052");
                        Console.WriteLine("- gRPC (HTTPS): https://localhost:50051");

                        mainWindow.DataContext = gameVm;
                        mainWindow.Title += " (Server Mode – Hosting Game)";
                        break;

                    case "client":
                        Console.WriteLine("Starting in CLIENT mode...");
                        var channel = GrpcChannel.ForAddress(ServerAddress);
                        var grpcClient = new GameViewModelService.GameViewModelServiceClient(channel);

                        var remoteViewModel = new GameViewModelRemoteClient(grpcClient);
                        await remoteViewModel.InitializeRemoteAsync();

                        mainWindow.DataContext = remoteViewModel;
                        mainWindow.Title += $" (Client Mode - Connected to {ServerAddress})";
                        break;

                    default: // "local"
                        Console.WriteLine("Starting in LOCAL mode...");
                        mainWindow.DataContext = new GameViewModel();
                        mainWindow.Title += " (Local Mode)";
                        break;
                }

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}\n\nMode: {mode}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Detailed error: {ex}");
                Current.Shutdown();
            }
        }
    }
}