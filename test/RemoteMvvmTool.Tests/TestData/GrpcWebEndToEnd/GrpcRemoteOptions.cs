namespace PeakSWC.Mvvm.Remote {
    public class ServerOptions {
        public int Port { get; set; }
        public bool UseHttps { get; set; } = false;
    }
    public class ClientOptions {
        public string Address { get; set; } = "http://localhost";
    }
}
