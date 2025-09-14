# Code Generation and Synchronization

RemoteMvvm turns a `CommunityToolkit.Mvvm` view model into a gRPC service and a set of remote clients. The `RemoteMvvmTool` reads the source view model and emits:

- a `.proto` file describing the view model API
- a C# gRPC server implementation
- client proxies in C# (with optional WPF/WinForms UI) or TypeScript

## Generated Server

The generated gRPC service hosts the original view model and exposes methods such as `GetState`, `UpdatePropertyValue`, and `SubscribeToPropertyChanges`. It tracks subscribers and pushes `PropertyChangeNotification` messages whenever the view model raises `PropertyChanged`.

## Generated Client

Client proxies mirror the view model and implement `INotifyPropertyChanged`. When a property setter runs, the client sends an `UpdatePropertyValue` RPC to the server. Clients also start a long‑running subscription to receive server‑initiated changes and apply them locally.

## Keeping the Server in Sync (Client → Server)

1. The user edits a bound control.
2. The property setter calls `UpdatePropertyValueAsync`, which sends a gRPC `UpdatePropertyValue` request.
3. The server updates its view model and returns a response.
4. The server's view model raises `PropertyChanged`, triggering a `PropertyChangeNotification` to all subscribers.
5. The originating client already has the new value; other clients apply the update and refresh their UI.

## Keeping the Client in Sync (Server → Client)

1. Server code or another client modifies a view model property.
2. The server's `PropertyChanged` handler packages the change into a `PropertyChangeNotification` and writes it to each subscriber stream.
3. Each client receives the notification, sets the corresponding property, and `INotifyPropertyChanged` refreshes the UI.
4. Because the property setter sees no change from the current value, no further update is sent back to the server, preventing feedback loops.

This bidirectional flow keeps both sides synchronized with minimal boilerplate while letting the developer focus on the original `ObservableObject` view model.
