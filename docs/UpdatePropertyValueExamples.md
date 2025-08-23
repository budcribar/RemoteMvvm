# Enhanced UpdatePropertyValue Examples

## Overview

The enhanced `UpdatePropertyValue` method provides comprehensive support for updating ViewModels remotely with:
- ? **Success/failure feedback** via `UpdatePropertyValueResponse`
- ? **Nested property paths** like `User.Address.Street`
- ? **Collection operations** with dictionary keys and array indices  
- ? **Different operation types** (set, add, remove, clear, insert)
- ? **Type validation and conversion**
- ? **Old value tracking** for undo operations

## Basic Usage

### Simple Property Update
```csharp
// Server Response
var response = await client.UpdatePropertyValue(new UpdatePropertyValueRequest 
{
    PropertyName = "UserName",
    NewValue = Any.Pack(new StringValue { Value = "John Doe" })
});

if (response.Success) 
{
    Console.WriteLine("Property updated successfully");
}
else 
{
    Console.WriteLine($"Update failed: {response.ErrorMessage}");
}
```

### TypeScript Client
```typescript
// Basic update
const response = await client.updatePropertyValue("userName", "John Doe");
if (response.getSuccess()) {
    console.log("Property updated successfully");
} else {
    console.log(`Update failed: ${response.getErrorMessage()}`);
}

// Advanced update with options
const response = await client.updatePropertyValueAdvanced("items", newValue, {
    arrayIndex: 2,
    operationType: 'set'
});
```

## Advanced Scenarios

### 1. Nested Property Updates
```csharp
var request = new UpdatePropertyValueRequest
{
    PropertyName = "User", 
    PropertyPath = "User.Profile.DisplayName",  // Navigate to nested property
    NewValue = Any.Pack(new StringValue { Value = "Updated Name" })
};
```

### 2. Dictionary Updates
```csharp
var request = new UpdatePropertyValueRequest
{
    PropertyName = "Settings",
    CollectionKey = "Theme",         // Dictionary key
    NewValue = Any.Pack(new StringValue { Value = "Dark" }),
    OperationType = "set"
};
```

### 3. Array Element Updates  
```csharp
var request = new UpdatePropertyValueRequest
{
    PropertyName = "Scores",
    ArrayIndex = 5,                  // Update element at index 5
    NewValue = Any.Pack(new Int32Value { Value = 1500 })
};
```

### 4. Complex Collection Operations
```csharp
// Add to collection
var addRequest = new UpdatePropertyValueRequest
{
    PropertyName = "Items",
    NewValue = Any.Pack(/* new item */),
    OperationType = "add"
};

// Remove from collection
var removeRequest = new UpdatePropertyValueRequest
{
    PropertyName = "Items", 
    CollectionKey = "item-id-123",
    OperationType = "remove"
};

// Clear collection
var clearRequest = new UpdatePropertyValueRequest
{
    PropertyName = "Items",
    OperationType = "clear"
};
```

## Supported Operations

| Operation | Description | Supported Collections |
|-----------|-------------|----------------------|
| `set` | Update/replace value (default) | All properties, dictionary values, array elements |
| `add` | Add new item | Lists, dictionaries, sets |
| `remove` | Remove existing item | Lists, dictionaries, sets |
| `clear` | Remove all items | Collections |
| `insert` | Insert at specific position | Lists, arrays |

## Error Handling

The response provides detailed error information:

```csharp
public class UpdatePropertyValueResponse 
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }        // Main error description
    public string ValidationErrors { get; set; }    // Validation details  
    public Any OldValue { get; set; }              // Previous value for undo
}
```

### Common Error Types:
- **Property not found**: `"Property 'UserName' not found"`
- **Read-only property**: `"Property 'Id' is read-only"`
- **Type conversion**: `"Cannot convert 'abc' to System.Int32"`
- **Index out of bounds**: `"Array index 10 is out of bounds (count: 5)"`
- **Invalid enum**: `"'Yellow' is not a valid value for enum Color"`

## Property Change Notifications

Enhanced notifications include additional context:

```protobuf
message PropertyChangeNotification {
  string property_name = 1;
  google.protobuf.Any new_value = 2;
  string property_path = 3;          // Full path for nested changes
  string change_type = 4;            // "property", "collection", "nested"  
  google.protobuf.Any old_value = 5; // Previous value
}
```

## Benefits

### ? **Better Error Handling**
- Know immediately if updates succeed or fail
- Get detailed error messages for debugging
- Implement proper error recovery

### ? **Complex Data Support**  
- Update nested object properties
- Modify dictionary entries by key
- Update specific array elements
- Support for collections and custom types

### ? **Undo/History Support**
- Access previous values via `old_value` 
- Implement undo functionality
- Track change history

### ? **Type Safety**
- Server-side type validation
- Proper type conversion
- Enum validation

### ? **Flexible Operations**
- Different operation types beyond simple assignment
- Collection-specific operations (add/remove/clear)
- Batch operations support

This enhanced implementation makes RemoteMvvm suitable for complex, production-ready applications with sophisticated data models and robust error handling requirements.