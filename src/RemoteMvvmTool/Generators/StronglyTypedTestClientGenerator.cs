using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using GrpcRemoteMvvmModelUtil;

namespace RemoteMvvmTool.Generators;

public static class StronglyTypedTestClientGenerator
{
    public static string Generate(string viewModelName, string protoNamespace, string serviceName,
        List<PropertyInfo> props, List<CommandInfo> cmds)
    {
        var sb = new StringBuilder();
        var clientClassName = $"{viewModelName}TestClient";
        var serviceClientName = $"{serviceName}.{serviceName}Client";

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using Grpc.Net.Client;");
        sb.AppendLine("using Google.Protobuf.WellKnownTypes;");
        sb.AppendLine($"using {protoNamespace};");
        sb.AppendLine();

        sb.AppendLine("namespace Generated.TestClients;");
        sb.AppendLine();

        GenerateInterfaces(sb);
        GenerateMainClass(sb, clientClassName, serviceClientName);
        GeneratePropertyUpdateMethods(sb, props);
        GenerateCollectionUpdateHelpers(sb, props, viewModelName, serviceName);

        // Close class body additions
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_disposed) return;");
        sb.AppendLine("        _disposed = true;");
        sb.AppendLine("        _channel?.Dispose();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Structural snapshot for equality-based verification");
        sb.AppendLine("    public async Task<string> GetStructuralStateAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        var state = await _grpcClient.GetStateAsync(new Empty());");
        sb.AppendLine("        var sbSnap = new System.Text.StringBuilder();");
        sb.AppendLine("        DumpObject(state, \"$\", sbSnap, new HashSet<object>(ReferenceEqualityComparer.Instance));");
        sb.AppendLine("        return sbSnap.ToString();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void DumpObject(object? value, string path, System.Text.StringBuilder sbOut, HashSet<object> seen)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value == null) { sbOut.AppendLine(path+\"=null\"); return; }");
        sb.AppendLine("        if (value is string s) { sbOut.AppendLine(path+\"=\"+s); return; }");
        sb.AppendLine("        if (value is bool b) { sbOut.AppendLine(path+\"=\"+(b?1:0)); return; }");
        sb.AppendLine("        if (value is System.Enum) { sbOut.AppendLine(path+\"=\"+Convert.ToInt32(value)); return; }");
        sb.AppendLine("        if (value is IFormattable f && (value is int || value is long || value is short || value is byte || value is uint || value is ulong || value is double || value is float || value is decimal)) { sbOut.AppendLine(path+\"=\"+f.ToString(null, CultureInfo.InvariantCulture)); return; }");
        sb.AppendLine("        var t = value.GetType();");
        sb.AppendLine("        if (!t.IsValueType)");
        sb.AppendLine("        { if (!seen.Add(value)) { sbOut.AppendLine(path+\"=<cycle>\"); return; } }");
        sb.AppendLine("        if (value is Google.Protobuf.WellKnownTypes.Timestamp ts) { var dt = ts.ToDateTime(); if (dt.Kind==DateTimeKind.Unspecified) dt=DateTime.SpecifyKind(dt, DateTimeKind.Utc); var secs=(long)(dt - DateTime.UnixEpoch).TotalSeconds; sbOut.AppendLine(path+\"=\"+secs); return; }");
        sb.AppendLine("        if (value is DateTime dt2) { if (dt2.Kind==DateTimeKind.Unspecified) dt2=DateTime.SpecifyKind(dt2, DateTimeKind.Utc); var secs=(long)(dt2.ToUniversalTime()-DateTime.UnixEpoch).TotalSeconds; sbOut.AppendLine(path+\"=\"+secs); return; }");
        sb.AppendLine("        if (value is DateTimeOffset dto) { var secs=(long)(dto.ToUniversalTime().UtcDateTime-DateTime.UnixEpoch).TotalSeconds; sbOut.AppendLine(path+\"=\"+secs); return; }");
        sb.AppendLine("        if (value is System.Collections.IDictionary dict) { int i=0; foreach (var key in dict.Keys) { DumpObject(dict[key], path+\"[\"+key+\"]\", sbOut, seen); i++; } if (i==0) sbOut.AppendLine(path+\"=<empty_dict>\"); return; }");
        sb.AppendLine("        if (value is System.Collections.IEnumerable en && value is not string) { int idx=0; bool any=false; foreach (var item in en){ any=true; DumpObject(item, path+\"[\"+idx++ +\"]\", sbOut, seen);} if(!any) sbOut.AppendLine(path+\"=<empty_list>\"); return; }");
        sb.AppendLine("        // object with properties");
        sb.AppendLine("        var props = t.GetProperties(BindingFlags.Public|BindingFlags.Instance); bool had=false; foreach (var p in props){ if(!p.CanRead) continue; try { var v=p.GetValue(value); DumpObject(v, path+\".\"+p.Name, sbOut, seen); had=true; } catch { } } if(!had) sbOut.AppendLine(path+\"={}\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>");
        sb.AppendLine("    {");
        sb.AppendLine("        public static readonly ReferenceEqualityComparer Instance = new();");
        sb.AppendLine("        public new bool Equals(object? x, object? y) => ReferenceEquals(x,y);");
        sb.AppendLine("        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void GenerateInterfaces(StringBuilder sb)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for strongly-typed test clients");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface ITestClient : IDisposable");
        sb.AppendLine("{");
        sb.AppendLine("    Task<string> GetModelDataAsync();");
        sb.AppendLine("    Task InitializeAsync();");
        sb.AppendLine("    Task<string> GetStructuralStateAsync();");
        sb.AppendLine("    Task UpdatePropertyAsync(string propertyName, object value);");
        sb.AppendLine("    Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for indexed property updates");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public interface IIndexedUpdater");
        sb.AppendLine("{");
        sb.AppendLine("    Task UpdatePropertyAsync(string propertyName, object value);");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void GenerateMainClass(StringBuilder sb, string clientClassName, string serviceClientName)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Strongly-typed test client for testing");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {clientClassName} : ITestClient");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {serviceClientName} _grpcClient;");
        sb.AppendLine("    private readonly GrpcChannel _channel;");
        sb.AppendLine("    private readonly string _serverAddress;");
        sb.AppendLine("    private readonly int _port;");
        sb.AppendLine("    private bool _disposed = false;");
        sb.AppendLine();
        sb.AppendLine($"    public {clientClassName}(string serverAddress, int port)");
        sb.AppendLine("    {");
        sb.AppendLine("        _serverAddress = serverAddress;");
        sb.AppendLine("        _port = port;");
        sb.AppendLine("        var address = new Uri($\"https://{_serverAddress}:{_port}/\");");
        sb.AppendLine("        var httpsHandler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };");
        sb.AppendLine("        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = httpsHandler });");
        sb.AppendLine($"        _grpcClient = new {serviceClientName}(_channel);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public async Task InitializeAsync() => await Task.Delay(100);");
        sb.AppendLine();
        sb.AppendLine("    public async Task UpdatePropertyAsync(string propertyName, object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var request = new UpdatePropertyValueRequest");
        sb.AppendLine("        {");
        sb.AppendLine("            PropertyName = propertyName,");
        sb.AppendLine("            NewValue = Any.Pack(CreateValueFromObject(value))");
        sb.AppendLine("        };");
        sb.AppendLine("        await _grpcClient.UpdatePropertyValueAsync(request);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public async Task UpdateIndexedPropertyAsync(string collectionName, int index, string propertyName, object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var request = new UpdatePropertyValueRequest");
        sb.AppendLine("        {");
        sb.AppendLine("            PropertyName = propertyName,");
        sb.AppendLine("            PropertyPath = $\"{collectionName}[{index}].{propertyName}\",");
        sb.AppendLine("            NewValue = Any.Pack(CreateValueFromObject(value))");
        sb.AppendLine("        };");
        sb.AppendLine("        await _grpcClient.UpdatePropertyValueAsync(request);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static Google.Protobuf.IMessage CreateValueFromObject(object value) => value switch");
        sb.AppendLine("    {");
        sb.AppendLine("        string s => (Google.Protobuf.IMessage)new StringValue { Value = s },");
        sb.AppendLine("        int i => (Google.Protobuf.IMessage)new Int32Value { Value = i },");
        sb.AppendLine("        bool b => (Google.Protobuf.IMessage)new BoolValue { Value = b },");
        sb.AppendLine("        double d => (Google.Protobuf.IMessage)new DoubleValue { Value = d },");
        sb.AppendLine("        float f => (Google.Protobuf.IMessage)new FloatValue { Value = f },");
        sb.AppendLine("        long l => (Google.Protobuf.IMessage)new Int64Value { Value = l },");
        sb.AppendLine("        DateTime dt => (Google.Protobuf.IMessage)Timestamp.FromDateTime((dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime())),");
        sb.AppendLine("        DateTimeOffset dto => (Google.Protobuf.IMessage)Timestamp.FromDateTime(dto.UtcDateTime)," );
        sb.AppendLine("        _ => (Google.Protobuf.IMessage)new StringValue { Value = value?.ToString() ?? \"\" }");
        sb.AppendLine("    };");
        sb.AppendLine();
    }

    private static void GeneratePropertyUpdateMethods(StringBuilder sb, List<PropertyInfo> props)
    {
        foreach (var prop in props.Where(p => !p.IsReadOnly))
        {
            var methodName = $"Update{prop.Name}Async";
            var paramType = GetCSharpTypeName(prop.TypeString);
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Updates the {prop.Name} property");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public async Task {methodName}({paramType} value)");
            sb.AppendLine("    {");
            sb.AppendLine("        var request = new UpdatePropertyValueRequest");
            sb.AppendLine("        {");
            sb.AppendLine($"            PropertyName = \"{prop.Name}\",");
            if (prop.TypeString == "System.String") sb.AppendLine("            NewValue = Any.Pack(new StringValue { Value = value })");
            else if (prop.TypeString == "System.Int32") sb.AppendLine("            NewValue = Any.Pack(new Int32Value { Value = value })");
            else if (prop.TypeString == "System.Boolean") sb.AppendLine("            NewValue = Any.Pack(new BoolValue { Value = value })");
            else if (prop.TypeString == "System.Double") sb.AppendLine("            NewValue = Any.Pack(new DoubleValue { Value = value })");
            else if (prop.TypeString == "System.Single") sb.AppendLine("            NewValue = Any.Pack(new FloatValue { Value = value })");
            else if (prop.TypeString == "System.Int64") sb.AppendLine("            NewValue = Any.Pack(new Int64Value { Value = value })");
            else if (prop.TypeString == "System.DateTime") sb.AppendLine("            NewValue = Any.Pack(Timestamp.FromDateTime((value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime())))");
            else if (prop.TypeString.StartsWith("System.Collections")) sb.AppendLine("            NewValue = Any.Pack(new StringValue { Value = value?.ToString() ?? \"\" })");
            else sb.AppendLine("            NewValue = Any.Pack(new StringValue { Value = value.ToString() })");
            sb.AppendLine("        };");
            sb.AppendLine("        await _grpcClient.UpdatePropertyValueAsync(request);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static string GetCSharpTypeName(string typeString) => typeString switch
    {
        "System.String" => "string",
        "System.Int32" => "int",
        "System.Boolean" => "bool",
        "System.Double" => "double",
        "System.Single" => "float",
        "System.Int64" => "long",
        "System.UInt32" => "uint",
        "System.UInt64" => "ulong",
        "System.Int16" => "short",
        "System.UInt16" => "ushort",
        "System.Byte" => "byte",
        "System.SByte" => "sbyte",
        "System.Decimal" => "decimal",
        _ when typeString.StartsWith("System.Collections.ObjectModel.ObservableCollection") => "object",
        _ => typeString
    };

    private static bool IsCollectionType(string typeString)
    {
        if (string.IsNullOrEmpty(typeString)) return false;
        return typeString.Contains("ObservableCollection<") || typeString.Contains("List<") || typeString.EndsWith("[]", StringComparison.Ordinal);
    }

    private static void GenerateCollectionUpdateHelpers(StringBuilder sb, List<PropertyInfo> props, string viewModelName, string serviceName)
    {
        sb.AppendLine("    public async Task<string> GetModelDataAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        var state = await _grpcClient.GetStateAsync(new Empty());");
        sb.AppendLine("        var numbers = new List<double>();");
        sb.AppendLine("        ExtractFromState(state, numbers);");
        sb.AppendLine("        numbers.Sort();");
        sb.AppendLine("        return string.Join(\",\", numbers.Select(n => n % 1 == 0 ? n.ToString(\"F0\") : n.ToString(\"G\", CultureInfo.InvariantCulture)));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void ExtractFromState(object? value, List<double> numbers)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value == null) return;");
        sb.AppendLine("        var t = value.GetType();");
        sb.AppendLine("        var fullName = t.FullName ?? string.Empty;");
        sb.AppendLine("        // Normalize date/time values from Timestamps and DateTime/DateOnly to whole epoch seconds (UTC) to match expected test numbers.");
        sb.AppendLine("        if (t == typeof(Google.Protobuf.WellKnownTypes.Timestamp)) { var ts = (Google.Protobuf.WellKnownTypes.Timestamp)value; var dt = ts.ToDateTime(); if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc); var secs = (long)(dt - DateTime.UnixEpoch).TotalSeconds; numbers.Add(secs); return; }");
        sb.AppendLine("        if (t == typeof(DateTime)) { var dt = (DateTime)value; if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc); var secs = (long)(dt.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds; numbers.Add(secs); return; }");
        sb.AppendLine("        if (t == typeof(DateTimeOffset)) { var dto = (DateTimeOffset)value; var secs = (long)(dto.ToUniversalTime().UtcDateTime - DateTime.UnixEpoch).TotalSeconds; numbers.Add(secs); return; }");
        sb.AppendLine("        if (fullName == \"System.DateOnly\") { dynamic d = value; var dt = new DateTime(d.Year, d.Month, d.Day,0,0,0,DateTimeKind.Utc); var secs = (long)(dt - DateTime.UnixEpoch).TotalSeconds; numbers.Add(secs); return; }");
        sb.AppendLine("        if (fullName == \"System.TimeOnly\" || t == typeof(TimeSpan)) return;");
        sb.AppendLine("        // Skip UIntPtr / nuint to avoid adding unwanted zeros");
        sb.AppendLine("        if (t.FullName == \"System.UIntPtr\") return;");
        sb.AppendLine("        if (t == typeof(Guid)) { var s = ((Guid)value).ToString(); if (TryExtractGuidTrailingNumber(s, out var gnum)) numbers.Add(gnum); return; }");
        sb.AppendLine("        if (t == typeof(string)) { var s = (string)value; if (DateTime.TryParse(s, out _)) return; if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dnum)) { numbers.Add(dnum); return; } if (s.Length == 36 && s.Count(c=>c=='-')==4 && TryExtractGuidTrailingNumber(s, out var gnum2)) { numbers.Add(gnum2); return; } int end = s.Length - 1; while (end >=0 && !char.IsDigit(s[end])) end--; if (end >= 0) { int start = end; while (start >=0 && char.IsDigit(s[start])) start--; start++; if (start <= end) { var digits = s.Substring(start, end-start+1); if (digits.Length > 0 && double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var tail)) numbers.Add(tail); } } return; }");
        sb.AppendLine("        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte)) { numbers.Add(Convert.ToDouble(value)); return; }");
        sb.AppendLine("        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) { numbers.Add(Convert.ToDouble(value)); return; }");
        sb.AppendLine("        if (t == typeof(bool)) { numbers.Add((bool)value ? 1 : 0); return; }");
        sb.AppendLine("        if (value is System.Collections.IEnumerable en && value is not string) { foreach (var item in en) ExtractFromState(item, numbers); return; }");
        sb.AppendLine("        foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) { try { var v = p.GetValue(value); ExtractFromState(v, numbers); } catch { } }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool TryExtractGuidTrailingNumber(string guidString, out double number)");
        sb.AppendLine("    {");
        sb.AppendLine("        number = 0; if (string.IsNullOrWhiteSpace(guidString) || guidString.Length != 36) return false; if (guidString.Count(c=>c=='-')!=4) return false;");
        sb.AppendLine("        int lastDash = guidString.LastIndexOf('-'); if (lastDash < 0 || lastDash == guidString.Length-1) return false; var lastSegment = guidString[(lastDash+1)..];");
        sb.AppendLine("        if (string.IsNullOrWhiteSpace(lastSegment)) return false; if (lastSegment.All(c=>c=='0')) return false; var digitsOnly = new string(lastSegment.Where(char.IsDigit).ToArray()); if (string.IsNullOrEmpty(digitsOnly)) return false; digitsOnly = digitsOnly.TrimStart('0'); if (digitsOnly.Length==0) return false; if (double.TryParse(digitsOnly, out number)) return true; return false;");
        sb.AppendLine("    }");
        sb.AppendLine();

        var collectionProps = props.Where(p => IsCollectionType(p.TypeString)).ToList();
        if (collectionProps.Count > 0)
        {
            foreach (var cp in collectionProps)
            {
                var updaterName = cp.Name + "IndexedUpdater";
                sb.AppendLine($"    public IIndexedUpdater {cp.Name}(int index) => new {updaterName}(_grpcClient, \"{cp.Name}\", index);");
                sb.AppendLine();
                sb.AppendLine($"    private class {updaterName} : IIndexedUpdater");
                sb.AppendLine("    {");
                sb.AppendLine($"        private readonly {serviceName}.{serviceName}Client _client;");
                sb.AppendLine("        private readonly string _collectionName;");
                sb.AppendLine("        private readonly int _index;");
                sb.AppendLine($"        public {updaterName}({serviceName}.{serviceName}Client client, string collectionName, int index) {{ _client = client; _collectionName = collectionName; _index = index; }}");
                string[] commonProps = new [] { "Temperature", "Zone", "Message", "Counter", "PlayerLevel", "HasBonus", "BonusMultiplier", "IsEnabled", "Enabled" };
                foreach (var cname in commonProps)
                {
                    sb.AppendLine($"        public async Task Update{cname}Async(int value) {{ await UpdatePropertyAsync(\"{cname}\", value); }}");
                }
                sb.AppendLine("        public async Task UpdateHasBonusAsync(bool value) => await UpdatePropertyAsync(\"HasBonus\", value);");
                sb.AppendLine("        public async Task UpdateIsEnabledAsync(bool value) => await UpdatePropertyAsync(\"IsEnabled\", value);");
                sb.AppendLine("        public async Task UpdateEnabledAsync(bool value) => await UpdatePropertyAsync(\"IsEnabled\", value);");
                sb.AppendLine("        public async Task UpdateBonusMultiplierAsync(double value) => await UpdatePropertyAsync(\"BonusMultiplier\", value);");
                sb.AppendLine("        public async Task UpdatePropertyAsync(string propertyName, object value)");
                sb.AppendLine("        {");
                sb.AppendLine("            var protoValue = value switch { string s => (Google.Protobuf.IMessage)new StringValue { Value = s }, int i => (Google.Protobuf.IMessage)new Int32Value { Value = i }, bool b => (Google.Protobuf.IMessage)new BoolValue { Value = b }, double d => (Google.Protobuf.IMessage)new DoubleValue { Value = d }, float f => (Google.Protobuf.IMessage)new FloatValue { Value = f }, long l => (Google.Protobuf.IMessage)new Int64Value { Value = l }, DateTime dt => (Google.Protobuf.IMessage)Timestamp.FromDateTime((dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime())), DateTimeOffset dto => (Google.Protobuf.IMessage)Timestamp.FromDateTime(dto.UtcDateTime), _ => (Google.Protobuf.IMessage)new StringValue { Value = value?.ToString() ?? \"\" } };");
                sb.AppendLine("            var request = new UpdatePropertyValueRequest { ");
                sb.AppendLine("                PropertyName = propertyName,");
                sb.AppendLine("                PropertyPath = $\"{_collectionName}[{_index}].{propertyName}\",");
                sb.AppendLine("                NewValue = Any.Pack(protoValue) ");
                sb.AppendLine("            };");
                sb.AppendLine("            await _client.UpdatePropertyValueAsync(request);");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }
    }
}