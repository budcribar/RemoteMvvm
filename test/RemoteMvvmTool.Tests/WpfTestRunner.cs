using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

// WPF and Winforms using statements
using System.Windows;
using System.Windows.Threading;
using SystemForms = System.Windows.Forms;

namespace RemoteMvvmTool.Tests;

/// <summary>
/// WPF test runner that creates a WPF application, connects to gRPC server,
/// and validates data synchronization between server and client.
/// </summary>
public class WpfTestRunner : IDisposable
{
    private System.Windows.Application? _wpfApp;
    private Dispatcher? _dispatcher;
    private bool _isDisposed = false;
    private readonly string _testProjectDir;
    private readonly int _port;
    private readonly string _expectedDataValues;
    private readonly string _platform = "wpf";

    public WpfTestRunner(string testProjectDir, int port, string expectedDataValues)
    {
        _testProjectDir = testProjectDir;
        _port = port;
        _expectedDataValues = expectedDataValues;
    }

    /// <summary>
    /// Runs the WPF end-to-end test by creating a WPF application and connecting to the server
    /// </summary>
    public async Task<string> RunWpfTestAsync()
    {
        var dataValues = new List<double>();
        Exception? testException = null;

        try
        {
            // Create WPF application on a STA thread
            await RunOnStaThreadAsync(async () =>
            {
                try
                {
                    _wpfApp = new System.Windows.Application();
                    _dispatcher = Dispatcher.CurrentDispatcher;

                    // Create the remote client
                    var clientOptions = new ClientOptions
                    {
                        Address = $"http://localhost:{_port}"
                    };

                    // Load the generated ViewModel assembly
                    var assemblyPath = FindAssemblyPath(_testProjectDir);
                    if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException($"Generated assembly not found in {_testProjectDir}\\bin\\Debug\\*\\TestProject.dll");
                    }

                    var assembly = Assembly.LoadFrom(assemblyPath);
                    var viewModelType = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name.Contains("TestViewModel") && !t.Name.Contains("RemoteClient"));

                    if (viewModelType == null)
                    {
                        throw new Exception("Could not find TestViewModel type in generated assembly");
                    }

                    // Create the ViewModel instance
                    var viewModel = Activator.CreateInstance(viewModelType, clientOptions);
                    if (viewModel == null)
                    {
                        throw new Exception("Failed to create ViewModel instance");
                    }

                    // Get the remote model
                    var getRemoteModelMethod = viewModelType.GetMethod("GetRemoteModel");
                    if (getRemoteModelMethod == null)
                    {
                        throw new Exception("GetRemoteModel method not found");
                    }

                    var remoteModelTask = (Task)getRemoteModelMethod.Invoke(viewModel, null)!;
                    await remoteModelTask;

                    var remoteModelProperty = viewModelType.GetProperty("Result");
                    if (remoteModelProperty == null)
                    {
                        // Try getting the result from the task
                        var resultProperty = remoteModelTask.GetType().GetProperty("Result");
                        var remoteModel = resultProperty?.GetValue(remoteModelTask);
                        if (remoteModel == null)
                        {
                            throw new Exception("Could not get remote model instance");
                        }

                        // Extract data from the remote model properties
                        ExtractDataFromRemoteModel(remoteModel, dataValues);
                    }
                    else
                    {
                        var remoteModel = remoteModelProperty.GetValue(remoteModelTask);
                        if (remoteModel != null)
                        {
                            ExtractDataFromRemoteModel(remoteModel, dataValues);
                        }
                    }

                    Console.WriteLine($"✅ WPF client connected and extracted {dataValues.Count} data values");
                }
                catch (Exception ex)
                {
                    testException = ex;
                    Console.WriteLine($"❌ WPF test failed: {ex.Message}");
                }
            });

            if (testException != null)
            {
                throw testException;
            }

            // Sort and format the data values
            var sortedValues = dataValues.OrderBy(x => x).ToList();
            return string.Join(",", sortedValues.Select(n => n % 1 == 0 ? n.ToString("F0") : n.ToString("G")));
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>
    /// Extracts numeric data from the remote model properties
    /// </summary>
    private void ExtractDataFromRemoteModel(object remoteModel, List<double> dataValues)
    {
        var remoteModelType = remoteModel.GetType();

        // Get all public properties
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

    /// <summary>
    /// Recursively extracts numeric values from objects
    /// </summary>
    private void ExtractValue(object value, List<double> dataValues)
    {
        if (value == null) return;

        var type = value.GetType();

        // Handle primitive types
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
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
            // Try to extract numbers from strings
            var str = (string)value;
            if (double.TryParse(str, out var num))
            {
                dataValues.Add(num);
            }
        }
        else if (typeof(IEnumerable<>).IsAssignableFrom(type) || type.IsArray)
        {
            // Handle collections
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
            // Handle complex objects - recurse into their properties
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
                    // Skip properties that can't be accessed
                }
            }
        }
    }

    /// <summary>
    /// Finds the TestProject.dll assembly in the bin/Debug directory structure
    /// </summary>
    private static string FindAssemblyPath(string testProjectDir)
    {
        var binDebugDir = Path.Combine(testProjectDir, "bin", "Debug");
        if (!Directory.Exists(binDebugDir))
        {
            return null;
        }

        // Look for any subdirectory that contains TestProject.dll
        foreach (var subDir in Directory.GetDirectories(binDebugDir))
        {
            var assemblyPath = Path.Combine(subDir, "TestProject.dll");
            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs code on a STA thread (required for WPF)
    /// </summary>
    private async Task RunOnStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                action().Wait();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await tcs.Task;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        try
        {
            if (_dispatcher != null && !_dispatcher.HasShutdownStarted)
            {
                _dispatcher.InvokeShutdown();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error shutting down WPF dispatcher: {ex.Message}");
        }

        _wpfApp = null;
        _dispatcher = null;
    }
}

/// <summary>
/// Winforms test runner that creates a Winforms application, connects to gRPC server,
/// and validates data synchronization between server and client.
/// </summary>
public class WinformsTestRunner : IDisposable
{
    private System.Windows.Forms.ApplicationContext? _appContext;
    private System.Windows.Forms.Control? _dispatcher;
    private bool _isDisposed = false;
    private readonly string _testProjectDir;
    private readonly int _port;
    private readonly string _expectedDataValues;
    private readonly string _platform = "winforms";

    public WinformsTestRunner(string testProjectDir, int port, string expectedDataValues)
    {
        _testProjectDir = testProjectDir;
        _port = port;
        _expectedDataValues = expectedDataValues;
    }

    /// <summary>
    /// Runs the Winforms end-to-end test by creating a Winforms application and connecting to the server
    /// </summary>
    public async Task<string> RunWinformsTestAsync()
    {
        var dataValues = new List<double>();
        Exception? testException = null;

        try
        {
            // Create Winforms application
            await Task.Run(async () =>
            {
                try
                {
                    // Create a control for synchronization
                    _dispatcher = new System.Windows.Forms.Control();
                    _dispatcher.CreateControl();

                    // Create the remote client
                    var clientOptions = new ClientOptions
                    {
                        Address = $"http://localhost:{_port}"
                    };

                    // Load the generated ViewModel assembly
                    var assemblyPath = FindAssemblyPath(_testProjectDir);
                    if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException($"Generated assembly not found in {_testProjectDir}\\bin\\Debug\\*\\TestProject.dll");
                    }

                    var assembly = Assembly.LoadFrom(assemblyPath);
                    var viewModelType = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name.Contains("TestViewModel") && !t.Name.Contains("RemoteClient"));

                    if (viewModelType == null)
                    {
                        throw new Exception("Could not find TestViewModel type in generated assembly");
                    }

                    // Create the ViewModel instance
                    var viewModel = Activator.CreateInstance(viewModelType, clientOptions);
                    if (viewModel == null)
                    {
                        throw new Exception("Failed to create ViewModel instance");
                    }

                    // Get the remote model
                    var getRemoteModelMethod = viewModelType.GetMethod("GetRemoteModel");
                    if (getRemoteModelMethod == null)
                    {
                        throw new Exception("GetRemoteModel method not found");
                    }

                    var remoteModelTask = (Task)getRemoteModelMethod.Invoke(viewModel, null)!;
                    await remoteModelTask;

                    var remoteModelProperty = viewModelType.GetProperty("Result");
                    if (remoteModelProperty == null)
                    {
                        // Try getting the result from the task
                        var resultProperty = remoteModelTask.GetType().GetProperty("Result");
                        var remoteModel = resultProperty?.GetValue(remoteModelTask);
                        if (remoteModel == null)
                        {
                            throw new Exception("Could not get remote model instance");
                        }

                        // Extract data from the remote model properties
                        ExtractDataFromRemoteModel(remoteModel, dataValues);
                    }
                    else
                    {
                        var remoteModel = remoteModelProperty.GetValue(remoteModelTask);
                        if (remoteModel != null)
                        {
                            ExtractDataFromRemoteModel(remoteModel, dataValues);
                        }
                    }

                    Console.WriteLine($"✅ Winforms client connected and extracted {dataValues.Count} data values");
                }
                catch (Exception ex)
                {
                    testException = ex;
                    Console.WriteLine($"❌ Winforms test failed: {ex.Message}");
                }
            });

            if (testException != null)
            {
                throw testException;
            }

            // Sort and format the data values
            var sortedValues = dataValues.OrderBy(x => x).ToList();
            return string.Join(",", sortedValues.Select(n => n % 1 == 0 ? n.ToString("F0") : n.ToString("G")));
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>
    /// Extracts numeric data from the remote model properties
    /// </summary>
    private void ExtractDataFromRemoteModel(object remoteModel, List<double> dataValues)
    {
        var remoteModelType = remoteModel.GetType();

        // Get all public properties
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

    /// <summary>
    /// Recursively extracts numeric values from objects
    /// </summary>
    private void ExtractValue(object value, List<double> dataValues)
    {
        if (value == null) return;

        var type = value.GetType();

        // Handle primitive types
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
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
            // Try to extract numbers from strings
            var str = (string)value;
            if (double.TryParse(str, out var num))
            {
                dataValues.Add(num);
            }
        }
        else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            // Handle collections
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
            // Handle complex objects - recurse into their properties
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
                    // Skip properties that can't be accessed
                }
            }
        }
    }

    /// <summary>
    /// Finds the TestProject.dll assembly in the bin/Debug directory structure
    /// </summary>
    private static string FindAssemblyPath(string testProjectDir)
    {
        var binDebugDir = Path.Combine(testProjectDir, "bin", "Debug");
        if (!Directory.Exists(binDebugDir))
        {
            return null;
        }

        // Look for any subdirectory that contains TestProject.dll
        foreach (var subDir in Directory.GetDirectories(binDebugDir))
        {
            var assemblyPath = Path.Combine(subDir, "TestProject.dll");
            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        try
        {
            if (_dispatcher != null && !_dispatcher.IsDisposed)
            {
                _dispatcher.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error disposing Winforms control: {ex.Message}");
        }

        _appContext = null;
        _dispatcher = null;
    }
}