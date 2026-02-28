# MQ-REQ-017 Implementation Documentation

## Summary

**Requirement:** The system SHALL rate-limit requests per provider.

**Status:** ✅ COMPLETE  
**Progress:** 100%

**Outcome:** `OnlineProviderRouter` now enforces provider-scoped request rate limits using per-provider configurable request windows.

---

## Implementation

### Owning Component
- `OnlineProviderRouter` in `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`
- Provider configuration in `src/Daiv3.ModelExecution/OnlineProviderOptions.cs`

### Core Behavior Added

#### 1. Provider-Scoped Rate Limit Configuration
Added to `ProviderConfig`:

```csharp
public int RateLimitWindowSeconds { get; set; } = 60;
public int MaxRequestsPerWindow { get; set; } = 60;
```

Behavior:
- Defines a per-provider time window and request capacity.
- Defaults support baseline throttling without requiring explicit config.
- Rate limiting can be disabled per provider by setting either value to `<= 0`.

#### 2. Thread-Safe Provider Window Tracking
Added provider-scoped request window state:
- `_providerRequestWindows` keyed by provider name.
- `_providerRateLimitLock` for synchronized window checks/updates.

Behavior:
- Expired request timestamps are purged when evaluating current capacity.
- Request slot reservation occurs atomically before provider execution.

#### 3. Shared Execution Path Enforcement
Rate limit checks are enforced in `ExecuteThroughProviderAsync(...)`:
- `WaitForProviderRateLimitSlotAsync(providerName, ct)` is called before provider call execution.
- Applies consistently to both:
  - `ExecuteAsync(...)`
  - `ExecuteWithConfirmationAsync(...)`

This keeps all online execution entry points aligned on rate-limit behavior.

---

## Testing

### Unit Tests Added

**File:** `tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterRateLimitingTests.cs`

Tests:
1. `ExecuteBatchAsync_SameProvider_RateLimitedByProviderWindow`
2. `ExecuteBatchAsync_DifferentProviders_NotRateLimitedByOtherProvider`
3. `ExecuteBatchAsync_MaxRequestsPerWindowZero_DisablesRateLimiting`

Coverage:
- Positive throttling behavior for same provider.
- Isolation of limits across different providers.
- Disabled-rate-limit behavior per provider.

### Validation Run
Executed targeted OnlineProviderRouter tests, including new MQ-REQ-017 coverage.

**Result:** ✅ passing targeted suite including new rate-limiting tests.

---

## Design Decisions

### 1. Provider-Level Configuration Surface
Rate-limit settings are part of `ProviderConfig`, keeping per-provider operational controls close to other provider budgets/settings.

### 2. Shared Execution Enforcement
Rate limiting is enforced in shared execution helper so all online execution flows behave consistently.

### 3. Window-Based Limiting with Wait Semantics
When a provider is over limit, requests wait until capacity is available instead of failing immediately, preserving current request contract and minimizing caller changes.

---

## Operational Notes

### Configuration
Under each provider in `OnlineProviderOptions.Providers`:
- `RateLimitWindowSeconds`
- `MaxRequestsPerWindow`

### Runtime Behavior
- Per-provider limits are independent.
- Exceeding a provider limit delays that provider only.
- Concurrency limits (`MaxConcurrentRequestsPerProvider`) still apply separately.

---

## Files Changed

### Source
- `src/Daiv3.ModelExecution/OnlineProviderOptions.cs`
- `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`

### Tests
- `tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterRateLimitingTests.cs`

### Documentation
- `Docs/Requirements/Reqs/MQ-REQ-017.md`
- `Docs/Requirements/Reqs/MQ-REQ-017-Implementation.md`
- `Docs/Requirements/Master-Implementation-Tracker.md`
