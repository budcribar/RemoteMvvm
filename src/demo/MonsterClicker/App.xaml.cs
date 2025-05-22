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
using Microsoft.Extensions.Logging;
using Grpc.AspNetCore.Web;
using System.Windows.Threading;

namespace MonsterClicker
{
    public partial class App : Application
    {
        private const string ServerAddress = "https://localhost:50052";

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
                                    // Single port for both HTTP/1.1 (gRPC-Web) and HTTP/2 (gRPC)
                                    options.ListenLocalhost(50052, listenOptions =>
                                    {
                                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;

                                        // UseHttps for Monster client comment out for BlazerMonster client
                                        //listenOptions.UseHttps();
                                        // Start without HTTPS for debugging
                                    });
                                });

                                webBuilder.ConfigureServices(services =>
                                {
                                    services.AddSingleton(gameVm);
                                    services.AddSingleton<Dispatcher>(Dispatcher.CurrentDispatcher);

                                    // Add gRPC services
                                    services.AddGrpc(options =>
                                    {
                                        options.EnableDetailedErrors = true; // Enable detailed error messages
                                    });

                                    // Add CORS
                                    services.AddCors(options =>
                                    {
                                        options.AddPolicy("AllowAll", policy =>
                                        {
                                            policy.AllowAnyOrigin()
                                                  .AllowAnyMethod()
                                                  .AllowAnyHeader()
                                                  .WithExposedHeaders(
                                                      "Grpc-Status",
                                                      "Grpc-Message",
                                                      "Grpc-Encoding",
                                                      "Grpc-Accept-Encoding");
                                        });
                                    });

                                    // Add detailed logging
                                    services.AddLogging(builder =>
                                    {
                                        builder.AddConsole();
                                        builder.SetMinimumLevel(LogLevel.Debug);
                                    });
                                });

                                webBuilder.Configure((context, app) =>
                                {
                                    var logger = app.ApplicationServices.GetRequiredService<ILogger<App>>();

                                    // Add request logging middleware
                                    app.Use(async (context, next) =>
                                    {
                                        logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} from {context.Request.Headers.UserAgent}");
                                        logger.LogInformation($"Content-Type: {context.Request.ContentType}");
                                        logger.LogInformation($"Headers: {string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}:{h.Value}"))}");

                                        await next();

                                        logger.LogInformation($"Response: {context.Response.StatusCode}");
                                    });

                                    app.UseRouting();
                                    app.UseCors("AllowAll");

                                    // Enable gRPC-Web
                                    app.UseGrpcWeb(new GrpcWebOptions
                                    {
                                        DefaultEnabled = true
                                    });

                                    app.UseEndpoints(endpoints =>
                                    {
                                        // Map the gRPC service
                                        endpoints.MapGrpcService<GameViewModelGrpcServiceImpl>()
                                                .EnableGrpcWeb()
                                                .RequireCors("AllowAll");

                                        // Test endpoint to verify server is working
                                        endpoints.MapGet("/", async context =>
                                        {
                                            var response = "gRPC Server is running on HTTP :50052\n" +
                                                         "Available services:\n" +
                                                         "- /monsterclicker_viewmodels_protos.GameViewModelService/GetState\n" +
                                                         $"- Request path: {context.Request.Path}\n" +
                                                         $"- Time: {DateTime.Now}";

                                            await context.Response.WriteAsync(response);
                                        });

                                        // Add a test endpoint for gRPC service discovery
                                        endpoints.MapGet("/grpc/health", async context =>
                                        {
                                            context.Response.ContentType = "application/json";
                                            await context.Response.WriteAsync("{\"status\":\"serving\",\"service\":\"GameViewModelService\"}");
                                        });

                                        // Add endpoint to test CORS
                                        endpoints.MapGet("/test-cors", async context =>
                                        {
                                            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                                            await context.Response.WriteAsync("CORS test endpoint working");
                                        });
                                    });
                                });
                            })
                            .Build();

                        // Start the host
                        await host.StartAsync();

                        Console.WriteLine("=== gRPC Server Started ===");
                        Console.WriteLine("Server URL: http://localhost:50052");
                        Console.WriteLine("Test in browser: http://localhost:50052");
                        Console.WriteLine("Health check: http://localhost:50052/grpc/health");
                        Console.WriteLine("CORS test: http://localhost:50052/test-cors");
                        Console.WriteLine("==============================");

                        mainWindow.DataContext = gameVm;
                        mainWindow.Title += " (Server Mode – Hosting Game on :50052)";
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
                MessageBox.Show($"Error during startup: {ex.Message}\n\nMode: {mode}\n\n{ex.StackTrace}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Detailed error: {ex}");
                Current.Shutdown();
            }
        }
    }
}