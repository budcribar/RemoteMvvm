using Grpc.Core;
using Grpc.Net.Client;
using Pointer.ViewModels.Protos;
using HPSystemsTools.RemoteClients;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using PeakSWC.Mvvm.Remote;

namespace HPSystemsTools
{
    public partial class PointerViewModel : IDisposable
    {
        private PointerViewModelGrpcServiceImpl? _grpcService;
        private IHost? _aspNetCoreHost;
        private GrpcChannel? _channel;
        private HPSystemsTools.RemoteClients.PointerViewModelRemoteClient? _remoteClient;
        private readonly Dispatcher _dispatcher;

        public PointerViewModel(ServerOptions options) : this()
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _dispatcher = Dispatcher.CurrentDispatcher;
            _grpcService = new PointerViewModelGrpcServiceImpl(this, _dispatcher);

            // Always use ASP.NET Core with Kestrel to support gRPC-Web
            StartAspNetCoreServer(options);
        }

        private void StartAspNetCoreServer(ServerOptions options)
        {
            var builder = WebApplication.CreateBuilder();

            // Add services to the container
            builder.Services.AddGrpc();

            // Add CORS support for gRPC-Web
            builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            // Register the gRPC service implementation with ASP.NET Core DI
            builder.Services.AddSingleton(_grpcService);

            // Configure Kestrel to listen on the specified port with HTTP/2 support
            builder.WebHost.ConfigureKestrel(kestrelOptions =>
            {
                kestrelOptions.ListenLocalhost(NetworkConfig.Port, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
            });

            // Build the application
            var app = builder.Build();

            // Configure the HTTP request pipeline
            app.UseRouting();

            // Use CORS middleware
            app.UseCors("AllowAll");

            // Enable gRPC-Web middleware
            app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

            // Map gRPC services
            app.MapGrpcService<PointerViewModelGrpcServiceImpl>()
               .EnableGrpcWeb()
               .RequireCors("AllowAll");

            // Start the server
            _aspNetCoreHost = app;
            Task.Run(() => app.RunAsync()); // Run the server in a background thread
        }

        public PointerViewModel(ClientOptions options) : this()
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _dispatcher = Dispatcher.CurrentDispatcher;
            _channel = GrpcChannel.ForAddress(options.Address);
            var client = new PointerViewModelService.PointerViewModelServiceClient(_channel);
            _remoteClient = new PointerViewModelRemoteClient(client);
        }

        public async Task<PointerViewModelRemoteClient> GetRemoteModel()
        {
            if (_remoteClient == null) throw new InvalidOperationException("Client options not provided");
            await _remoteClient.InitializeRemoteAsync();
            return _remoteClient;
        }

        public void Dispose()
        {
            _channel?.ShutdownAsync().GetAwaiter().GetResult();
            _aspNetCoreHost?.StopAsync().GetAwaiter().GetResult();
            _aspNetCoreHost?.Dispose();
        }
    }
}
