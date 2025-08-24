# RemoteMvvm TODO - Future Enhancements

## ?? Current Issues & Fixes

### PropertyChanged Threading Issues (In Progress)
- [ ] **CRITICAL**: Fix `FirePropertyChangedOnUIThread = false` for console/gRPC applications
  - Issue: PropertyChanged events try to marshal to non-existent UI thread in console apps
  - Solution: Detect console mode and set `FirePropertyChangedOnUIThread = false` in constructor
  - Status: Template updated, needs testing

### UpdatePropertyValue Race Conditions
- [ ] **HIGH**: Implement sequence number ordering for out-of-order updates
  - Issue: Client sends Update A, then Update B, but server processes B then A (wrong final state)
  - Solution: Add `sequence_number` and `client_id` to `UpdatePropertyValueRequest`
  - Implementation: Buffer out-of-order updates, apply in sequence
  - Reference: Previous successful implementation by user

### Thread Safety
- [x] **MEDIUM**: Remove unsafe event handler manipulation during property updates
  - Issue: Multiple concurrent UpdatePropertyValue calls create race conditions
  - Solution: Removed event handler add/remove pattern from `HandleSetOperation`
  - Status: ? Completed

## ?? New Architecture - Client ViewModel Pattern

### Async Property Setters (Major Enhancement)
- [ ] **HIGH**: Design async property setter pattern for client ViewModels
  - Current Problem: C# property setters cannot be async
  - Client needs: Set property ? Server update ? Confirm response ? Update local state
  
#### Proposed Solutions to Evaluate:
1. **Method-Based API (Explicit Async)**
   ```csharp
   public string Status => _status; // Read-only property
   public async Task SetStatusAsync(string value) { /* server update */ }
   ```

2. **Command Pattern**
   ```csharp
   public IAsyncRelayCommand<string> SetStatusCommand { get; }
   ```

3. **Hybrid Properties + Async Methods**
   ```csharp
   public string Status 
   { 
       get => _status; 
       set => _ = UpdateStatusAsync(value); // Fire-and-forget
   }
   public Task SetStatusAsync(string value) { /* explicit async */ }
   ```

4. **Response-Based Confirmation**
   - Rely on UpdatePropertyValue response instead of PropertyChanged events
   - Client knows property was set based on successful response

### Client ViewModel Architecture
- [ ] **HIGH**: Create `ClientViewModel` base class pattern
- [ ] **MEDIUM**: Implement optimistic updates with rollback on failure
- [ ] **MEDIUM**: Add client-side validation before server updates
- [ ] **LOW**: Consider caching strategies for frequently accessed properties

## ?? Protocol & Communication Enhancements

### Sequence Ordering Implementation
- [ ] **HIGH**: Add sequence fields to protobuf messages:
  ```protobuf
  message UpdatePropertyValueRequest {
    // ... existing fields ...
    int64 sequence_number = 7;  // Client-provided sequence
    string client_id = 8;       // To separate sequences per client
  }
  ```

- [ ] **HIGH**: Implement server-side sequence buffering:
  ```csharp
  private static readonly ConcurrentDictionary<string, long> _lastSequencePerClient = new();
  private static readonly ConcurrentDictionary<string, SortedDictionary<long, UpdatePropertyValueRequest>> _bufferedUpdates = new();
  ```

### Advanced Features
- [ ] **MEDIUM**: Add timeout mechanism for buffered updates
- [ ] **MEDIUM**: Implement buffer size limits to prevent memory issues
- [ ] **LOW**: Handle client restart/reconnection sequence number resets
- [ ] **LOW**: Consider per-property vs global sequence numbering

## ?? Testing & Validation

### Property Change Testing - UPDATED APPROACH
- [ ] **HIGH**: Create proper test for server-initiated PropertyChanged streaming
  - **Issue**: Current test expects PropertyChanged from client-initiated UpdatePropertyValue
  - **Solution**: Test should focus on UpdatePropertyValue SUCCESS response (not PropertyChanged)
  - **New test needed**: Server background processes that trigger PropertyChanged events
  - Status: New test files created: `test_server_initiated_changes.js`, `test_server_side_property_changes.cs`

### Two Distinct Scenarios to Test:
1. **Client-Initiated Updates** (? Should test SUCCESS response)
   - Client calls UpdatePropertyValue 
   - Server updates property via reflection
   - Client relies on response.Success (not PropertyChanged)
   - **No streaming needed** - client already knows what changed

2. **Server-Initiated Updates** (? Needs proper test)
   - Server updates properties in background (timers, business logic, external events)
   - PropertyChanged events MUST fire and stream to clients
   - Clients get notified of changes they didn't initiate
   - **This is where PropertyChanged streaming is critical**

### Current Test Issues
- [ ] **HIGH**: Fix `SubscribeToPropertyChanges_Simple_Test` - wrong expectation
  - Current: Expects PropertyChanged after UpdatePropertyValue
  - Should: Test UpdatePropertyValue SUCCESS response + separate server-initiated changes test
- [ ] **MEDIUM**: Add tests for multiple concurrent property updates
- [ ] **MEDIUM**: Add tests for out-of-order update scenarios  
- [ ] **LOW**: Add stress tests for rapid property updates

### New Testing Strategy
- [ ] **HIGH**: Implement background property changes in test ViewModels
  - Add timer-based property updates
  - Add manual trigger methods for testing
  - Verify PropertyChanged events fire with FirePropertyChangedOnUIThread = false
- [ ] **HIGH**: Create end-to-end test for server-initiated streaming
  - Server has background processes updating properties
  - Client subscribes to PropertyChanged stream
  - Verify client receives notifications of server changes

## ?? Documentation & Examples

### Architecture Documentation
- [ ] **MEDIUM**: Document the console vs UI app property change differences
- [ ] **MEDIUM**: Create async property patterns guide
- [ ] **LOW**: Add sequence ordering implementation examples

### Code Examples
- [ ] **LOW**: Update demo projects with new async property patterns
- [ ] **LOW**: Create "Best Practices" guide for RemoteMvvm usage

## ?? Future Considerations

### Performance Optimizations
- [ ] **LOW**: Batch multiple property updates in single gRPC call
- [ ] **LOW**: Add compression for large property values
- [ ] **LOW**: Implement property change filtering/subscription

### Advanced Scenarios
- [ ] **LOW**: Multi-server property synchronization
- [ ] **LOW**: Conflict resolution strategies for concurrent updates
- [ ] **LOW**: Property versioning and history tracking

## ?? Research & Investigation

### Community Toolkit Mvvm Integration
- [ ] **MEDIUM**: Investigate if CommunityToolkit automatically detects console mode
- [ ] **LOW**: Research compatibility with other MVVM frameworks

### gRPC Streaming Optimization
- [ ] **LOW**: Research gRPC-Web streaming performance optimizations
- [ ] **LOW**: Investigate alternative streaming protocols

---

## ?? Priority Matrix

### Immediate (Next Sprint)
1. Fix PropertyChanged console mode issue
2. Implement basic sequence ordering
3. Design async property setter API

### Short Term (Next Month)
1. Complete sequence ordering implementation
2. Create ClientViewModel base class
3. Fix all failing tests

### Long Term (Future Versions)
1. Advanced conflict resolution
2. Performance optimizations
3. Multi-server scenarios

---

**Last Updated**: 2024-12-19  
**Status**: Active Development  
**Next Review**: After PropertyChanged fix completion