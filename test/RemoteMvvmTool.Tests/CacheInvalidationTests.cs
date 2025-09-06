using System;
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
        private static string GetCachePlatformRoot(string platform)
        {
            var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var repoRoot = Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            return Path.Combine(repoRoot, "test", "RemoteMvvmTool.Tests", "TestData", "GuiBuildCache", platform);
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
        public async Task Cache_Is_Evicted_When_Generator_Assembly_Hash_Changes()
        {
            string? uniquePlatform = null;
            try
            {
                // Use a unique pseudo-platform name to avoid warmup/background contention and existing cached builds.
                uniquePlatform = "wpf_cachetest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

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

                // Simulate generator assembly change by altering env.sig (only in this isolated platform folder)
                File.WriteAllText(envSigPath, firstSig + "_modified");

                // Second model variant (comment) gives different model hash to avoid reuse of prior in-memory task.
                var modelV2 = modelV1 + "\n// signature-change-trigger";
                using (var ctx2 = await SplitTestContext.CreateAsync(modelV2, uniquePlatform))
                {
                    ctx2.MarkTestPassed();
                }

                var secondSig = File.ReadAllText(envSigPath);
                var secondCount = CountCacheBuildDirs(uniquePlatform);

                Assert.NotEqual(firstSig + "_modified", secondSig); // should be restored to actual generator hash
                Assert.True(secondCount > 0);
                // After purge only new build(s) should exist
                Assert.True(secondCount <= firstCount);
            }
            finally
            {
                if (uniquePlatform != null)
                {
                    try
                    {
                        var cacheRoot = GetCachePlatformRoot(uniquePlatform);
                        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
                    }
                    catch
                    {
                        // Swallow cleanup failures so test result focuses on assertions
                    }
                }
            }
        }
    }
}
