# RemoteMvvm End-to-End Overview

RemoteMvvm converts a `CommunityToolkit.Mvvm` view model into a gRPC service together with client libraries that keep UI layers synchronized across processes. This document consolidates the architecture philosophy, generation pipeline, and property update mechanics into a single reference.

## Architectural Principles

- **Do the heavy lifting at generation time.** Roslyn analysis collects every detail about the view model before the application ships so the runtime never has to rediscover metadata.
- **Stay framework independent for as long as possible.** The tool produces neutral representations of layout and property trees. Framework-specific translators (WPF, WinForms, or TypeScript UI shims) consume those structures without duplicating logic.
- **Minimize runtime reflection.** Generated code binds metadata to controls and reacts to live data changes directly, keeping runtime work predictable and lightweight.

## Generation Pipeline

### 1. Roslyn-driven view-model analysis
`PropertyDiscoveryUtility` walks the source view model and stores rich metadata inside `PropertyAnalysis` and `PropertyMetadata` structures. For every property the generator captures:

- Safe identifier names for code emission and access paths for nested members.
- Property categorization (simple, boolean, enum, collection, complex, or read-only).
- Nullability hints, array/collection sizing semantics, and converter information.
- UI hints such as suggested editors and whether the value is mutable on the client.

This catalog becomes the authoritative description used by every downstream generator.

### 2. Intermediate representations
Once the view model is analyzed, the generator emits framework-neutral descriptions.

#### UIComponent layout DSL
A `UIComponent` tree describes the shell of the generated property explorer. Each node records the element type, optional name, textual content, attributes, and children. Translators walk the tree to create identical split panes, toolbars, and detail panels across frameworks.

#### Property tree commands
The property hierarchy is encoded as an ordered list of `TreeCommand` records. Commands instruct the translator to create nodes, attach children, apply metadata tags, and mark collection boundaries. Each generated node carries a `PropertyNodeInfo` payload that links UI selections back to the underlying property, instance, and collection indices.

### 3. Code emission targets
Using the intermediate representation, the tool produces:

- A `.proto` contract that exposes the view model API through `GetState`, `UpdatePropertyValue`, and `SubscribeToPropertyChanges` RPCs.
- A C# gRPC server hosting the original view model, orchestrating property updates, and streaming `PropertyChangeNotification` messages to subscribers.
- Client proxies in C#, WPF, WinForms, or TypeScript. Each proxy mirrors the view model shape, implements `INotifyPropertyChanged`, and manages outbound property updates plus inbound change subscriptions.

## Example walkthrough: `TestModel`

The following example shows the complete journey for a small view model.

### View-model definition
```csharp
public partial class TestModel : ObservableObject
{
    [ObservableProperty]
    private string status = "Ready";

    [ObservableProperty]
    private UserProfile profile = new();

    public ObservableCollection<Order> Orders { get; } = new();

    public Dictionary<string, string> Settings { get; } = new();

    public DateTime LastUpdated { get; private set; }

    partial void OnStatusChanged(string value)
    {
        LastUpdated = DateTime.UtcNow;
    }
}

public partial class UserProfile : ObservableObject
{
    [ObservableProperty]
    private string displayName = "Guest";

    [ObservableProperty]
    private Address address = new();
}

public partial class Address : ObservableObject
{
    [ObservableProperty]
    private string street = "1 Remote Way";

    [ObservableProperty]
    private string city = "Roslyn";
}

public class Order
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public decimal Total { get; set; }
}
```

### Property analysis output
`PropertyDiscoveryUtility` classifies the surface area as follows:

| Property | Path | Category | Notes |
|----------|------|----------|-------|
| `Status` | `TestModel.Status` | Simple (string) | Editable, generates direct editor binding. |
| `Profile` | `TestModel.Profile` | Complex | Generates nested metadata; child properties inherit editability. |
| `Profile.DisplayName` | `TestModel.Profile.DisplayName` | Simple (string) | Nested property reachable through `PropertyPath`. |
| `Profile.Address.Street` | `TestModel.Profile.Address.Street` | Simple (string) | Nested property with safe accessors and update path. |
| `Orders` | `TestModel.Orders` | Collection (`ObservableCollection<Order>`) | Supports add/remove/insert operations. |
| `Settings` | `TestModel.Settings` | Dictionary (`Dictionary<string, string>`) | Supports key-based updates. |
| `LastUpdated` | `TestModel.LastUpdated` | Simple (DateTime) | Read-only on clients because of the private setter. |

The metadata also captures nullability, default values, and safe identifiers for method generation.

### Intermediate representations for `TestModel`

#### Layout description
The generator synthesizes a `UIComponent` tree resembling:

```csharp
new UIComponent("Grid")
    .WithName("RootLayout")
    .WithChildren(
        new UIComponent("GridSplitter").WithAttribute("Column", 0),
        new UIComponent("TreeView")
            .WithName("PropertyTree")
            .WithAttribute("Column", 0),
        new UIComponent("StackPanel")
            .WithName("DetailsPanel")
            .WithAttribute("Column", 1)
            .WithChildren(
                new UIComponent("TextBox")
                    .WithName("StatusEditor")
                    .WithAttribute("Binding", "Status"),
                new UIComponent("ContentPresenter")
                    .WithName("DynamicEditors")));
```

Translators replay this tree to emit XAML, WinForms code, or TypeScript widget creation with identical structure.

#### Property tree commands
A condensed set of `TreeCommand` instructions might look like:

```csharp
CreateNode("Status", NodeKind.SimpleProperty);
CreateNode("Profile", NodeKind.ComplexProperty);
PushParent();
CreateNode("DisplayName", NodeKind.SimpleProperty);
CreateNode("Address", NodeKind.ComplexProperty);
PushParent();
CreateNode("Street", NodeKind.SimpleProperty);
CreateNode("City", NodeKind.SimpleProperty);
PopParent();
PopParent();
CreateCollectionNode("Orders", NodeKind.Collection);
CreateDictionaryNode("Settings", NodeKind.Dictionary);
CreateNode("LastUpdated", NodeKind.ReadOnlyProperty);
```

The resulting tree nodes embed `PropertyNodeInfo` so runtime selection handlers immediately know which property and collection element are represented.

### Generated artifacts

#### Proto service
```protobuf
service TestModelService {
  rpc GetState(google.protobuf.Empty) returns (GetStateResponse);
  rpc UpdatePropertyValue(UpdatePropertyValueRequest) returns (UpdatePropertyValueResponse);
  rpc SubscribeToPropertyChanges(SubscribeRequest) returns (stream PropertyChangeNotification);
}
```

`UpdatePropertyValueRequest` exposes fields such as `property_name`, `property_path`, `collection_key`, `array_index`, `operation_type`, and `google.protobuf.Any new_value` so clients can target nested or collection members precisely.

#### Server implementation
```csharp
public override async Task<UpdatePropertyValueResponse> UpdatePropertyValue(UpdatePropertyValueRequest request, ServerCallContext context)
{
    var result = await _updateDispatcher.TryApplyAsync(_viewModel, request);
    if (!result.Success)
    {
        return new UpdatePropertyValueResponse
        {
            Success = false,
            ErrorMessage = result.ErrorMessage,
            ValidationErrors = result.ValidationDetails
        };
    }

    NotifySubscribers(result.PropertyName, result.PropertyPath, result.NewValue, result.OldValue, result.ChangeType);

    return new UpdatePropertyValueResponse
    {
        Success = true,
        OldValue = result.OldValue
    };
}
```
The server also implements `GetState` to serialize the full view model snapshot and `SubscribeToPropertyChanges` to stream updates through `IServerStreamWriter<PropertyChangeNotification>`.

#### Client proxies
C# clients mirror the original view model and automatically propagate setter changes:

```csharp
public string Status
{
    get => _status;
    set => SetProperty(ref _status, value, async () =>
    {
        await _service.UpdatePropertyValueAsync(new UpdatePropertyValueRequest
        {
            PropertyName = "Status",
            NewValue = Any.Pack(new StringValue { Value = value })
        });
    });
}
```

TypeScript clients expose the same surface:

```typescript
await testModelClient.updatePropertyValue("profile", {
  propertyPath: "Profile.Address.Street",
  newValue: "42 Generator Ave"
});
```

Both clients start a long-running subscription that listens for `PropertyChangeNotification` messages and applies them locally without triggering feedback loops.

## Property updates and synchronization

### Client → server flow
1. A user edits the **Street** field in a WPF client bound to `Profile.Address.Street`.
2. The generated setter packs the value into an `Any` payload and calls `UpdatePropertyValueAsync` with:
   ```csharp
   new UpdatePropertyValueRequest
   {
       PropertyName = "Profile",
       PropertyPath = "Profile.Address.Street",
       NewValue = Any.Pack(new StringValue { Value = "100 Main" })
   }
   ```
3. The server resolves the path through the analyzed metadata, updates the underlying `TestModel.Profile.Address.Street`, and records the previous value for undo scenarios.
4. A successful response returns `Success = true` plus the `OldValue` ("1 Remote Way"). Clients receive immediate confirmation to provide optimistic UI feedback.
5. The server raises `PropertyChanged` on the real view model. A change notification is dispatched to all subscribers except the originator, which already reflects the new value.

### Server → client flow
1. The server updates `Settings["Theme"] = "Dark"` in response to an admin command.
2. The `PropertyChanged` event causes the update dispatcher to enqueue a `PropertyChangeNotification`:
   ```protobuf
   message PropertyChangeNotification {
     string property_name = 1;
     google.protobuf.Any new_value = 2;
     string property_path = 3;
     string change_type = 4; // "property", "collection", or "nested"
     google.protobuf.Any old_value = 5;
   }
   ```
3. Each connected client receives the message, looks up the property metadata, and updates its local proxy. Because the setter detects that the value is already synchronized, it does not send another update back to the server, avoiding loops.

### Enhanced `UpdatePropertyValue` capabilities
The dispatcher supports a wide range of scenarios:

- Success and failure reporting through `UpdatePropertyValueResponse` with detailed error messages and validation text.
- Nested property traversal via `property_path` (e.g., `Profile.Address.Street`).
- Collection operations for lists, arrays, dictionaries, and sets.
- Type conversion and enum validation before the underlying view model is mutated.
- Old value tracking to enable undo or change history.

Supported operations:

| Operation | Description | Supported collections |
|-----------|-------------|-----------------------|
| `set` | Replace a value (default) | Properties, dictionary entries, array/list elements |
| `add` | Append a new element | Lists, dictionaries, sets |
| `remove` | Remove an existing element | Lists, dictionaries, sets |
| `clear` | Remove all items | Collections |
| `insert` | Insert at a specific position | Lists, arrays |

Errors are reported consistently:

```csharp
public class UpdatePropertyValueResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public string ValidationErrors { get; set; }
    public Any OldValue { get; set; }
}
```
Common failures include missing properties, read-only setters, type conversion issues, invalid enum values, or out-of-range indices. Clients can display these messages or retry with corrected data.

## Runtime responsibilities and extensibility
At runtime the generated application focuses on a narrow set of tasks:

- Instantiate the declared controls, wire standard events, and call the generated `LoadTree` method to populate the property explorer using compile-time metadata.
- React to UI events by consulting `PropertyNodeInfo` payloads and presenting editors with precomputed hints.
- Maintain a gRPC channel that handles `GetState`, `UpdatePropertyValue`, and subscription streams.

Adding new frameworks requires implementing translators for the layout tree and property commands. Enhancing property semantics flows through `PropertyDiscoveryUtility`, `PropertyNodeInfo`, and `TreeCommand` extensions without changing runtime logic.
