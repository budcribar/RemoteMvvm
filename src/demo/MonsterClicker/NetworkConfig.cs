// Centralize the port definition for server and client
namespace MonsterClicker
{
    public static class NetworkConfig
    {
        public const int Port = 50052;
        public static string ServerAddress = $"https://localhost:{Port}";
        public static string GrpcWebAddress = $"http://localhost:{Port}"; // For Blazor WASM
    }
}
