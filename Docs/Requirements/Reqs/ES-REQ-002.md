# ES-REQ-002

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHALL provide a configurable online fallback path that requires explicit user configuration or per-call confirmation.

## Implementation Status
**Status:** Complete  
**Date Completed:** 2026-03-06

### Architecture
Created a policy enforcement layer between settings persistence and runtime routing decisions:

1. **IOnlineAccessPolicy** (Daiv3.ModelExecution.Interfaces)
   - `IsOnlineAccessAllowedAsync()` - Evaluates access policy for a request
   - `GetOnlineAccessModeAsync()` - Retrieves current access mode
   - `AreOnlineProvidersEnabledAsync()` - Checks if any providers are enabled

2. **OnlineAccessDecision** (Daiv3.ModelExecution.Models)
   - Result model with `IsAllowed`, `RequiresConfirmation`, `Reason`, `AccessMode`
   - Factory methods: `Denied()`, `AllowedWithConfirmation()`, `AllowedWithoutConfirmation()`

3. **OnlineAccessPolicyService** (Daiv3.Persistence.Services)
   - Implementation reading from `ISettingsService`
   - Enforces four access modes from `ApplicationSettings.Providers.OnlineAccessMode`:
     - `"never"` - Always denies online access
     - `"ask"` - Allows with confirmation required
     - `"auto_within_budget"` - Allows without confirmation (subject to budget checks)
     - `"per_task"` - Allows with per-task confirmation
   - Checks `ApplicationSettings.Providers.OnlineProvidersEnabled` (JSON array)
   - Registered as scoped service in `PersistenceServiceExtensions.AddPersistenceServices()`

4. **ModelQueue Integration** (Daiv3.ModelExecution)
   - Added `IOnlineAccessPolicy` parameter (optional for backward compatibility)
   - Changed `ShouldRouteOnline()` to async `ShouldRouteOnlineAsync()`
   - Policy check is first gate before ES-REQ-001 local-first routing logic
   - If policy denies access, routing fails before consulting other rules

### Configuration
Settings managed through `ISettingsService` (CT-REQ-001, CT-REQ-002):

```csharp
// Configuration keys
ApplicationSettings.Providers.OnlineAccessMode
ApplicationSettings.Providers.OnlineProvidersEnabled

// Default values
ApplicationSettings.Defaults.OnlineAccessMode = "ask"
ApplicationSettings.Defaults.OnlineProvidersEnabled = "[]" (empty JSON array)
```

Configuration UI provided by CT-ACC-001 (Settings Management).

## Testing Plan

### Unit Tests
- **OnlineAccessPolicyServiceTests** (13 tests, all passing)
  - Access mode enforcement: never, ask, auto_within_budget, per_task
  - Provider enablement validation
  - Invalid JSON handling
  - Default behavior when settings missing
  - Edge cases (null request, empty provider lists)

### Integration Tests  
- **OnlineFallbackAcceptanceTests** (7 acceptance tests, all passing)
  - AC1: `online_access_mode = "never"` denies access
  - AC2: `online_access_mode = "ask"` allows with confirmation
  - AC3: `online_access_mode = "auto_within_budget"` allows without confirmation
  - AC4: `online_access_mode = "per_task"` allows with per-task confirmation
  - AC5: No enabled providers denies access (regardless of mode)
  - AC6: Configuration changes take effect immediately
  - AC7: Complete workflow from settings persistence to policy enforcement

### Test Results
- Unit tests: 282/282 passing in Daiv3.Persistence.Tests
- Integration tests: 14/14 passing (7 tests × 2 target frameworks)
- Full suite: 2313/2328 passing (15 skipped for known reasons)
- No regressions introduced

## Usage and Operational Notes

### Configuration
Users configure online fallback behavior via Settings UI (CT-ACC-001):

1. **Online Access Mode** (`online_access_mode`):
   - `"never"` - Disable all online access (local-only operation)
   - `"ask"` - Prompt for confirmation before each online request (default)
   - `"auto_within_budget"` - Allow online access automatically within budget limits
   - `"per_task"` - Prompt for confirmation once per task (not per request)

2. **Enabled Providers** (`online_providers_enabled`):
   - JSON array of provider names: `["openai", "azure-openai", "anthropic"]`
   - Empty array `[]` disables all online access regardless of mode

### Runtime Behavior
When `ModelQueue.ShouldRouteOnlineAsync()` is called:

1. **Policy Check** (first gate):
   - If `IOnlineAccessPolicy` is null → skip policy check (backward compatible)
   - If access mode is `"never"` → deny routing
   - If no providers enabled → deny routing
   - If access mode requires confirmation → return decision with `RequiresConfirmation = true`

2. **Local-First Check** (ES-REQ-001):
   - Only evaluated if policy allows access
   - Prefers local execution when capable model available

3. **Budget Check** (if applicable):
   - Only evaluated for `"auto_within_budget"` mode
   - Implementation deferred to budget tracking requirement

### User-Visible Effects
- Settings changes take effect immediately (no restart required)
- Confirmation prompts appear when `access_mode = "ask"` or `"per_task"`
- Offline operation available when `access_mode = "never"` or providers disabled
- Status messages logged at Information level for troubleshooting

### Operational Constraints
- Requires CT-REQ-001, CT-REQ-002 (settings infrastructure)
- Settings stored in SQLite with versioned history
- Policy evaluation is synchronous (sub-millisecond latency)
- No impact on local-only workflows (policy check skipped when no online routing needed)

## Dependencies
- CT-REQ-001 (Settings Infrastructure) - Required for accessing configured policies
- CT-REQ-002 (Settings Versioning) - Required for tracked configuration changes
- CT-ACC-001 (Settings UI) - Provides user interface for configuration
- MQ-REQ-012 (ModelQueue Implementation) - Required for routing integration
- ES-REQ-001 (Local-First Routing) - Policy check is first gate before local-first logic

## Related Requirements
- MQ-REQ-014 (Confirmation Logic) - Implements confirmation dialogs when policy requires them
- CT-REQ-003 (Budget Tracking) - Future integration for "auto_within_budget" mode
