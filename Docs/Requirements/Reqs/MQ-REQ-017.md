# MQ-REQ-017

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL rate-limit requests per provider.

## Implementation Plan
- ✅ Implemented provider-scoped request-rate limiting in `OnlineProviderRouter` using per-provider sliding windows.
- ✅ Added per-provider configuration fields to `ProviderConfig`:
	- `RateLimitWindowSeconds` (default `60`)
	- `MaxRequestsPerWindow` (default `60`)
- ✅ Enforced rate limiting in shared execution path (`ExecuteThroughProviderAsync`) so behavior applies to both:
	- `ExecuteAsync(...)`
	- `ExecuteWithConfirmationAsync(...)`
- ✅ Added lock-protected provider window state to ensure thread-safe behavior under parallel batch execution.

## Testing Plan
- ✅ Added unit tests in `OnlineProviderRouterRateLimitingTests.cs`:
	- Same provider requests are delayed when exceeding configured window capacity.
	- Different providers are rate-limited independently.
	- `MaxRequestsPerWindow = 0` disables rate limiting for that provider.
- ✅ Ran targeted unit test suite for `OnlineProviderRouter` and `TaskToModelMappingConfiguration`.
- ✅ Result: passing targeted suite including new MQ-REQ-017 tests.

## Usage and Operational Notes
**How to configure:**
- Configure each provider in `OnlineProviderOptions.Providers` with:
	- `RateLimitWindowSeconds` = size of the rate-limit window
	- `MaxRequestsPerWindow` = maximum requests allowed in that window

**Operational behavior:**
- Limits are provider-scoped: one provider hitting its limit does not block other providers.
- Requests above the configured provider limit wait until a slot becomes available.
- Existing provider concurrency limits (`MaxConcurrentRequestsPerProvider`) still apply independently.
- Set `MaxRequestsPerWindow <= 0` (or `RateLimitWindowSeconds <= 0`) to disable rate limiting for a provider.

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- MQ-REQ-012 (provider routing)
- MQ-REQ-016 (cross-provider concurrent execution)

## Implementation Status
✅ **COMPLETE** - Provider-scoped request rate limiting is implemented with per-provider configurable windows and request capacity.

See [MQ-REQ-017-Implementation.md](MQ-REQ-017-Implementation.md) for detailed implementation notes.
