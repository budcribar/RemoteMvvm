# Enhancements / Technical Debt Backlog

## 1. GUI Test Initialization & Flakiness
**Issue**: Intermittent failures when WPF and WinForms end-to-end tests run together. Individually they pass. Failures show default / partial model state or missing structural numbers.

**Root Causes (Observed / Likely)**
- Generated strongly-typed test client `InitializeAsync` originally used a fixed delay, not a readiness handshake.
- Snapshot-first (GetState ? Subscribe) pattern could miss ultra-early mutations if future models add them.
- Potential for missed streamed updates if models mutate immediately post-connect.
- Numeric “digest” extraction heuristics amplify timing sensitivity.
- Parallel execution of WPF + WinForms test classes increases scheduling variability.

**Not Root Cause**
- Cross-process static interference (WPF/WinForms servers are distinct OS processes when launched via `dotnet run`).
- Server ViewModel constructor not running.

**Proposed Improvements**
| Priority | Enhancement | Notes |
|----------|-------------|-------|
| High | Make `InitializeAsync` return validated initial snapshot (state digest or structural) | Removes blind timing | 
| High | Introduce versioning (`long Version`) in `GetState` + notifications; ignore stale updates | Deterministic sync |
| Medium | Optional subscribe-first buffering with version barrier | Requires proto change |
| Medium | Add `/status` JSON with `modelReady:true/false` | Strong readiness gate |
| Medium | Replace numeric digest tests with structural + property assertions | Less brittle |
| Low | Stream initial full state as first frame on change stream | Cleanest but larger proto change |
| Low | Enhanced logging: ctor, first GetState, subscription active timestamps | Faster diagnosis |

**Acceptance Criteria**
- Zero flakiness over 50 consecutive combined WPF+WinForms suite runs.
- Can remove ad-hoc tolerances / distinct-only relaxations.
- Deterministic prevention of stale update overwrite.

---
## 2. Test Architecture
- Group GUI (WPF + WinForms) tests into single non-parallel xUnit collection (implemented).
- Keep other test classes parallel for performance.

---
## 3. Future Quality Tasks
- Add deterministic seed + synthetic `ReadyVersion` barrier property.
- Switch large collection tests to structural size + sampled hash verification.
- Add perf metrics (startup latency, snapshot latency, subscription activation lag).

---
## 4. Tracking
Open issues referencing these sections as they are implemented. This file serves as the living index.
