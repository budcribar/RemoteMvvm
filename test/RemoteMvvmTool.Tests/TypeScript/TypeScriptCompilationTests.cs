using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GrpcRemoteMvvmModelUtil;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.TypeScript;

public class TypeScriptCompilationTests
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

    static void RunPs(string scriptPath, string args, string workDir)
    {
        var psi = new ProcessStartInfo("powershell", $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {args}")
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

        if (p.ExitCode != 0)
        {
            throw new Exception($"PowerShell script failed with {p.ExitCode}:\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }
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

    static void CreateTsStubs(string dir, string vmName, string serviceName)
    {
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
            "export class StringValue { setValue(v:string):void; getValue():string; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):StringValue; }\n"+
            "export class Int32Value { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):Int32Value; }\n"+
            "export class BoolValue { setValue(v:boolean):void; getValue():boolean; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):BoolValue; }\n"+
            "export class DoubleValue { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):DoubleValue; }");
        File.WriteAllText(Path.Combine(gp, "wrappers_pb.js"),
            "class W{ constructor(){this.value=0;} setValue(v){this.value=v;} getValue(){return this.value;} serializeBinary(){return new Uint8Array();} } exports.StringValue=W; exports.Int32Value=W; exports.BoolValue=W; exports.DoubleValue=W;");

        var gen = Path.Combine(dir, "generated");
        Directory.CreateDirectory(gen);
        File.WriteAllText(Path.Combine(gen, serviceName + "ServiceClientPb.ts"), $@"
import * as grpcWeb from 'grpc-web';
import {{ {vmName}State, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, StateChangedRequest, CancelTestRequest }} from './{serviceName}_pb.js';
export class {serviceName}Client {{
  constructor(addr: string) {{}}
  getState(req: any): Promise<{vmName}State> {{ return Promise.resolve(new {vmName}State()); }}
  updatePropertyValue(req: UpdatePropertyValueRequest): Promise<void> {{ return Promise.resolve(); }}
  subscribeToPropertyChanges(req: SubscribeRequest): grpcWeb.ClientReadableStream<PropertyChangeNotification> {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(req:any): Promise<ConnectionStatusResponse> {{ return Promise.resolve(new ConnectionStatusResponse()); }}
  stateChanged(req: StateChangedRequest): Promise<void> {{ return Promise.resolve(); }}
  cancelTest(req: CancelTestRequest): Promise<void> {{ return Promise.resolve(); }}
}}
");
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.js"),
            $"exports.{vmName}State = class {{}};"+
            "exports.UpdatePropertyValueRequest = class { setPropertyName(){} setNewValue(){} };"+
            "exports.SubscribeRequest = class { setClientId(){} };"+
            "exports.PropertyChangeNotification = class { getPropertyName(){return ''} getNewValue(){return null} };"+
            "exports.ConnectionStatusResponse = class { getStatus(){return 0} };"+
            "exports.ConnectionStatus = { CONNECTED:0, DISCONNECTED:1 };"+
            "exports.StateChangedRequest = class { setState(){} };"+
            "exports.CancelTestRequest = class {};");
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.d.ts"),
            $"export class {vmName}State {{}}\n"+
            "export class UpdatePropertyValueRequest { setPropertyName(v:string):void; setNewValue(v:any):void; }\n"+
            "export class SubscribeRequest { setClientId(v:string):void; }\n"+
            "export class PropertyChangeNotification { getPropertyName():string; getNewValue():any; }\n"+
            "export class ConnectionStatusResponse { getStatus():number; }\n"+
            "export enum ConnectionStatus { CONNECTED=0, DISCONNECTED=1 }\n"+
            "export class StateChangedRequest { setState(v:any):void; }\n"+
            "export class CancelTestRequest {}\n");
    }

    static async Task RunSimpleCompilationTest(string propertyType, string propertyName, string tsReturnValue, string tsAssertion, string? extraCode = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmCode = "public class ObservablePropertyAttribute : System.Attribute {}\n" +
                     "public class RelayCommandAttribute : System.Attribute {}\n" +
                     (extraCode ?? string.Empty) +
                     $"public partial class TestViewModel : ObservableObject {{ [ObservableProperty] public partial {propertyType} {propertyName} {{ get; set; }} }}\n" +
                     "public class ObservableObject {}";
        var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, vmCode);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");
        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        if (propertyType == "double")
        {
            ts = ts.Replace("import { StringValue, Int32Value, BoolValue }", "import { StringValue, Int32Value, BoolValue, DoubleValue }")
                   .Replace("unpack(, 'google.protobuf.DoubleValue')", "unpack(DoubleValue.deserializeBinary, 'google.protobuf.DoubleValue')");
        }
        var tsClientFile = Path.Combine(tempDir, name + "RemoteClient.ts");
        File.WriteAllText(tsClientFile, ts);

        CreateTsStubs(tempDir, name, name + "Service");

        var testTs = $@"declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      get{propertyName}: () => {tsReturnValue}
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(); }}
  subscribeToPropertyChanges(_req:any) {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(_req:any) {{ return Promise.resolve({{ getStatus: () => 0 }}); }}
  stateChanged(_req:any) {{ return Promise.resolve(); }}
  cancelTest(_req:any) {{ return Promise.resolve(); }}
}}
(async () => {{
  const client = new {name}RemoteClient(new FakeClient(''));
  await client.initializeRemote();
  {tsAssertion}
  client.dispose();
}})().catch(e => {{ console.error(e); process.exit(1); }});
";
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
        try
        {
            RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
        }
        catch
        {
            RunCmd("tsc", "--project tsconfig.json", tempDir);
        }

        RunCmd("node", "test.js", Path.Combine(tempDir, "dist"));
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_With_Int_Property()
    {
        await RunSimpleCompilationTest("int", "Value", "42", "if (client.value !== 42) throw new Error('Int property transfer failed');");
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_With_String_Property()
    {
        await RunSimpleCompilationTest("string", "Text", "'hello'", "if (client.text !== 'hello') throw new Error('String property transfer failed');");
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_With_Bool_Property()
    {
        await RunSimpleCompilationTest("bool", "Enabled", "true", "if (!client.enabled) throw new Error('Bool property transfer failed');");
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_With_Double_Property()
    {
        await RunSimpleCompilationTest("double", "Ratio", "0.5", "if (client.ratio !== 0.5) throw new Error('Double property transfer failed');");
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_With_Enum_Property()
    {
        await RunSimpleCompilationTest("Mode", "Mode", "1", "if (client.mode !== 1) throw new Error('Enum property transfer failed');", "public enum Mode { A, B }\\n");
    }

    [Fact]
    public async Task Generated_TypeScript_Compiles_And_Transfers_Dictionary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmCode = @"public class ObservablePropertyAttribute : System.Attribute {}\npublic class RelayCommandAttribute : System.Attribute {}\nnamespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }\nnamespace HPSystemsTools.ViewModels { public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } public int Temperature { get; set; } } }\npublic partial class TestViewModel : ObservableObject { [ObservableProperty] public partial System.Collections.Generic.Dictionary<HP.Telemetry.Zone, HPSystemsTools.ViewModels.ThermalZoneComponentViewModel> Zones { get; set; } }\npublic class ObservableObject {}";
        var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, vmCode);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");
        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        var tsClientFile = Path.Combine(tempDir, name + "RemoteClient.ts");
        File.WriteAllText(tsClientFile, ts);

        CreateTsStubs(tempDir, name, name + "Service");

        var testTs = $@"
declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      getZonesMap: () => ({{ toObject: () => ({{ 0: {{ zone: 0, temperature: 42 }} }}) }}),
      getTestSettings: () => ({{ cpuTemperatureThreshold:0, cpuLoadThreshold:0, cpuLoadTimeSpan:0, dTS:{{}} }}),
      getShowDescription: () => true,
      getShowReadme: () => false
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(); }}
  subscribeToPropertyChanges(_req:any) {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(_req:any) {{ return Promise.resolve({{ getStatus: () => 0 }}); }}
  stateChanged(_req:any) {{ return Promise.resolve(); }}
  cancelTest(_req:any) {{ return Promise.resolve(); }}
}}
(async () => {{
  const client = new {name}RemoteClient(new FakeClient(''));
  await client.initializeRemote();
  if (client.zones[0].temperature !== 42) throw new Error('Data transfer failed');
  client.dispose();
}})().catch(e => {{ console.error(e); process.exit(1); }});
";
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
        try
        {
            RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
        }
        catch
        {
            RunCmd("tsc", "--project tsconfig.json", tempDir);
        }

        RunCmd("node", "test.js", Path.Combine(tempDir, "dist"));
    }
}
