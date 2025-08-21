# RemoteMvvmTool

`RemoteMvvmTool` is a command line utility that turns a [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) view model into a gRPC service and matching remote clients.  It reads the C# view model source and generates:

- a `.proto` file describing the view model API
- a C# gRPC server implementation
- a C# client proxy that talks to the server
- a TypeScript client, or even a small TypeScript project if requested

The tool expects a view model that derives from `ObservableObject` and uses `[ObservableProperty]` for data and `[RelayCommand]` for actions.

## Installation

Build the project and install it as a .NET global tool:

```bash
cd src/RemoteMvvmTool
dotnet pack
# installs the `remotemvvm` tool from the local package output
dotnet tool install --global RemoteMvvm --add-source ./publish
```

After installation the tool is available as `remotemvvm`.

## Usage

```bash
remotemvvm [options] <viewmodel.cs> [additional.cs]
```

### Options

- `--generate <list>` – comma‑separated outputs to generate. Values: `proto`, `server`, `client`, `ts`, `tsproject`. Default is `all`.
- `--output <dir>` – directory for generated C# and TypeScript files. Defaults to `generated`.
- `--protoOutput <dir>` – directory for the generated `.proto` file. Defaults to `protos`.
- `--protoNamespace <ns>` – C# namespace for generated proto types. Defaults to `Generated.Protos`.
- `--serviceName <name>` – gRPC service name. Defaults to `<ViewModelName>Service`.
- `--clientNamespace <ns>` – namespace for the generated C# client proxy. Defaults to `<ViewModelNamespace>.RemoteClients`.

## Example

```bash
remotemvvm --generate proto,server,client,ts \
           --output generated --protoOutput protos \
           --protoNamespace MyApp.Protos \
           MyViewModel.cs
```

The command above analyzes `MyViewModel.cs` and writes the generated files into `generated/` and `protos/`.

### Supported Types

| C# type | Proto | Server | C# client | TS client | Update direction |
|---------|-------|:------:|:---------:|:---------:|------------------|
| `string` | `StringValue` | ✅ | ✅ | ✅ | 2‑way |
| `bool` | `BoolValue` | ✅ | ✅ | ✅ | 2‑way |
| `int` | `Int32Value` | ✅ | ✅ | ✅ | 2‑way |
| `long` | `Int64Value` | ✅ | ✅ | ✅ | 2‑way |
| `uint` | `UInt32Value` | ✅ | ✅ | ✅ | 2‑way |
| `float` | `FloatValue` | ✅ | ✅ | ✅ | 2‑way |
| `double` | `DoubleValue` | ✅ | ✅ | ✅ | 2‑way |
| `byte` | `UInt32Value` | ✅ | ✅ | ❌ | server → client |
| `sbyte` | `Int32Value` | ✅ | ✅ | ❌ | server → client |
| `short` | `Int32Value` | ✅ | ✅ | ❌ | server → client |
| `ushort` | `UInt32Value` | ✅ | ✅ | ❌ | server → client |
| `ulong` | `UInt64Value` | ✅ | ✅ | ❌ | server → client |
| `decimal` | `StringValue` | ✅ | ✅ | ❌ | server → client |
| `char` | `StringValue` | ✅ | ✅ | ❌ | server → client |
| `nint` | `Int64Value` | ✅ | ✅ | ❌ | server → client |
| `nuint` | `UInt64Value` | ✅ | ✅ | ❌ | server → client |
| `half` | `FloatValue` | ✅ | ✅ | ❌ | server → client |
| `Guid` | `StringValue` | ✅ | ✅ | ✅ | server → client |
| `enum` | `Int32Value` | ✅ | ✅ | ✅ | server → client |
| Arrays & lists (`T[]`, `List<T>`, `ObservableCollection<T>`) | `repeated` | ✅ | ✅ | ✅ | server → client |
| `Dictionary<TKey,TValue>` (scalar keys) | `map` | ✅ | ✅ | ✅ | server → client |
| Custom classes/structs | `message` | ✅ | ✅ | ✅ | server → client |
| `DateTime` | `Timestamp` | ✅ | ❌ | ❌ | n/a |
| `DateTimeOffset` | `Timestamp` | ✅ | ❌ | ❌ | n/a |
| `TimeSpan` | `Duration` | ✅ | ❌ | ❌ | n/a |
| `DateOnly` | `StringValue` | ✅ | ❌ | ❌ | n/a |
| `TimeOnly` | `StringValue` | ✅ | ❌ | ❌ | n/a |
| Unsupported numeric types (`IntPtr`, `UIntPtr`, `BigInteger`) | — | ❌ | ❌ | ❌ | n/a |
