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
    private readonly string _platform;

    public WpfTestRunner(string testProjectDir, int port, string expectedDataValues, string platform)
    {
        _testProjectDir = testProjectDir;
        _port = port;
        _expectedDataValues = expectedDataValues;
        _platform = platform;
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
            Console.WriteLine("WpfTestRunner: Starting RunWpfTestAsync");
            
            var exePath = FindExecutablePath(_testProjectDir);
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                throw new FileNotFoundException($"Generated executable not found in {_testProjectDir}\\bin\\Debug\\*\\TestProject.exe");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"{_port} client", // Pass port and "client" argument
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            var output = new System.Text.StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            // Add timeout to prevent hanging
            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var exitTask = process.WaitForExitAsync();

            var completedTask = await Task.WhenAny(exitTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                Console.WriteLine("Process timed out, killing it...");
                process.Kill();
                throw new Exception("Test application timed out after 30 seconds");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"Test application exited with code {process.ExitCode}. Output: {output}");
            }

            var outputString = output.ToString();
            var dataLine = outputString.Split(new[] { Environment.NewLine }, StringSplitOptions.None).FirstOrDefault(s => s.StartsWith("CLIENT_DATA:"));
            if (dataLine != null)
            {
                return dataLine.Substring("CLIENT_DATA:".Length).Trim();
            }
            
            // If no data line is found, return the full output for debugging
            return outputString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WpfTestRunner: Exception in RunWpfTestAsync: {ex}");
            throw;
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
    /// Finds the TestProject.exe executable in the bin directory structure.
    /// </summary>
    private static string FindExecutablePath(string testProjectDir)
    {
        var binDir = Path.Combine(testProjectDir, "bin");
        if (!Directory.Exists(binDir))
        {
            return null;
        }

        // Search recursively for TestProject.exe in Debug and Release folders
        var executables = Directory.GetFiles(binDir, "TestProject.exe", SearchOption.AllDirectories);

        var debugExecutable = executables.FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (debugExecutable != null)
        {
            return debugExecutable;
        }

        var releaseExecutable = executables.FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (releaseExecutable != null)
        {
            return releaseExecutable;
        }

        return executables.FirstOrDefault();
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
    public WinformsTestRunner(string testProjectDir, int port, string expectedDataValues, string platform)
    {
        _testProjectDir = testProjectDir;
        _port = port;
        _expectedDataValues = expectedDataValues;
        _platform = platform;
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
            Console.WriteLine("WinformsTestRunner: Starting RunWinformsTestAsync");

            var exePath = FindExecutablePath(_testProjectDir);
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                throw new FileNotFoundException($"Generated executable not found in {_testProjectDir}\\bin\\Debug\\*\\TestProject.exe");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"{_port} client", // Pass port and "client" argument
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            var output = new System.Text.StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            // Add timeout to prevent hanging
            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var exitTask = process.WaitForExitAsync();

            var completedTask = await Task.WhenAny(exitTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                Console.WriteLine("Process timed out, killing it...");
                process.Kill();
                throw new Exception("Test application timed out after 30 seconds");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"Test application exited with code {process.ExitCode}. Output: {output}");
            }

            var outputString = output.ToString();
            var dataLine = outputString.Split(new[] { Environment.NewLine }, StringSplitOptions.None).FirstOrDefault(s => s.StartsWith("CLIENT_DATA:"));
            if (dataLine != null)
            {
                return dataLine.Substring("CLIENT_DATA:".Length).Trim();
            }

            // If no data line is found, return the full output for debugging
            return outputString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WinformsTestRunner: Exception in RunWinformsTestAsync: {ex}");
            throw;
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
    /// Finds the TestProject.exe executable in the bin directory structure.
    /// </summary>
    private static string FindExecutablePath(string testProjectDir)
    {
        var binDir = Path.Combine(testProjectDir, "bin");
        if (!Directory.Exists(binDir))
        {
            return null;
        }

        // Search recursively for TestProject.exe in Debug and Release folders
        var executables = Directory.GetFiles(binDir, "TestProject.exe", SearchOption.AllDirectories);

        var debugExecutable = executables.FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (debugExecutable != null)
        {
            return debugExecutable;
        }

        var releaseExecutable = executables.FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (releaseExecutable != null)
        {
            return releaseExecutable;
        }

        return executables.FirstOrDefault();
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