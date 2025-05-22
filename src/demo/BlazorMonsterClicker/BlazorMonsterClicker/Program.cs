using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Grpc.Net.Client.Web; // For GrpcWebHandler
// Assuming your generated gRPC client and RemoteClient are in these namespaces
using MonsterClicker.ViewModels.Protos;
using MonsterClicker.RemoteClients;

namespace BlazorMonsterClicker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
           
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
        
            // Configure gRPC client
            builder.Services.AddGrpcClient<GameViewModelService.GameViewModelServiceClient>(options =>
            {
                // The address of your gRPC service.
                // This might be the same as your Blazor app's address if hosted together,
                // or a different address if the gRPC service is hosted elsewhere.
                options.Address = new Uri("https://localhost:50051/"); // Adjust if gRPC service is on a different URL
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                // Important for Blazor WASM: Use GrpcWebHandler
                return new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
            });

            // Register your GameViewModelRemoteClient for dependency injection
            // It depends on GameViewModelService.GameViewModelServiceClient which is now registered
            builder.Services.AddScoped<GameViewModelRemoteClient>(); // Or Singleton/Transient as appropriate

            await builder.Build().RunAsync();
        }
    }
}
