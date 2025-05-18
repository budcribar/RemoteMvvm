using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http; // For HttpClient
using Grpc.Net.Client;
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

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            await builder.Build().RunAsync();
        }
    }
}
