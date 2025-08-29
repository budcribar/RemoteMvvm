using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using MonsterClicker; // For NetworkConfig
using MonsterClicker.ViewModels.Protos; // For GameViewModelService
using MonsterClicker.ViewModels.RemoteClients; // For GameViewModelRemoteClient

namespace BlazorMonsterClicker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            var grpcAddress = NetworkConfig.GrpcWebAddress; // HTTP endpoint for gRPC-Web

            // Configure regular HttpClient
            builder.Services.AddScoped(sp => new HttpClient 
            { 
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
            });

            // Configure gRPC client with proper settings for browser environment
            builder.Services.AddGrpcClient<GameViewModelService.GameViewModelServiceClient>(options =>
            {
                options.Address = new Uri(grpcAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                // Use GrpcWebText mode which is more compatible for browsers
                // In WebAssembly we can't use ServerCertificateCustomValidationCallback
                return new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
            });

            // For Blazor WASM in .NET 8, enable gRPC-Web compatibility
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Register your GameViewModelRemoteClient
            builder.Services.AddScoped<GameViewModelRemoteClient>();

            // Add logging for debugging
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            await builder.Build().RunAsync();
        }
    }
}