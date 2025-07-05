using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

            // Option 2: If you want to use HTTPS, you'll need to handle certificate issues
            //var grpcAddress = "https://localhost:50051";

            // Configure gRPC client for gRPC-Web
            builder.Services.AddGrpcClient<GameViewModelService.GameViewModelServiceClient>(options =>
            {
                options.Address = new Uri(grpcAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
                return handler;
            });

            // Register your GameViewModelRemoteClient
            builder.Services.AddScoped<GameViewModelRemoteClient>();

            // Add logging for debugging
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            await builder.Build().RunAsync();
        }
    }
}