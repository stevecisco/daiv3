# CT-NFR-001

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
Dashboard SHOULD update in near real-time without blocking UI, using async/await patterns, proper thread marshaling, and debouncing to prevent performance degradation.

## Technical Guidance

### Async/Await Patterns
- **All I/O Operations:** Database queries, file reads, service calls MUST be async (`Task` / `Task<T>`)
- **No Blocking Waits:** Never call `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on UI thread
- **Fire-and-Forget Practices:** Use `_ = SomeAsyncMethod()` carefully; prefer explicit task handling

### Thread Marshaling (MAUI Dispatcher)
- **Background Thread Pattern:**
  - Fetch data on thread pool using `await FetchDashboardDataAsync()`
  - Marshal result to UI thread via `MainThread.BeginInvokeOnMainThread(() => ViewModel.UpdateMetrics(data))`
- **ViewModel Bindings:** All bound properties MUST update via MainThread.BeginInvokeOnMainThread()
- **No Synchronous Updates:** Database context, file system access on UI thread ONLY via async patterns

### Debouncing High-Frequency Updates
- **Metric Refresh Intervals:** 
  - System metrics (CPU/GPU/memory): 2-5 sec debounce
  - Queue status: 1-2 sec debounce
  - Agent activity: 1 sec debounce
  - Never update more frequently than 500ms
- **Implementation:** Use Timer or Task.Delay with CancellationToken
- **Rationale:** Prevents UI thread churn and battery drain

### Cancellation Token Support
- **All Async Operations:** Accept `CancellationToken` parameter
- **Navigation Cleanup:** Cancel outstanding tasks when navigating away from dashboard view
- **Page OnDisappearing:** Stop timers, cancel pending operations

### Error Handling & Graceful Degradation
- **Fetch Timeout:** 5 second default timeout for data fetches (configurable)
- **Fallback to Cache:** Show last known good state if fetch fails
- **Mark as Stale:** Visual indicator (e.g., timestamp \"as of 2 min ago\") when using cached data
- **Error Logging:** Log exceptions but don't crash dashboard; show error toast instead
- **Retry Logic:** Automatic retry with exponential backoff for transient failures

### Connection Pooling
- **Data Source Connections:** Reuse SqliteConnection pooling, avoid exhausting connection limits
- **ServiceCollection Singleton:** Register services as Singleton to maintain connection pool
- **Async Context:** Use proper async context for MAUI

### Performance Targets
- **Dashboard Load Time:** <2 seconds from view appearance to first data display
- **Metric Update Latency:** <200ms from data fetch completion to UI visual change
- **No UI Freezing:** UI remains responsive even during 10+ second data fetch
- **Memory Overhead:** <20MB for dashboard view including cached metrics

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Async/dispatch patterns directly address \"Async/Dispatch Patterns\" brainstorming item
- Debouncing prevents performance degradation mentioned in ideas
- Graceful degradation keeps app usable during network/service delays

## Testing Plan
- Unit tests: Mock services with simulated delays; verify async/await behavior
- UI tests: Rapid navigation between dashboard views; verify cancellation
- Performance tests: Measure UI responsiveness under load (10+ metric updates/sec)
- Stress test: Simulate slow data source (5+ second latency)
- Memory profiling: Verify no memory leaks in long-running dashboard
- Manual testing: Watch Task Manager; verify CPU stays <5% idle, smooth scrolling

## Usage and Operational Notes
- Dashboard is background-safe: can be left open indefinitely without UI degradation
- Data fetches continue in background even if user switches to other app tabs
- Closing dashboard stops all data fetches immediately
- Configuration options (refresh intervals) in Settings UI (CT-REQ-002)

## Dependencies
- KLC-REQ-011 (MAUI framework)
- All dashboard requirements (CT-REQ-003 through CT-REQ-009)

## Related Requirements
- CT-REQ-003 (dashboard foundation)
- CT-REQ-004, CT-REQ-005, CT-REQ-006, CT-REQ-007, CT-REQ-008, CT-REQ-009 (specific dashboard views)
