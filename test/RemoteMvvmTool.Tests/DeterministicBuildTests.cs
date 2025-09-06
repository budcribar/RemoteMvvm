using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RemoteMvvmTool.Tests
{
    public class DeterministicBuildTests
    {
        private static string RepoRoot
        {
            get
            {
                var baseTestDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                return Path.GetFullPath(Path.Combine(baseTestDir, "../../../../.."));
            }
        }

        private static string ToolProjectDir => Path.Combine(RepoRoot, "src", "RemoteMvvmTool");
        private static string ToolDllPath => Path.Combine(ToolProjectDir, "bin", "Debug", "net8.0", "RemoteMvvmTool.dll");

        [Fact]
        public async Task Deterministic_Build_Output_Has_Stable_Hash()
        {
            // Build #1 (clean)
            ForceClean();
            await BuildAsync();
            var hash1 = ComputeFileSha256(ToolDllPath);
            var mvid1 = ReadMvid(ToolDllPath);

            // Small delay to avoid any race (not strictly required)
            await Task.Delay(150);

            // Build #2 (clean again)
            ForceClean();
            await BuildAsync();
            var hash2 = ComputeFileSha256(ToolDllPath);
            var mvid2 = ReadMvid(ToolDllPath);

            // Assert stable output (will FAIL if <Deterministic>false in csproj which is what you wanted to validate)
            if (hash1 != hash2)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Deterministic build expectation failed: file hash changed.\n" +
                    $"Hash1: {hash1}\nHash2: {hash2}\nMVID1: {mvid1}\nMVID2: {mvid2}\n" +
                    "Check that <Deterministic>true</Deterministic> is set and no nondeterministic code (timestamps, GUIDs, etc.) is injected.");
            }

            // Secondary safety: if hashes equal but MVID differs, surface that (indicates unusual metadata stripping)
            if (mvid1 != mvid2)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Hashes equal but MVID differs (unexpected in deterministic scenario).\nHash: {hash1}\nMVID1: {mvid1}\nMVID2: {mvid2}");
            }
        }

        private static void ForceClean()
        {
            TryDelete(Path.Combine(ToolProjectDir, "bin"));
            TryDelete(Path.Combine(ToolProjectDir, "obj"));

            static void TryDelete(string dir)
            {
                if (!Directory.Exists(dir)) return;
                for (int i = 0; i < 3; i++)
                {
                    try { Directory.Delete(dir, true); return; }
                    catch { System.Threading.Thread.Sleep(100); }
                }
            }
        }

        private static async Task BuildAsync()
        {
            await RunDotnet("build --nologo -c Debug -t:Rebuild", ToolProjectDir);
            Assert.True(File.Exists(ToolDllPath), "Expected tool DLL after build.");
        }

        private static string ComputeFileSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        private static Guid ReadMvid(string assemblyPath)
        {
            // Load into memory to avoid file locking
            var bytes = File.ReadAllBytes(assemblyPath);
            var asm = Assembly.Load(bytes);
            return asm.ManifestModule.ModuleVersionId;
        }

        private static async Task RunDotnet(string args, string workDir)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var p = System.Diagnostics.Process.Start(psi)!;
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
                throw new Exception($"dotnet {args} failed.\nOUT:\n{sbOut}\nERR:\n{sbErr}");
        }
    }
}
