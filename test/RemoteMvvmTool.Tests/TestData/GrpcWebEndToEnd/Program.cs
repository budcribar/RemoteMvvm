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
