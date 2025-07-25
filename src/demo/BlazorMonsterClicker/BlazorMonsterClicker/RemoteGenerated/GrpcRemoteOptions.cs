namespace PeakSWC.Mvvm.Remote
{
    public class ServerOptions
    {
        public int Port { get; set; } = MonsterClicker.NetworkConfig.Port;
        public bool UseHttps { get; set; } = true;
        public string? CorsPolicyName { get; set; } = "AllowAll";
        public string[]? AllowedOrigins { get; set; } = null;
        public string[]? AllowedHeaders { get; set; } = null;
        public string[]? AllowedMethods { get; set; } = null;
        public string[]? ExposedHeaders { get; set; } = null;
        public string? LogLevel { get; set; } = "Debug";
    }

    public class ClientOptions
    {
        public string Address { get; set; } = MonsterClicker.NetworkConfig.ServerAddress;
    }
}
