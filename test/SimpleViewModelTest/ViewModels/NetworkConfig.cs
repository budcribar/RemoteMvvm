namespace SimpleViewModelTest.ViewModels
{
    public static class NetworkConfig
    {
        public const int Port = 50055;
        public static string ServerAddress = $"http://localhost:{Port}";
        public static string GrpcWebAddress = $"http://localhost:{Port}";
    }
}
