using Generated.ViewModels;
using PeakSWC.Mvvm.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            
            var dataValues = new List<double>();
            ExtractDataFromRemoteModel(remoteModel, dataValues);
            dataValues.Sort();
            var data = string.Join(",", dataValues);

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

    private static void ExtractDataFromRemoteModel(object remoteModel, List<double> dataValues)
    {
        var remoteModelType = remoteModel.GetType();
        var properties = remoteModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(remoteModel);
                if (value != null)
                {
                    ExtractValue(value, dataValues);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not extract value from property {prop.Name}: {ex.Message}");
            }
        }
    }

    private static void ExtractValue(object value, List<double> dataValues)
    {
        if (value == null) return;

        var type = value.GetType();

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
        {
            dataValues.Add(Convert.ToDouble(value));
        }
        else if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            dataValues.Add(Convert.ToDouble(value));
        }
        else if (type == typeof(bool))
        {
            dataValues.Add((bool)value ? 1.0 : 0.0);
        }
        else if (type == typeof(string))
        {
            if (double.TryParse((string)value, out var num))
            {
                dataValues.Add(num);
            }
        }
        else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                foreach (var item in enumerable)
                {
                    ExtractValue(item, dataValues);
                }
            }
        }
        else if (type.IsClass && type != typeof(string))
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var propValue = prop.GetValue(value);
                    if (propValue != null)
                    {
                        ExtractValue(propValue, dataValues);
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}