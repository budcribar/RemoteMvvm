namespace RemoteMvvmTool.Tests;

/// <summary>
/// Simple client options for testing WPF and Winforms clients
/// </summary>
public class ClientOptions
{
    public string Address { get; set; } = "http://localhost";
}

/// <summary>
/// Simple server options for testing
/// </summary>
public class ServerOptions
{
    public int Port { get; set; }
    public bool UseHttps { get; set; } = false;
}