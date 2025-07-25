using System.Text;

namespace RemoteMvvmTool.Generators;

public static class OptionsGenerator
{
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace PeakSWC.Mvvm.Remote");
        sb.AppendLine("{");
        sb.AppendLine("    public class ServerOptions");
        sb.AppendLine("    {");
        sb.AppendLine("        public int Port { get; set; } = MonsterClicker.NetworkConfig.Port;");
        sb.AppendLine("        public bool UseHttps { get; set; } = true;");
        sb.AppendLine("        public string? CorsPolicyName { get; set; } = \"AllowAll\";");
        sb.AppendLine("        public string[]? AllowedOrigins { get; set; } = null;");
        sb.AppendLine("        public string[]? AllowedHeaders { get; set; } = null;");
        sb.AppendLine("        public string[]? AllowedMethods { get; set; } = null;");
        sb.AppendLine("        public string[]? ExposedHeaders { get; set; } = null;");
        sb.AppendLine("        public string? LogLevel { get; set; } = \"Debug\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public class ClientOptions");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Address { get; set; } = MonsterClicker.NetworkConfig.ServerAddress;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
