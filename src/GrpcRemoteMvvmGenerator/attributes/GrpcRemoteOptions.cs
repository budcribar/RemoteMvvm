namespace PeakSWC.Mvvm.Remote
{
    public class ServerOptions
    {
        public int Port { get; set; } = 50052;
        public bool UseHttps { get; set; } = true;
        public string? CorsPolicyName { get; set; } = "AllowAll";
        public string[]? AllowedOrigins { get; set; } = null; // null = allow all
        public string[]? AllowedHeaders { get; set; } = null; // null = allow all
        public string[]? AllowedMethods { get; set; } = null; // null = allow all
        public string[]? ExposedHeaders { get; set; } = null; // null = default gRPC headers
        public string? LogLevel { get; set; } = "Debug"; // e.g. "Debug", "Information", "Warning"
        // Add other server options as needed
    }

    public class ClientOptions
    {
        public string Address { get; set; } = "https://localhost:50052";
        // Add other client options as needed
    }
}
