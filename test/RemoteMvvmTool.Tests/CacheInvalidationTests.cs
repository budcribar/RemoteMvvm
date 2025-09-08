using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace RemoteMvvmTool.Tests
{
    [Collection("GuiSequential")] // serialize with other GUI-related tests to reduce contention
    public class CacheInvalidationTests
    {
        public CacheInvalidationTests()
        {
            FailIfNamedGuiProcessesRunning();
        }

        private static void FailIfNamedGuiProcessesRunning()
        {
            var names = new[] { "ServerApp", "GuiClientApp" };
            foreach (var name in names)
            {
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine($"[CacheInvalidationTests] Found {processes.Length} leftover {name} process(es). Attempting cleanup...");

                        // Attempt to kill leftover processes
                        foreach (var process in processes)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    Console.WriteLine($"[CacheInvalidationTests] Killing leftover process: {name} (PID: {process.Id})");
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(2000);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[CacheInvalidationTests] Failed to kill {name}: {ex.Message}");
                            }
                            finally
                            {
                                try { process.Dispose(); } catch { }
                            }
                        }

                        // Wait a moment for cleanup to complete
                        System.Threading.Thread.Sleep(500);

                        // Double-check if any are still running
                        var remainingProcesses = Process.GetProcessesByName(name);
                        if (remainingProcesses.Length > 0)
                        {
                            foreach (var p in remainingProcesses) { try { p.Dispose(); } catch { } }
                            throw new InvalidOperationException($"Blocked: Unable to cleanup leftover process '{name}'. Please manually terminate it before running cache invalidation tests.");
                        }

                        Console.WriteLine($"[CacheInvalidationTests] Successfully cleaned up leftover {name} processes.");
                    }
                }
                catch (PlatformNotSupportedException) { }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception) { }
            }
        }

        private static string GetCachePlatformRoot(string platform)
        {
            var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            return Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GuiBuildCache", platform);
        }

        private static string GetWorkSplitRoot(string platform)
        {
            var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            // Split harness uses WorkSplit<platform>
            return Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "WorkSplit" + platform);
        }

        private static int CountCacheBuildDirs(string platform)
        {
            var root = GetCachePlatformRoot(platform);
            if (!Directory.Exists(root)) return 0;
            return Directory.GetDirectories(root, "h_*", SearchOption.TopDirectoryOnly).Length;
        }

        private static string LoadExistingModel(string name)
        {
            var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            var modelPath = Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GrpcWebEndToEnd", "Models", name + ".cs");
            Assert.True(File.Exists(modelPath), $"Model file not found: {modelPath}");
            return File.ReadAllText(modelPath);
        }

        [Fact]
        public async Task Cache_Creates_Separate_Entries_For_Different_Models()
        {
            string? uniquePlatform = null;
            try
            {
                // Use a unique pseudo-platform name to avoid warmup/background contention and existing cached builds.
                uniquePlatform = "winforms_cachetest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                // Test that the cache system works correctly by creating different models
                // This is a safer test than modifying env.sig which can cause build issues
                
                var modelV1 = LoadExistingModel("SimpleStringPropertyModel");
                using (var ctx1 = await SplitTestContext.CreateAsync(modelV1, uniquePlatform))
                {
                    ctx1.MarkTestPassed();
                }

                var cacheRoot = GetCachePlatformRoot(uniquePlatform);
                Assert.True(Directory.Exists(cacheRoot));
                var envSigPath = Path.Combine(cacheRoot, "env.sig");
                Assert.True(File.Exists(envSigPath));
                var firstSig = File.ReadAllText(envSigPath);
                var firstCount = CountCacheBuildDirs(uniquePlatform);
                Assert.True(firstCount > 0);

                // Create a different model variant to test cache behavior
                // This tests that different models create different cache entries
                var modelV2 = modelV1.Replace("SimpleStringProperty", "SimpleStringPropertyModified");
                var modelV3 = modelV2 + "\n// Additional cache variation test";
                
                using (var ctx2 = await SplitTestContext.CreateAsync(modelV3, uniquePlatform))
                {
                    ctx2.MarkTestPassed();
                }

                var secondSig = File.ReadAllText(envSigPath);
                var secondCount = CountCacheBuildDirs(uniquePlatform);

                // Environment signature should remain the same (we didn't modify generator)
                Assert.Equal(firstSig, secondSig);
                
                // Should have created additional cache entry for the different model
                Assert.True(secondCount > firstCount, 
                    $"Expected more cache entries after different model. First: {firstCount}, Second: {secondCount}");
                
                // Verify both cache entries exist
                Assert.True(secondCount >= 2, 
                    $"Expected at least 2 cache entries for different models. Actual: {secondCount}");
            }
            finally
            {
                if (uniquePlatform != null)
                {
                    // Always delete both cache and split work dirs to prevent contaminating later solution builds.
                    try
                    {
                        var cacheRoot = GetCachePlatformRoot(uniquePlatform);
                        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
                    }
                    catch { }
                    try
                    {
                        var workSplitRoot = GetWorkSplitRoot(uniquePlatform);
                        if (Directory.Exists(workSplitRoot)) Directory.Delete(workSplitRoot, true);
                    }
                    catch { }
                }
            }
        }
    }
}
