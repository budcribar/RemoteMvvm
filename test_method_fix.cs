    [Fact]
    public async Task SubscribeToPropertyChanges_Simple_Test()
    {
        var modelCode = """
            using CommunityToolkit.Mvvm.ComponentModel;
            using System.ComponentModel;
            using System.Diagnostics;

            namespace Generated.ViewModels
            {
                public partial class TestViewModel : ObservableObject
                {
                    public TestViewModel()
                    {
                        Debug.WriteLine("[TestViewModel] Constructor called");
                        Status = "Initial";
                        Debug.WriteLine("[TestViewModel] Set Status to 'Initial'");
                    }

                    [ObservableProperty]
                    private string _status = "Default";
                }
            }
            """;

        // **FIXED**: This test should verify UpdatePropertyValue response, not PropertyChanged streaming
        // PropertyChanged streaming should be tested separately with server-initiated changes
        await TestEndToEndScenario(modelCode, "", "test-update-simple.js", null);
    }
}