# MQ-REQ-015

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL send only the minimal required context to online providers.

## Implementation Plan
- ✅ Add `ContextMinimizationOptions` configuration class with settings for:
  - Enable/disable context minimization
  - Maximum context tokens (total and per-key)
  - Whitelist/blacklist for context keys
  - Logging preferences
- ✅ Implement `MinimizeContextForOnlineProvider()` method in `OnlineProviderRouter` that:
  - Filters context based on whitelist/blacklist
  - Truncates context values exceeding token limits
  - Logs minimization actions for transparency
  - Returns a minimized copy (original request unchanged)
- ✅ Update `ExecuteAsync()` and `ExecuteWithConfirmationAsync()` to apply context minimization before sending to providers
- ✅ Create comprehensive unit tests (13 test cases covering all scenarios)

## Testing Plan
- ✅ Unit tests to validate primary behavior and edge cases (13 tests, all passing)
- ✅ Test whitelist/blacklist filtering
- ✅ Test token limit enforcement (per-key and total)
- ✅ Test case-insensitive key matching
- ✅ Test precedence rules (whitelist over blacklist)
- ✅ Test truncation with ellipsis
- ✅ Test logging behavior
- ✅ Negative tests with empty context, disabled minimization

## Usage and Operational Notes
**Configuration:** Context minimization is enabled by default with privacy-first settings:
- **Default limits:** 2000 total tokens, 1000 per key
- **Whitelist/blacklist:** Empty by default (no keys excluded)
- **Logging:** Enabled by default for transparency

**How it works:**
1. When `ExecuteAsync()` or `ExecuteWithConfirmationAsync()` is called, the router creates a minimized copy of the request
2. Context keys are filtered based on `IncludeOnlyKeys` (whitelist) and `ExcludeKeys` (blacklist)
3. Remaining context values are truncated if they exceed `MaxTokensPerKey` or would exceed `MaxContextTokens` total
4. Minimization actions are logged (what was removed/truncated and why)
5. The minimized request is sent to the online provider (original request unchanged)

**User-visible effects:**
- Online requests send less contextual data (better privacy, lower token usage)
- Logs show what context was removed/truncated for transparency
- Configuration in `appsettings.json` under `OnlineProviderOptions:ContextMinimization`

**Operational constraints:**
- Can be disabled if full context is needed (set `Enabled = false`)
- Whitelist takes precedence over blacklist
- Truncation adds "..." to indicate content was cut
- Original request is never modified (minimized copy is created)

## Dependencies
- MQ-REQ-012 (Online provider routing)
- MQ-REQ-014 (User confirmation)

## Related Requirements
- ES-NFR-002 (Privacy: Don't transmit user documents without consent)

## Implementation Status
✅ **COMPLETE** - All code implemented, 13 unit tests passing (26 test runs across 2 frameworks)

See [MQ-REQ-015-Implementation.md](MQ-REQ-015-Implementation.md) for detailed implementation notes.
