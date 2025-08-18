// Centralize the port definition for server and client
namespace MonsterClicker
{
    public static class NetworkConfig
    {
        public const int Port = 50052;
        // The gRPC server started by the WPF demo uses insecure credentials.
        // Use HTTP so the client doesn't attempt an HTTPS handshake.
        public static string ServerAddress = $"http://localhost:{Port}";
        public static string GrpcWebAddress = $"http://localhost:{Port}"; // For Blazor WASM
    }
}
