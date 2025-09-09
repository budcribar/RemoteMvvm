# RemoteMvvmTool

`RemoteMvvmTool` is a command line utility that turns a [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) view model into a gRPC service and matching remote clients.  It reads the C# view model source and generates:

- a `.proto` file describing the view model API
- a C# gRPC server implementation
- C# client proxies that talk to the server (with optional WPF or WinForms GUI)
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
- `--platform <type>` – GUI platform for client generation. Values: `wpf`, `winforms`. Generates console client if not specified.

## Example

```bash
remotemvvm --generate proto,server,client,ts \
           --output generated --protoOutput protos \
           --protoNamespace MyApp.Protos \
           --platform wpf \
           MyViewModel.cs
```

The command above analyzes `MyViewModel.cs` and writes the generated files into `generated/` and `protos/`, creating a WPF client application.

## GUI Client Features

When using `--platform wpf` or `--platform winforms`, the tool generates rich GUI applications with:

- **Automatic Property Discovery**: Intelligently categorizes properties into Simple, Boolean, Collection, Enum, and Complex types
- **Dynamic UI Generation**: Creates appropriate controls (TextBox, CheckBox, ComboBox, ListBox) based on property types  
- **Real-time Data Binding**: Two-way binding with automatic updates via INotifyPropertyChanged
- **Command Integration**: Buttons for all RelayCommands with error handling
- **Tree View Navigation**: Hierarchical display of view model properties with expand/collapse
- **Connection Status**: Visual feedback for gRPC connection state
- **Error Handling**: Graceful degradation with detailed error reporting

### Supported Types

| C# type | Proto | Server | WPF Client | WinForms Client | TS client | Update direction |
|---------|-------|:------:|:----------:|:---------------:|:---------:|------------------|
| `string` | `StringValue` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `bool` | `BoolValue` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `int` | `Int32Value` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `long` | `Int64Value` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `uint` | `UInt32Value` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `float` | `FloatValue` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `double` | `DoubleValue` | ✅ | ✅ | ✅ | ✅ | 2‑way |
| `byte` | `UInt32Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `sbyte` | `Int32Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `short` | `Int32Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `ushort` | `UInt32Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `ulong` | `UInt64Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `decimal` | `StringValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `char` | `StringValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `nint` | `Int64Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `nuint` | `UInt64Value` | ✅ | ✅ | ✅ | ❌ | server → client |
| `half` | `FloatValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `Guid` | `StringValue` | ✅ | ✅ | ✅ | ✅ | server → client |
| `enum` | `Int32Value` | ✅ | ✅ | ✅ | ✅ | server → client |
| Arrays & lists (`T[]`, `List<T>`, `ObservableCollection<T>`) | `repeated` | ✅ | ✅ | ✅ | ✅ | server → client |
| `Dictionary<TKey,TValue>` (scalar keys) | `map` | ✅ | ✅ | ✅ | ✅ | server → client |
| Custom classes/structs | `message` | ✅ | ✅ | ✅ | ✅ | server → client |
| `Memory<T>` / `ReadOnlyMemory<T>` | `BytesValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `Span<T>` / `ReadOnlySpan<T>` | `BytesValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `DateTime` | `Timestamp` | ✅ | ✅ | ✅ | ❌ | server → client |
| `DateTimeOffset` | `Timestamp` | ✅ | ✅ | ✅ | ❌ | server → client |
| `TimeSpan` | `Duration` | ✅ | ✅ | ✅ | ❌ | server → client |
| `DateOnly` | `StringValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| `TimeOnly` | `StringValue` | ✅ | ✅ | ✅ | ❌ | server → client |
| Unsupported numeric types (`IntPtr`, `UIntPtr`, `BigInteger`) | — | ❌ | ❌ | ❌ | ❌ | n/a |

### GUI Client Update Directions

- **2-way**: Full bidirectional synchronization between client and server
- **server → client**: Server pushes updates to client; client displays read-only values  
- **n/a**: Type not supported in GUI clients

## PropertyDiscovery System

The tool includes an advanced **PropertyDiscovery** system that automatically:

- **Analyzes property types** at build time using reflection and semantic analysis
- **Categorizes properties** into Simple, Boolean, Collection, Enum, and Complex types
- **Generates appropriate UI controls** for each property category
- **Handles edge cases** like nullable types, Memory<T>, and custom enums
- **Provides real-time updates** through INotifyPropertyChanged monitoring
- **Graceful error handling** with detailed diagnostics for unsupported scenarios

This ensures that generated GUI applications work reliably with any valid CommunityToolkit.Mvvm view model without manual configuration.
