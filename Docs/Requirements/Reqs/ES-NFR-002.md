# ES-NFR-002

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHOULD not transmit user documents to online providers unless explicitly configured or confirmed.

## Status
**Status:** Complete  
**Date Completed:** 2026-03-08

## Acceptance Criteria

| Criterion | Description | Validation |
|-----------|-------------|------------|
| AC-1: Default Guardrail | Online calls are policy-gated and not allowed unless explicitly enabled by settings | `OnlineAccessPolicyServiceTests` and `OnlineFallbackAcceptanceTests` |
| AC-2: Explicit Consent Path | When online access mode requires confirmation (`ask` / `per_task`), decision model marks confirmation as required | `OnlineAccessPolicyServiceTests` and `OnlineProviderRouterConfirmationTests` |
| AC-3: Minimal Context Transmission | Only minimized context is sent to online providers, with truncation/filtering controls | `OnlineProviderRouterContextMinimizationTests` |
| AC-4: Auditability | Minimization and policy decisions are logged/observable for transparency | `OnlineProviderRouterContextMinimizationTests` and policy service tests |

## Implementation Summary

ES-NFR-002 is satisfied by the combined behavior of online access policy enforcement and context minimization in the model execution pipeline.

### Privacy Control Stack

1. **Access policy gate before online routing**
- `src/Daiv3.Persistence/Services/OnlineAccessPolicyService.cs`
- `src/Daiv3.ModelExecution/ModelQueue.cs`
- Online dispatch requires explicit policy allowance based on user settings.

2. **Confirmation-aware online modes**
- `src/Daiv3.ModelExecution/Models/OnlineAccessDecision.cs`
- `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`
- Modes such as `ask` and `per_task` require confirmation instead of silent transmission.

3. **Context minimization before provider execution**
- `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`
- `src/Daiv3.ModelExecution/OnlineProviderOptions.cs`
- `MinimizeContextForOnlineProvider(...)` applies include/exclude filters and token budgets.

### Configuration Controls

Policy settings (explicit online enablement/consent):
- `ApplicationSettings.Providers.OnlineAccessMode`
- `ApplicationSettings.Providers.OnlineProvidersEnabled`

Context minimization settings:
- `OnlineProviderOptions.ContextMinimization.Enabled` (default `true`)
- `OnlineProviderOptions.ContextMinimization.MaxContextTokens`
- `OnlineProviderOptions.ContextMinimization.MaxTokensPerKey`
- `OnlineProviderOptions.ContextMinimization.IncludeOnlyKeys`
- `OnlineProviderOptions.ContextMinimization.ExcludeKeys`

## Testing Plan

### Requirement Traceability Coverage

- `tests/unit/Daiv3.Persistence.Tests/OnlineAccessPolicyServiceTests.cs`
	- Explicit rule enforcement for online access modes and provider enablement
- `tests/integration/Daiv3.Persistence.IntegrationTests/OnlineFallbackAcceptanceTests.cs`
	- End-to-end policy behavior from settings persistence to decision outcomes
- `tests/unit/Daiv3.ModelExecution.Tests/OnlineProviderRouterContextMinimizationTests.cs`
	- Context filtering/truncation and confirmation-path minimization coverage
- `tests/unit/Daiv3.ModelExecution.Tests/OnlineProviderRouterConfirmationTests.cs`
	- Confirmation requirement logic for online execution modes

### Validation Runs (2026-03-08)

- `dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo --verbosity minimal`
	- Result: **285 total, 0 failed, 285 passed, 0 skipped**
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --nologo --verbosity minimal`
	- Result: **292 total, 0 failed, 292 passed, 0 skipped**
- `dotnet test tests/unit/Daiv3.ModelExecution.Tests/Daiv3.ModelExecution.Tests.csproj --nologo --verbosity minimal`
	- Result: **272 total, 0 failed, 272 passed, 0 skipped**
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **2446 total, 0 failed, 2431 passed, 15 skipped**

### Warning/Build Evidence (2026-03-08)

- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **0 warnings, 0 errors**

## Usage and Operational Notes

- Without explicit online configuration, online provider execution remains disallowed.
- If online is enabled but confirmation mode is active, user confirmation is required before execution.
- When online execution is allowed, context minimization is applied by default to reduce transmitted data.
- Administrators can enforce stricter behavior with whitelist-only context keys and tighter token caps.

## Dependencies
- MQ-REQ-015

## Related Requirements
- ES-REQ-002
- MQ-REQ-014
- CT-ACC-001
