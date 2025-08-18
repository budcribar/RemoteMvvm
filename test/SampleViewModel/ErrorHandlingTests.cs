using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using GrpcRemoteMvvmModelUtil;

namespace SampleViewModel
{
    public class ErrorHandlingTests
    {
        static System.Collections.Generic.List<string> LoadDefaultRefs()
        {
            var list = new System.Collections.Generic.List<string>();
            string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (tpa != null)
            {
                foreach (var p in tpa.Split(Path.PathSeparator))
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
            }
            return list;
        }

        [Fact]
        public async Task MissingType_ReportsError()
        {
            var code = @"using CommunityToolkit.Mvvm.ComponentModel;
public partial class MissingTypeViewModel : ObservableObject
{
    [ObservableProperty]
    private UnknownType foo;
}";

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "MissingTypeViewModel.cs");
            await File.WriteAllTextAsync(filePath, code);

            var refs = LoadDefaultRefs();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ViewModelAnalyzer.AnalyzeAsync(new[] { filePath },
                "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute",
                "CommunityToolkit.Mvvm.Input.RelayCommandAttribute",
                refs));

            Assert.Contains("UnknownType", ex.Message);
        }
    }
}

