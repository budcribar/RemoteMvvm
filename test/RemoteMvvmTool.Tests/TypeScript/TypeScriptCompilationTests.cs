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

    static async Task RunCmdAsync(string file, string args, string workDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
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

        // Add NodeJS type definitions
        Directory.CreateDirectory(Path.Combine(nodeModules, "@types", "node"));
        File.WriteAllText(Path.Combine(nodeModules, "@types", "node", "index.d.ts"),
            "declare namespace NodeJS { interface Timeout {} }");
        File.WriteAllText(Path.Combine(nodeModules, "@types", "node", "package.json"),
            """{"name": "@types/node", "version": "1.0.0"}""");

        var gp = Path.Combine(nodeModules, "google-protobuf", "google", "protobuf");
        Directory.CreateDirectory(gp);
        File.WriteAllText(Path.Combine(gp, "empty_pb.d.ts"), "export class Empty { serializeBinary(): Uint8Array; }");
        File.WriteAllText(Path.Combine(gp, "empty_pb.js"), "exports.Empty = class { serializeBinary() { return new Uint8Array(); } };");
        File.WriteAllText(Path.Combine(gp, "any_pb.d.ts"),
            "export class Any { pack(a:Uint8Array,b:string):void; unpack(fn:any,name:string):any; getTypeUrl():string; static pack(data:any):Any; serializeBinary(): Uint8Array; }");
        File.WriteAllText(Path.Combine(gp, "any_pb.js"),
            "class AnyImpl { pack(){} unpack(){ return null; } getTypeUrl(){ return 'test.type.url'; } static pack(data) { return new AnyImpl(); } serializeBinary() { return new Uint8Array(); } } exports.Any = AnyImpl;");
        File.WriteAllText(Path.Combine(gp, "wrappers_pb.d.ts"),
            "export class StringValue { setValue(v:string):void; getValue():string; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):StringValue; }\n" +
            "export class Int32Value { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):Int32Value; }\n" +
            "export class Int64Value { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):Int64Value; }\n" +
            "export class BoolValue { setValue(v:boolean):void; getValue():boolean; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):BoolValue; }\n" +
            "export class DoubleValue { setValue(v:number):void; getValue():number; serializeBinary():Uint8Array; static deserializeBinary(b:Uint8Array):DoubleValue; }");
        File.WriteAllText(Path.Combine(gp, "wrappers_pb.js"),
            "class W{ constructor(){this.value=0;} setValue(v){this.value=v;} getValue(){return this.value;} serializeBinary(){return new Uint8Array();} } exports.StringValue=W; exports.Int32Value=W; exports.Int64Value=W; exports.BoolValue=W; exports.DoubleValue=W;");

        File.WriteAllText(Path.Combine(gp, "timestamp_pb.d.ts"),
            "export class Timestamp { static deserializeBinary(b:Uint8Array):Timestamp; static fromDate(d:Date):Timestamp; toDate():Date; serializeBinary():Uint8Array; }");
        File.WriteAllText(Path.Combine(gp, "timestamp_pb.js"),
            "class T{ constructor(d=new Date(0)){this.d=d;} static deserializeBinary(){return new T();} static fromDate(d){return new T(d);} toDate(){return this.d;} serializeBinary(){return new Uint8Array();} } exports.Timestamp=T;");

        var gen = Path.Combine(dir, "generated");
        Directory.CreateDirectory(gen);
        
        var clientFileName = serviceName + "ServiceClientPb.ts";
            
        File.WriteAllText(Path.Combine(gen, clientFileName), $@"
import * as grpcWeb from 'grpc-web';
import {{ {vmName}State, UpdatePropertyValueRequest, UpdatePropertyValueResponse, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, StateChangedRequest, CancelTestRequest }} from './{serviceName}_pb.js';
export class {serviceName}Client {{
  constructor(addr: string) {{}}
  getState(req: any): Promise<{vmName}State> {{ return Promise.resolve(new {vmName}State()); }}
  updatePropertyValue(req: UpdatePropertyValueRequest): Promise<UpdatePropertyValueResponse> {{ return Promise.resolve(new UpdatePropertyValueResponse()); }}
  subscribeToPropertyChanges(req: SubscribeRequest): grpcWeb.ClientReadableStream<PropertyChangeNotification> {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(req:any): Promise<ConnectionStatusResponse> {{ return Promise.resolve(new ConnectionStatusResponse()); }}
  stateChanged(req: StateChangedRequest): Promise<void> {{ return Promise.resolve(); }}
  cancelTest(req: CancelTestRequest): Promise<void> {{ return Promise.resolve(); }}
}}
export {{ UpdatePropertyValueResponse }};
");
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.js"),
            $"exports.{vmName}State = class {{}};" +
            "exports.UpdatePropertyValueRequest = class { constructor(){ this.p=''; this.v=undefined; this.path=''; this.key=''; this.idx=-1; this.op=''; } setPropertyName(v){ this.p=v; } getPropertyName(){ return this.p; } setNewValue(v){ this.v=v; } getNewValue(){ return this.v; } setPropertyPath(v){ this.path=v; } getPropertyPath(){ return this.path; } setCollectionKey(v){ this.key=v; } getCollectionKey(){ return this.key; } setArrayIndex(v){ this.idx=v; } getArrayIndex(){ return this.idx; } setOperationType(v){ this.op=v; } getOperationType(){ return this.op; } };" +
            "exports.UpdatePropertyValueResponse = class { constructor(){ this.success=true; this.error=''; } getSuccess(){ return this.success; } setSuccess(v){ this.success=v; } getErrorMessage(){ return this.error; } setErrorMessage(v){ this.error=v; } };" +
            "exports.SubscribeRequest = class { setClientId(){} };" +
            "exports.PropertyChangeNotification = class { getPropertyName(){return ''} getNewValue(){return null} getPropertyPath(){return ''} };" +
            "exports.ConnectionStatusResponse = class { getStatus(){return 0} };" +
            "exports.ConnectionStatus = { CONNECTED:0, DISCONNECTED:1 };" +
            "exports.StateChangedRequest = class { setState(){} };" +
            "exports.CancelTestRequest = class {};");
        File.WriteAllText(Path.Combine(gen, serviceName + "_pb.d.ts"),
            $"export class {vmName}State {{}}\n" +
            "export class UpdatePropertyValueRequest { setPropertyName(v:string):void; getPropertyName():string; setNewValue(v:any):void; getNewValue():any; setPropertyPath(v:string):void; getPropertyPath():string; setCollectionKey(v:string):void; getCollectionKey():string; setArrayIndex(v:number):void; getArrayIndex():number; setOperationType(v:string):void; getOperationType():string; }\n" +
            "export class UpdatePropertyValueResponse { getSuccess():boolean; setSuccess(v:boolean):void; getErrorMessage():string; setErrorMessage(v:string):void; }\n" +
            "export class SubscribeRequest { setClientId(v:string):void; }\n" +
            "export class PropertyChangeNotification { getPropertyName():string; getNewValue():any; getPropertyPath():string; }\n" +
            "export class ConnectionStatusResponse { getStatus():number; }\n" +
            "export enum ConnectionStatus { CONNECTED=0, DISCONNECTED=1 }\n" +
            "export class StateChangedRequest { setState(v:any):void; }\n" +
            "export class CancelTestRequest {}\n");
    }

    static async Task RunSimpleCompilationTest(string propertyType, string propertyName, string tsReturnValue, string tsAssertion, string? extraCode = null, string? extraImports = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmCode = $@"
public class ObservablePropertyAttribute : System.Attribute {{}}
public class RelayCommandAttribute : System.Attribute {{}}
{extraCode ?? string.Empty}
public partial class TestViewModel : ObservableObject {{
    [ObservableProperty] public partial {propertyType} {propertyName} {{ get; set; }}
}}
public class ObservableObject {{}}
";
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
import {{ {name}ServiceClient, UpdatePropertyValueResponse }} from './generated/{name}ServiceServiceClientPb';
{extraImports ?? string.Empty}
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      get{propertyName}: () => {tsReturnValue}
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(new UpdatePropertyValueResponse()); }}
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
    ""allowJs"": true,
    ""types"": [""node""]
  },
  ""include"": [""**/*.ts"", ""**/*.js""]
}";
        File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);
            
        var result = RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
        if (result.StartsWith("Powershell"))
            await RunCmdAsync("tsc", "--project tsconfig.json", tempDir);

        if (result.Length > 0) Assert.Fail(result);

        await RunCmdAsync("node", "test.js", Path.Combine(tempDir, "dist"));
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
public async Task Generated_TypeScript_Compiles_With_DateTime_Property()
{
    await RunSimpleCompilationTest("System.DateTime", "When", "Timestamp.fromDate(new Date('2020-01-01T00:00:00Z'))", "if (client.when.toISOString() !== '2020-01-01T00:00:00.000Z') throw new Error('Date property transfer failed');", null, "import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';\n");
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
import {{ {name}ServiceClient, UpdatePropertyValueResponse }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      getZonesMap: () => ({{ toObject: () => ({{ 0: {{ zone: 0, temperature: 42 }} }}) }}),
      getTestSettings: () => ({{ cpuTemperatureThreshold:0, cpuLoadThreshold:0, cpuLoadTimeSpan:0, dTS:{{}} }}),
      getShowDescription: () => true,
      getShowReadme: () => false
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(new UpdatePropertyValueResponse()); }}
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
    ""allowJs"": true,
    ""types"": [""node""]
  },
  ""include"": [""**/*.ts"", ""**/*.js""]
}";
    File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);

    var result = RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
    if (result.StartsWith("Powershell"))
        await RunCmdAsync("tsc", "--project tsconfig.json", tempDir);

    if (result.Length > 0) Assert.Fail(result);

    if (OperatingSystem.IsWindows())
        await RunCmdAsync("node", "test.js", Path.Combine(tempDir, "dist"));
}

[Fact]
public async Task Generated_TypeScript_Compiles_And_Transfers_ThermalZoneCollection()
{
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    var vmCode = @"public class ObservablePropertyAttribute : System.Attribute {}\npublic class RelayCommandAttribute : System.Attribute {}\nnamespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }\nnamespace HPSystemsTools.ViewModels { public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } public int Temperature { get; set; } } }\npublic partial class TestViewModel : ObservableObject { [ObservableProperty] public partial System.Collections.ObjectModel.ObservableCollection<HPSystemsTools.ViewModels.ThermalZoneComponentViewModel> ZoneList { get; set; } = new System.Collections.ObjectModel.ObservableCollection<HPSystemsTools.ViewModels.ThermalZoneComponentViewModel>(); }\npublic class ObservableObject {}";
    var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
    File.WriteAllText(vmFile, vmCode);
    var refs = LoadDefaultRefs();
    var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");
    var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
    var tsClientFile = Path.Combine(tempDir, name + "RemoteClient.ts");
    File.WriteAllText(tsClientFile, ts);

    CreateTsStubs(tempDir, name, name + "Service");

    var testTs = $@"declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient, UpdatePropertyValueResponse }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      getZoneListList: () => [{{ zone: 0, temperature: 42 }}, {{ zone: 1, temperature: 43 }}]
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(new UpdatePropertyValueResponse()); }}
  subscribeToPropertyChanges(_req:any) {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(_req:any) {{ return Promise.resolve({{ getStatus: () => 0 }}); }}
  stateChanged(_req:any) {{ return Promise.resolve(); }}
  cancelTest(_req:any) {{ return Promise.resolve(); }}
}}
(async () => {{
  const client = new {name}RemoteClient(new FakeClient(''));
  await client.initializeRemote();
  if (client.zoneList.length !== 2 || client.zoneList[0].temperature !== 42 || client.zoneList[1].temperature !== 43) throw new Error('Collection transfer failed');
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
    ""allowJs"": true,
    ""types"": [""node""]
  },
  ""include"": [""**/*.ts"", ""**/*.js""]
}";
        File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);

    var result = RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
    if (result.StartsWith("Powershell"))
        await RunCmdAsync("tsc", "--project tsconfig.json", tempDir);

    if (result.Length > 0) Assert.Fail(result);

    if (OperatingSystem.IsWindows())
        await RunCmdAsync("node", "test.js", Path.Combine(tempDir, "dist"));
}

[Fact]
public async Task Generated_TypeScript_Compiles_With_Derived_Collection()
{
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    var vmCode = """
public class ObservablePropertyAttribute : System.Attribute {}
public class RelayCommandAttribute : System.Attribute {}
namespace HP.Telemetry { public enum Zone { CPUZ_0, CPUZ_1 } }
namespace HPSystemsTools.ViewModels { public class ThermalZoneComponentViewModel { public HP.Telemetry.Zone Zone { get; set; } public int Temperature { get; set; } }
public class ZoneCollection : System.Collections.ObjectModel.ObservableCollection<ThermalZoneComponentViewModel> {} }
public partial class TestViewModel : ObservableObject { [ObservableProperty] public partial HPSystemsTools.ViewModels.ZoneCollection Zones { get; set; } = new HPSystemsTools.ViewModels.ZoneCollection(); }
public class ObservableObject {}
""";
    var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
    File.WriteAllText(vmFile, vmCode);
    var refs = LoadDefaultRefs();
    var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");
    var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
    var tsClientFile = Path.Combine(tempDir, name + "RemoteClient.ts");
    File.WriteAllText(tsClientFile, ts);

    CreateTsStubs(tempDir, name, name + "Service");

    var testTs = $@"declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient, UpdatePropertyValueResponse }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  async getState(_req:any) {{
    return {{
      getZonesList: () => [{{ zone: 0, temperature: 42 }}, {{ zone: 1, temperature: 43 }}]
    }};
  }}
  updatePropertyValue(_req:any) {{ return Promise.resolve(new UpdatePropertyValueResponse()); }}
  subscribeToPropertyChanges(_req:any) {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(_req:any) {{ return Promise.resolve({{ getStatus: () => 0 }}); }}
  stateChanged(_req:any) {{ return Promise.resolve(); }}
  cancelTest(_req:any) {{ return Promise.resolve(); }}
}}
(async () => {{
  const client = new {name}RemoteClient(new FakeClient(''));
  await client.initializeRemote();
  if (client.zones.length !== 2 || client.zones[0].temperature !== 42 || client.zones[1].temperature !== 43) throw new Error('Collection transfer failed');
  client.dispose();
}})().catch(e => {{ console.error(e); process.exit(1); }});";
    File.WriteAllText(Path.Combine(tempDir, "test.ts"), testTs);

    var tsconfig = """
{
  "compilerOptions": {
    "target": "es2018",
    "module": "commonjs",
    "strict": false,
    "esModuleInterop": true,
    "lib": ["es2018", "dom"],
    "outDir": "dist",
    "allowJs": true,
    "types": ["node"]
  },
  "include": ["**/*.ts", "**/*.js"]
}
""";
    File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);

    var result = RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
    if (result.StartsWith("Powershell"))
        await RunCmdAsync("tsc", "--project tsconfig.json", tempDir);

    if (result.Length > 0) Assert.Fail(result);

    if (OperatingSystem.IsWindows())
        await RunCmdAsync("node", "test.js", Path.Combine(tempDir, "dist"));
}

    [Fact]
    public async Task Generated_TypeScript_Compiles_And_Transfers_ObservableCollection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vmCode = @"public class ObservablePropertyAttribute : System.Attribute {}\npublic class RelayCommandAttribute : System.Attribute {}\npublic partial class TestViewModel : ObservableObject { [ObservableProperty] public partial System.Collections.ObjectModel.ObservableCollection<int> Numbers { get; set; } = new System.Collections.ObjectModel.ObservableCollection<int>(); }\npublic class ObservableObject {}";
        var vmFile = Path.Combine(tempDir, "TestViewModel.cs");
        File.WriteAllText(vmFile, vmCode);
        var refs = LoadDefaultRefs();
        var (_, name, props, cmds, _) = await ViewModelAnalyzer.AnalyzeAsync(new[] { vmFile }, "ObservablePropertyAttribute", "RelayCommandAttribute", refs, "ObservableObject");
        var ts = TypeScriptClientGenerator.Generate(name, "Test.Protos", name + "Service", props, cmds);
        var tsClientFile = Path.Combine(tempDir, name + "RemoteClient.ts");
        File.WriteAllText(tsClientFile, ts);

        CreateTsStubs(tempDir, name, name + "Service");

        var testTs = $@"declare var process: any;
import {{ {name}RemoteClient }} from './{name}RemoteClient';
import {{ {name}ServiceClient, UpdatePropertyValueResponse }} from './generated/{name}ServiceServiceClientPb';
class FakeClient extends {name}ServiceClient {{
  lastReq: any;
  async getState(_req:any) {{
    return {{
      getNumbersList: () => [1,2,3]
    }};
  }}
  updatePropertyValue(req:any) {{ this.lastReq = req; return Promise.resolve(new UpdatePropertyValueResponse()); }}
  subscribeToPropertyChanges(_req:any) {{ return {{ on:()=>{{}}, cancel:()=>{{}} }} as any; }}
  ping(_req:any) {{ return Promise.resolve({{ getStatus: () => 0 }}); }}
  stateChanged(_req:any) {{ return Promise.resolve(); }}
  cancelTest(_req:any) {{ return Promise.resolve(); }}
}}
(async () => {{
  const grpcClient = new FakeClient('');
  const client = new {name}RemoteClient(grpcClient);
  // Override createAnyValue for testing to return the raw value instead of an Any object
  (client as any).createAnyValue = (v:any) => v;
  await client.initializeRemote();
  if (client.numbers.length !== 3 || client.numbers[1] !== 2) throw new Error('Initial transfer failed');
  await client.updatePropertyValue('Numbers', [4,5]);
  if (grpcClient.lastReq.getPropertyName() !== 'Numbers' || JSON.stringify(grpcClient.lastReq.getNewValue()) !== JSON.stringify([4,5])) throw new Error('Update transfer failed');
  client.dispose();
}})().catch(e => {{ console.error(e); process.exit(1); }});
";
        File.WriteAllText(Path.Combine(tempDir, "test.ts"), testTs);

        var tsconfig = $@"
{{
  ""compilerOptions"": {{
    ""target"": ""es2018"",
    ""module"": ""commonjs"",
    ""strict"": false,
    ""esModuleInterop"": true,
    ""lib"": [""es2018"", ""dom""],
    ""outDir"": ""dist"",
    ""allowJs"": true,
    ""types"": [""node""]
  }},
  ""include"": [""**/*.ts"", ""**/*.js""]
}}";
        File.WriteAllText(Path.Combine(tempDir, "tsconfig.json"), tsconfig);

    var result = RunPs("C:\\Program Files\\nodejs\\tsc.ps1", "--project tsconfig.json", tempDir);
    if (result.StartsWith("Powershell"))
        await RunCmdAsync("tsc", "--project tsconfig.json", tempDir);

    if (result.Length > 0) Assert.Fail(result);

    if (OperatingSystem.IsWindows())
        await RunCmdAsync("node", "test.js", Path.Combine(tempDir, "dist"));
}
}
