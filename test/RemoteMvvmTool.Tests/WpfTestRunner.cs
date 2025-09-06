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
using System.Globalization;

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
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? _testProjectDir,
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
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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

            // Always dump client output for diagnostics
            Console.WriteLine("CLIENT_OUTPUT_START");
            Console.WriteLine(outputString);
            Console.WriteLine("CLIENT_OUTPUT_END");

            // Use common parsing helper for consistency across GUI runners
            var extracted = GuiParsingHelpers.ExtractNumericDataFromOutput(outputString);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted;
            }

            // No numeric data parsed. Return empty to avoid format exceptions upstream
            return string.Empty;
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
            if (double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var num))
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
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? _testProjectDir,
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
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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

            // Always dump client output for diagnostics
            Console.WriteLine("CLIENT_OUTPUT_START");
            Console.WriteLine(outputString);
            Console.WriteLine("CLIENT_OUTPUT_END");

            // Prefer explicit CLIENT_DATA payload if present, but parse it for numeric values
            var dataLine = outputString.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                       .FirstOrDefault(s => s.StartsWith("CLIENT_DATA:"));
            if (dataLine != null)
            {
                var payload = dataLine.Substring("CLIENT_DATA:".Length).Trim();
                var extractedFromClientData = ExtractNumericDataFromOutput(payload);
                if (!string.IsNullOrWhiteSpace(extractedFromClientData))
                {
                    return extractedFromClientData;
                }
            }

            // Fallback: parse the whole output
            var extracted = ExtractNumericDataFromOutput(outputString);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted;
            }

            // No numeric data parsed. Return empty to avoid format exceptions upstream
            return string.Empty;
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
    /// Extracts numeric data from any output payload, matching the TypeScript parsing logic.
    /// </summary>
    private static string ExtractNumericDataFromOutput(string output)
    {
        // Reuse common helper to keep behavior identical across GUI clients
        return GuiParsingHelpers.ExtractNumericDataFromOutput(output);
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
            if (double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var num))
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

// Internal helper to reuse the exact parsing logic across GUI runners
internal static class GuiParsingHelpers
{
    public static string ExtractNumericDataFromOutput(string output)
    {
        // Delegate to a single implementation to avoid duplication drift
        var numbers = new List<double>();
        var lines = output.Split('\n');
        bool foundFlatData = false;

        foreach (var rawLine in lines)
        {
            var trimmedLine = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) ||
                trimmedLine.StartsWith("Starting gRPC-Web test") ||
                trimmedLine.StartsWith("npm ") ||
                trimmedLine.StartsWith("✅ Generated") ||
                trimmedLine.StartsWith("node:"))
            {
                continue;
            }

            if (trimmedLine.StartsWith("FLAT_DATA:") || trimmedLine.StartsWith("CLIENT_DATA:"))
            {
                var jsonStart = trimmedLine.IndexOf("{");
                if (jsonStart >= 0)
                {
                    var jsonData = trimmedLine.Substring(jsonStart);
                    ExtractAllNumbersFromJson(jsonData, numbers);
                    foundFlatData = true;
                }
                else
                {
                    var payload = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1);
                    ExtractNumbersFromLine(payload, numbers);
                    foundFlatData = true;
                }
                break;
            }
        }

        if (!foundFlatData)
        {
            foreach (var rawLine in lines)
            {
                var trimmedLine = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) ||
                    trimmedLine.StartsWith("Starting gRPC-Web test") ||
                    trimmedLine.StartsWith("npm ") ||
                    trimmedLine.StartsWith("✅") ||
                    trimmedLine.StartsWith("node:") ||
                    trimmedLine.Contains("=== TestViewModel Data") ||
                    trimmedLine.StartsWith("RESPONSE_DATA:"))
                {
                    continue;
                }
                ExtractNumbersFromLine(trimmedLine, numbers);
            }
        }

        // If we only extracted zeros (likely noise), treat as no data
        if (numbers.Count == 0 || numbers.All(n => n == 0))
        {
            return string.Empty;
        }

        var sortedNumbers = numbers.OrderBy(x => x).ToList();
        return string.Join(",", sortedNumbers.Select(n => n % 1 == 0 ? n.ToString("F0") : n.ToString("G", CultureInfo.InvariantCulture)));
    }

    private static void ExtractAllNumbersFromJson(string jsonData, List<double> numbers)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(jsonData);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.EndsWith("_asb64") && property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var base64Value = property.Value.GetString();
                    if (!string.IsNullOrEmpty(base64Value))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(base64Value);
                            foreach (var b in bytes) numbers.Add(b);
                        }
                        catch (FormatException) { }
                    }
                }
                else
                {
                    ExtractAllNumbersFromJsonValue(property.Value, numbers);
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            ExtractNumbersFromLine(jsonData, numbers);
        }
    }

    private static void ExtractAllNumbersFromJsonValue(System.Text.Json.JsonElement element, List<double> numbers)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetDouble(out var numValue)) numbers.Add(numValue);
                break;
            case System.Text.Json.JsonValueKind.String:
                var strValue = element.GetString();
                if (!string.IsNullOrEmpty(strValue))
                {
                    if (IsLikelyNumericString(strValue))
                    {
                        if (double.TryParse(strValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedNum)) numbers.Add(parsedNum);
                    }
                    else if (strValue.Length == 36 && strValue.Count(c => c == '-') == 4)
                    {
                        var lastDash = strValue.LastIndexOf('-');
                        if (lastDash >= 0)
                        {
                            var lastSegment = strValue.Substring(lastDash + 1);
                            if (lastSegment != "000000000000" && !lastSegment.All(c => c == '0'))
                            {
                                var trailingDigits = lastSegment.TrimStart('0');
                                if (!string.IsNullOrEmpty(trailingDigits) && double.TryParse(trailingDigits, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var guidNum)) numbers.Add(guidNum);
                            }
                        }
                    }
                }
                break;
            case System.Text.Json.JsonValueKind.True:
                numbers.Add(1);
                break;
            case System.Text.Json.JsonValueKind.False:
                numbers.Add(0);
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) ExtractAllNumbersFromJsonValue(item, numbers);
                break;
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (double.TryParse(prop.Name, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var keyNum)) numbers.Add(keyNum);
                    else if (prop.Name.Contains('.'))
                    {
                        var lastPart = prop.Name.Substring(prop.Name.LastIndexOf('.') + 1);
                        if (double.TryParse(lastPart, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var flattenedKeyNum)) numbers.Add(flattenedKeyNum);
                    }
                    ExtractAllNumbersFromJsonValue(prop.Value, numbers);
                }
                break;
        }
    }

    private static bool IsLikelyNumericString(string value)
    {
        if (value.Length == 36 && value.Count(c => c == '-') == 4) return false;
        if (value.Contains('-') && value.Length > 10) return false;
        if (value.All(c => c == '0')) return false;
        if (value.Length > 30) return false; // allow longer numeric strings
        return value.All(c => char.IsDigit(c) || c == '.' || c == '-');
    }

    private static void ExtractNumbersFromLine(string line, List<double> numbers)
    {
        var processedLine = line.Replace("true", "1").Replace("false", "0");
        if (line.TrimStart().StartsWith("{") && line.TrimEnd().EndsWith("}"))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(line);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    ExtractAllNumbersFromJsonValue(property.Value, numbers);
                }
                return;
            }
            catch (System.Text.Json.JsonException) { }
        }
        var delimiters = new char[] { ' ', ',', ':', '[', ']', '{', '}', '"', '=', '(', ')', ';', '\t' };
        var words = processedLine.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var cleanWord = word.Trim();
            // Always try to parse as a number first using invariant culture
            if (double.TryParse(cleanWord, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number))
            {
                numbers.Add(number);
                continue;
            }
            if (IsNonNumericWord(cleanWord)) continue;
            ExtractNumbersFromString(cleanWord, numbers);
        }
    }

    private static bool IsNonNumericWord(string word)
    {
        var nonNumericWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "starting", "grpc-web", "test", "using", "generated", "client",
            "npm", "node", "data", "start", "end", "response", "flat",
            "status", "message", "counter", "enabled", "level", "bonus",
            "multiplier", "current", "game", "testviewmodel"
        };
        // Do not filter by length before attempting numeric parse
        return nonNumericWords.Contains(word) || (word.Contains('-') && word.Length > 10);
    }

    private static void ExtractNumbersFromString(string cleanWord, List<double> numbers)
    {
        if (cleanWord.Length > 40 || cleanWord.Count(c => c == '-') > 2) return;
        var numericChars = new System.Text.StringBuilder();
        bool hasDecimalPoint = false;
        foreach (char c in cleanWord)
        {
            if (char.IsDigit(c)) numericChars.Append(c);
            else if (c == '.' && !hasDecimalPoint && numericChars.Length > 0)
            {
                numericChars.Append(c);
                hasDecimalPoint = true;
            }
            else if (c == '-' && numericChars.Length == 0)
            {
                numericChars.Append(c);
            }
            else if (numericChars.Length > 0)
            {
                var numberStr = numericChars.ToString();
                if (numberStr.Length > 0 && numberStr != "-" && double.TryParse(numberStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var extractedNumber)) numbers.Add(extractedNumber);
                numericChars.Clear();
                hasDecimalPoint = false;
            }
        }
        if (numericChars.Length > 0)
        {
            var numberStr = numericChars.ToString();
            if (numberStr.Length > 0 && numberStr != "-" && double.TryParse(numberStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var finalNumber)) numbers.Add(finalNumber);
        }
    }
}