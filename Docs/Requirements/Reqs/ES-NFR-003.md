# ES-NFR-003

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHOULD provide clear visibility into any online calls and token usage.

## Status
**Status:** Complete  
**Date Completed:** 2026-03-08

## Acceptance Criteria

| Criterion | Description | Validation |
|-----------|-------------|------------|
| AC-1: Provider Visibility | Users can see configured online providers and whether they have active usage | `DashboardServiceTests.CollectOnlineProviderUsageAsync_*` |
| AC-2: Token/Budget Clarity | Daily/monthly token usage and budget percentages are visible per provider | `DashboardServiceTests` + `OnlineProviderRouterTests` token usage coverage |
| AC-3: Alerting | Near-budget and over-budget states are clearly surfaced | `DashboardServiceTests.CollectOnlineProviderUsageAsync_ProviderNearBudget_SetsBudgetAlert` and `...ProviderOverBudget_SetsBudgetAlert` |
| AC-4: User Access Paths | Visibility is available through both dashboard UI and CLI transparency/dashboard commands | `Daiv3.App.Maui.Tests` dashboard view-model/service tests and `Daiv3.App.Cli.Tests` dashboard command tests |

## Implementation Summary

ES-NFR-003 is satisfied through existing transparency and dashboard features that expose online provider token usage and budget status.

### Visibility Stack

1. **Online usage aggregation in dashboard service**
- `src/Daiv3.App.Maui/Services/DashboardService.cs`
- Collects online provider usage via `IOnlineProviderRouter` and computes budget-alert flags.

2. **Dashboard data contracts for token visibility**
- `src/Daiv3.App.Maui/Models/DashboardData.cs`
- `OnlineProviderUsage` and `ProviderUsageSummary` expose per-provider daily/monthly token usage, limits, and utilization percentages.

3. **UI surface for clear operator visibility**
- `src/Daiv3.App.Maui/Pages/DashboardPage.xaml`
- `src/Daiv3.App.Maui/ViewModels/DashboardViewModel.cs`
- Displays provider usage, progress bars, and budget alert indicators.

4. **CLI visibility for non-UI workflows**
- `src/Daiv3.App.Cli/Program.cs`
- `dashboard online` and transparency commands expose online usage and budget indicators from terminal workflows.

### Operational Behavior

- Visibility works even with zero active token usage (explicit no-usage state).
- Budget risk states are surfaced before hard budget failures.
- Works with multi-provider configurations (OpenAI, Azure OpenAI, Anthropic).
- Gracefully handles missing/unconfigured online router/providers.

## Testing Plan

### Requirement Traceability Coverage

- `tests/unit/Daiv3.App.Maui.Tests/Services/DashboardServiceTests.cs`
	- Online provider usage collection, active/no-provider behavior, and budget alert thresholds
- `tests/unit/Daiv3.ModelExecution.Tests/OnlineProviderRouterTests.cs`
	- Token usage tracking and provider-specific usage accounting used by dashboard visibility
- `tests/unit/Daiv3.App.Cli.Tests/`
	- Dashboard/transparency command execution paths for CLI visibility

### Validation Runs (2026-03-08)

- `dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal`
	- Result: **207 total, 0 failed, 205 passed, 2 skipped**
- `dotnet test tests/unit/Daiv3.ModelExecution.Tests/Daiv3.ModelExecution.Tests.csproj --nologo --verbosity minimal`
	- Result: **272 total, 0 failed, 272 passed, 0 skipped**
- `dotnet test tests/unit/Daiv3.App.Cli.Tests/Daiv3.App.Cli.Tests.csproj --nologo --verbosity minimal`
	- Result: **16 total, 0 failed, 16 passed, 0 skipped**
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **2446 total, 0 failed, 2431 passed, 15 skipped**

### Warning/Build Evidence (2026-03-08)

- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **0 warnings, 0 errors**

## Usage and Operational Notes

- MAUI Dashboard: online provider usage section shows per-provider token/budget status.
- CLI: `daiv3 dashboard online` provides token and budget visibility in terminal contexts.
- Transparency view APIs/commands include online usage context alongside queue/indexing/agent visibility.
- Online visibility reflects configured provider budgets and current usage snapshots.

## Dependencies
- CT-REQ-007

## Related Requirements
- ES-REQ-004
- CT-ACC-003
- ES-ACC-002
