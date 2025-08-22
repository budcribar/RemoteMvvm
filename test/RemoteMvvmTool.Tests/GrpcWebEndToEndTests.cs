using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;

namespace RemoteMvvmTool.Tests;

public class GrpcWebEndToEndTests
{
    static List<string> LoadDefaultRefs()
    {
        var list = new List<string>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(p);
        }
        return list;
    }

    static void RunCmd(string file, string args, string workDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            throw new Exception($"{file} {args} failed with {p.ExitCode}: {stdout} {stderr}");
        }
    }

    static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static void RunProtoc(string protoDir, string protoFile, string outDir)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolsRoot = Path.Combine(home, ".nuget", "packages", "grpc.tools");
        var versionDir = Directory.GetDirectories(toolsRoot).OrderBy(p => p).Last();
        string osPart = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "windows" : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "macosx" : "linux";
        string archPart = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => "x64"
        };
        bool isWin = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var protoc = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "protoc.exe" : "protoc");
        var plugin = Path.Combine(versionDir, "tools", $"{osPart}_{archPart}", isWin ? "grpc_csharp_plugin.exe" : "grpc_csharp_plugin");
        var includeDir = Path.Combine(versionDir, "build", "native", "include");
        var psi = new ProcessStartInfo
        {
            FileName = protoc,
            Arguments = $"--csharp_out \"{outDir}\" --grpc_out \"{outDir}\" --plugin=protoc-gen-grpc=\"{plugin}\" -I\"{protoDir}\" -I\"{includeDir}\" \"{protoFile}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var msg = proc.StandardError.ReadToEnd();
            throw new Exception($"protoc failed: {msg}");
        }
    }

    static void CreateTsGrpcWebClient(string dir, string vmName, string serviceName, string protoContent)
    {
        var pkgMatch = Regex.Match(protoContent, @"package\s+([^\s;]+);");
        var pkg = pkgMatch.Success ? pkgMatch.Groups[1].Value : "generated_protos";

        var stateMatch = Regex.Match(protoContent, @$"message\s+{vmName}State\s*\{{(?<body>[\s\S]*?)\}}", RegexOptions.Multiline);
        if (!stateMatch.Success) throw new InvalidOperationException("State message not found");
        var stateBody = stateMatch.Groups["body"].Value;
        var repMatch = Regex.Match(stateBody, @"repeated\s+(\w+)State\s+([a-zA-Z0-9_]+)\s*=\s*(\d+);");
        if (!repMatch.Success) throw new InvalidOperationException("Repeated field not found");
        var elemType = repMatch.Groups[1].Value;
        var snakeName = repMatch.Groups[2].Value;
        var fieldNum = int.Parse(repMatch.Groups[3].Value);

        var elemMatch = Regex.Match(protoContent, @$"message\s+{elemType}State\s*\{{(?<body>[\s\S]*?)\}}", RegexOptions.Multiline);
        var elemBody = elemMatch.Groups["body"].Value;
        var fieldMatches = Regex.Matches(elemBody, @"(int32|int64|uint32|uint64|bool)\s+([a-zA-Z0-9_]+)\s*=\s*(\d+);");

        var decElem = new System.Text.StringBuilder();
        decElem.Append($"function d{elemType}(buf:Uint8Array){{let i=0;const r:any={{}};while(i<buf.length){{const tag=buf[i++];");
        bool first = true;
        foreach (Match f in fieldMatches)
        {
            int fn = int.Parse(f.Groups[3].Value);
            int tag = fn << 3;
            var name = f.Groups[2].Value;
            decElem.Append(first ? $"if(tag=={tag}){{[r.{name},i]=dv(buf,i);}}" : $"else if(tag=={tag}){{[r.{name},i]=dv(buf,i);}}" );
            first = false;
        }
        decElem.Append("else break;} return r;}");

        string pascal = string.Concat(snakeName.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
        int topTag = (fieldNum << 3) | 2;
        var ds = $"function ds(buf:Uint8Array){{let i=0;const list:any[]=[];while(i<buf.length){{const tag=buf[i++];if(tag=={topTag}){{let l;i=(l=dv(buf,i))[1];const len=l[0];const sub=buf.slice(i,i+len);i+=len;list.push(d{elemType}(sub));}}else break;}}return {{ get{pascal}List: () => list }};}}";

        var nodeModules = Path.Combine(dir, "node_modules");
        Directory.CreateDirectory(Path.Combine(nodeModules, "grpc-web"));
        File.WriteAllText(Path.Combine(nodeModules, "grpc-web", "index.d.ts"),
            "export interface ClientReadableStream<T> { on(type:string, handler:(...a:any[])=>void): void; cancel(): void; }");
        File.WriteAllText(Path.Combine(nodeModules, "grpc-web", "index.js"),
            "exports.ClientReadableStream = class { on(){} cancel(){} };");

        var gp = Path.Combine(nodeModules, "google-protobuf", "google", "protobuf");
        Directory.CreateDirectory(gp);
        File.WriteAllText(Path.Combine(gp, "empty_pb.d.ts"), "export class Empty {}");
        File.WriteAllText(Path.Combine(gp, "empty_pb.js"), "exports.Empty = class {};");
        File.WriteAllText(Path.Combine(gp, "any_pb.d.ts"),
            "export class Any { pack(a:Uint8Array,b:string):void; unpack(fn:any,name:string):any; }");
        File.WriteAllText(Path.Combine(gp, "any_pb.js"),
            "exports.Any = class { pack(){} unpack(){ return null; } };");
        File.WriteAllText(Path.Combine(gp, "wrappers_pb.d.ts"),
            "export class StringValue { setValue(v:string):void; getValue():string; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):StringValue; }\n" +
            "export class Int32Value { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):Int32Value; }\n" +
            "export class Int64Value { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):Int64Value; }\n" +
            "export class BoolValue { setValue(v:boolean):void; getValue():boolean; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):BoolValue; }\n" +
            "export class DoubleValue { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):DoubleValue; }");
        File.WriteAllText(Path.Combine(gp, "wrappers_pb.js"),
            "class W{ constructor(){this.value=0;} setValue(v){this.value=v;} getValue(){return this.value;} serializeBinary(){return new Uint8Array();} static deserializeBinary(){return new W();} } exports.StringValue=W; exports.Int32Value=W; exports.Int64Value=W; exports.BoolValue=W; exports.DoubleValue=W;");
        File.WriteAllText(Path.Combine(gp, "timestamp_pb.d.ts"),
            "export class Timestamp { static deserializeBinary(b:Uint8Array):Timestamp; static fromDate(d:Date):Timestamp; toDate():Date; serializeBinary():Uint8Array; }");
        File.WriteAllText(Path.Combine(gp, "timestamp_pb.js"),
            "class T{ constructor(d=new Date(0)){this.d=d;} static deserializeBinary(){return new T();} static fromDate(d){return new T(d);} toDate(){return this.d;} serializeBinary(){return new Uint8Array();} } exports.Timestamp=T;");

        var gen = Path.Combine(dir, "generated");
        Directory.CreateDirectory(gen);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("import * as grpcWeb from 'grpc-web';");
        sb.AppendLine($"import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, StateChangedRequest, CancelTestRequest }} from './{serviceName}_pb.js';");
        sb.AppendLine("function dv(buf:Uint8Array,o:number):[number,number]{let v=0,s=0,i=o;for(;;){const b=buf[i++];v|=(b&0x7f)<<s;if((b&0x80)==0)break;s+=7;}return [v,i];}");
        sb.AppendLine(decElem.ToString());
        sb.AppendLine(ds);
        sb.AppendLine($"export class {serviceName}Client {{");
        sb.AppendLine("  constructor(private hostname: string) {}");
        sb.AppendLine("  async getState(_req:any): Promise<any> {");
        sb.AppendLine("    const body = new Uint8Array([0,0,0,0,0]);");
        sb.AppendLine($"    const res = await fetch(this.hostname + '/{pkg}.{serviceName}/GetState', {{ method:'POST', headers:{{'Content-Type':'application/grpc-web+proto'}}, body }});");
        sb.AppendLine("    const buf = new Uint8Array(await res.arrayBuffer());");
        sb.AppendLine("    const len = (buf[1]<<24)|(buf[2]<<16)|(buf[3]<<8)|buf[4];");
        sb.AppendLine("    const msg = buf.slice(5,5+len);");
        sb.AppendLine("    return ds(msg);");
        sb.AppendLine("  }");
        sb.AppendLine("  updatePropertyValue(_req:UpdatePropertyValueRequest): Promise<void> { return Promise.resolve(); }");
        sb.AppendLine("  subscribeToPropertyChanges(_req:SubscribeRequest): grpcWeb.ClientReadableStream<PropertyChangeNotification> { return { on:()=>{}, cancel:()=>{} } as any; }");
        sb.AppendLine("  ping(_req:any): Promise<ConnectionStatusResponse> { return Promise.resolve(new ConnectionStatusResponse()); }");
        sb.AppendLine("  stateChanged(_req:StateChangedRequest): Promise<void> { return Promise.resolve(); }");
        sb.AppendLine("  cancelTest(_req:CancelTestRequest): Promise<void> { return Promise.resolve(); }");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(gen, serviceName + "ServiceClientPb.ts"), sb.ToString());
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.js"),
            $"exports.{vmName}State = class {{}};" +
            "exports.UpdatePropertyValueRequest = class { constructor(){ this.p=''; this.v=undefined; } setPropertyName(v){ this.p=v; } getPropertyName(){ return this.p; } setNewValue(v){ this.v=v; } getNewValue(){ return this.v; } };" +
            "exports.SubscribeRequest = class { setClientId(){} };" +
            "exports.PropertyChangeNotification = class { getPropertyName(){return ''} getNewValue(){return null} };" +
            "exports.ConnectionStatusResponse = class { getStatus(){return 0} };" +
            "exports.ConnectionStatus = { CONNECTED:0, DISCONNECTED:1 };" +
            "exports.StateChangedRequest = class { setState(){} };" +
            "exports.CancelTestRequest = class {};");
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.d.ts"),
            $"export class {vmName}State {{}}\n" +
            "export class UpdatePropertyValueRequest { setPropertyName(v:string):void; getPropertyName():string; setNewValue(v:any):void; getNewValue():any; }\n" +
            "export class SubscribeRequest { setClientId(v:string):void; }\n" +
            "export class PropertyChangeNotification { getPropertyName():string; getNewValue():any; }\n" +
            "export class ConnectionStatusResponse { getStatus():number; }\n" +
            "export enum ConnectionStatus { CONNECTED=0, DISCONNECTED=1 }\n" +
            "export class StateChangedRequest { setState(v:any):void; }\n" +
            "export class CancelTestRequest {}\n");
    }
    static string RunPs(string scriptPath, string args, string workDir)
    {
        string file;
        string fileArgs;
        if (OperatingSystem.IsWindows())
        {
            file = "powershell";
            fileArgs = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {args}";
        }
        else
        {
            file = "tsc";
            fileArgs = args;
        }

        var psi = new ProcessStartInfo(file, fileArgs)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode > 0)
        {
            return $"STDOUT:\n{stdout}\nSTDERR:\n{stderr}";
        }


        if (p.ExitCode != 0)
        {
            return $"PowerShell script failed with {p.ExitCode}:\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}";
        }
        return "";
    }

    [Fact]
    public async Task TypeScript_Client_Can_Retrieve_Collection_From_Server()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var vmCode = @"public class ObservablePropertyAttribute : System.Attribute {}
public class RelayCommandAttribute : System.Attribute {}
namespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }
namespace Generated.ViewModels {
  public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } public int Temperature { get; set; } }
  public partial class TestViewModel : ObservableObject {
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel> zoneList;
  }
}
public class ObservableObject {}";
        var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, vmCode);
        var vmStub = @"namespace Generated.ViewModels { public partial class TestViewModel : ObservableObject { public TestViewModel() {} public System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel> ZoneList { get; set; } = new System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel>(); } }";
        File.WriteAllText(Path.Combine(tempDir, "TestViewModelStub.cs"), vmStub);
        var clientStub = @"namespace Generated.Clients { public class TestViewModelRemoteClient { public TestViewModelRemoteClient(object c) {} public System.Threading.Tasks.Task InitializeRemoteAsync() => System.Threading.Tasks.Task.CompletedTask; } }";
        File.WriteAllText(Path.Combine(tempDir, "TestViewModelRemoteClient.cs"), clientStub);
        var refs = LoadDefaultRefs();
        var (vmSymbol, name, props, cmds, compilation) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");

        var proto = ProtoGenerator.Generate("Test.Protos", name + "Service", name, props, cmds, compilation);
        var protoDir = Path.Combine(tempDir, "protos");
        Directory.CreateDirectory(protoDir);
        var protoFile = Path.Combine(protoDir, name + "Service.proto");
        File.WriteAllText(protoFile, proto);
        var grpcOut = Path.Combine(tempDir, "grpc");
        Directory.CreateDirectory(grpcOut);
        RunProtoc(protoDir, protoFile, grpcOut);

        var serverCode = ServerGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds, "Generated.ViewModels", "console");
        File.WriteAllText(Path.Combine(tempDir, name + "GrpcServiceImpl.cs"), serverCode);
        var rootTypes = props.Select(p => p.FullTypeSymbol!);
        var conv = ConversionGenerator.Generate("Test.Protos", "Generated.ViewModels", rootTypes, compilation);
        File.WriteAllText(Path.Combine(tempDir, "ProtoStateConverters.cs"), conv);
        var partial = ViewModelPartialGenerator.Generate(name, "Test.Protos", name + "Service", "Generated.ViewModels", "Generated.Clients", "ObservableObject", "console", true);
        File.WriteAllText(Path.Combine(tempDir, name + ".Remote.g.cs"), partial);
        var options = @"namespace PeakSWC.Mvvm.Remote { public class ServerOptions { public int Port { get; set; } public bool UseHttps { get; set; } = false; } public class ClientOptions { public string Address { get; set; } = ""http://localhost""; } }";
        File.WriteAllText(Path.Combine(tempDir, "GrpcRemoteOptions.cs"), options);

        var sourceFiles = Directory.GetFiles(tempDir, "*.cs").Concat(Directory.GetFiles(grpcOut, "*.cs"));
        var trees = sourceFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));
        var references = refs.Select(r => MetadataReference.CreateFromFile(r));
        var compilation2 = CSharpCompilation.Create("ServerAsm", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var dllPath = Path.Combine(tempDir, "server.dll");
        var emitResult = compilation2.Emit(dllPath);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));

        var asm = Assembly.LoadFile(dllPath);
        var vmType = asm.GetType("Generated.ViewModels.TestViewModel")!;
        var serverOptsType = asm.GetType("PeakSWC.Mvvm.Remote.ServerOptions")!;
        var serverOpts = Activator.CreateInstance(serverOptsType)!;
        int port = GetFreePort();
        serverOptsType.GetProperty("Port")!.SetValue(serverOpts, port);
        var vm = Activator.CreateInstance(vmType, new object[] { serverOpts })!;
        var zoneListProp = vmType.GetProperty("ZoneList")!;
        var list = (System.Collections.IList)zoneListProp.GetValue(vm)!;
        var tzType = asm.GetType("Generated.ViewModels.ThermalZoneComponentViewModel")!;
        var zoneEnum = asm.GetType("HP.Telemetry.Zone")!;
        var z0 = Activator.CreateInstance(tzType)!;
        tzType.GetProperty("Zone")!.SetValue(z0, Enum.Parse(zoneEnum, "CPUZ_0"));
        tzType.GetProperty("Temperature")!.SetValue(z0, 42);
        list.Add(z0);
        var z1 = Activator.CreateInstance(tzType)!;
        tzType.GetProperty("Zone")!.SetValue(z1, Enum.Parse(zoneEnum, "CPUZ_1"));
        tzType.GetProperty("Temperature")!.SetValue(z1, 43);
        list.Add(z1);

        await Task.Delay(500);

        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        File.WriteAllText(Path.Combine(tempDir, name + "RemoteClient.ts"), ts);
        CreateTsGrpcWebClient(tempDir, name, name + "Service", proto);
        var testTs = $@"declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient }} from './generated/{name}ServiceServiceClientPb';
(async () => {{
  const client = new {name}RemoteClient(new {name}ServiceClient('http://localhost:{port}'));
  await client.initializeRemote();
  if (client.zoneList.length < 2 || client.zoneList[0].temperature !== 42 || client.zoneList[1].temperature !== 43) throw new Error('Bad data');
  client.dispose();
}})().catch(e => {{ console.error(e); process.exit(1); }});";
        File.WriteAllText(Path.Combine(tempDir, "test.ts"), testTs);

        var tsconfig = @"{
  ""compilerOptions"": {
    ""target"": ""es2018"",
    ""module"": ""commonjs"",
    ""strict"": false,
    ""esModuleInterop"": true,
    ""lib"": [""es2018"", ""dom""],
    ""outDir"": ""dist"",
    ""allowJs"": true
  },
  ""include"": [""**/*.ts"", ""**/*.js""]
}";
        File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);

        if (OperatingSystem.IsWindows())
            RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
        else
            RunCmd("tsc", "--project tsconfig.json", tempDir);
        
        RunCmd("node", "test.js", Path.Combine(tempDir, "dist"));

        (vm as IDisposable)?.Dispose();
    }
}
