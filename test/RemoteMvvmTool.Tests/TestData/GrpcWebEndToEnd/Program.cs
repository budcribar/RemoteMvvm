using Generated.ViewModels;
using PeakSWC.Mvvm.Remote;
using System;
using System.Threading.Tasks;

namespace TestProject;

public class Program
{
    public static async Task Main(string[] args)
    {
        var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5000;
        
        // Check if we are running as a server or client
        if (args.Length > 1 && args[1] == "client")
        {
            Console.WriteLine($"Starting client on port {port}...");
            var clientOptions = new ClientOptions { Address = $"http://localhost:{port}" };
            TestViewModel viewModel = new TestViewModel(clientOptions);
            var remoteModel = await viewModel.GetRemoteModel();
            var data = await viewModel.ExtractAndPrintData();
            Console.WriteLine($"CLIENT_DATA:{data}");
        }
        else
        {
            Console.WriteLine($"Starting server on port {port}...");
            
            var serverOpts = new ServerOptions { Port = port };
            TestViewModel viewModel = new TestViewModel(serverOpts);
            
            Console.WriteLine($"Server ready on port {port}");
            
            // Wait for termination signal
            var tcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                tcs.SetResult(true);
            };
            
            await tcs.Task;
            Console.WriteLine("Stopping server...");
        }
    }
}
