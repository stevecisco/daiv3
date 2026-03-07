# CT-REQ-009

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display pending knowledge promotions.

## Implementation Status
**Status:** Complete  
**Completion Date:** March 7, 2026

## Architecture Overview

### Core Components
1. `PendingKnowledgePromotionsStatus` model - aggregate pending promotion state.
2. `PendingPromotionSummary` model - per-proposal display contract.
3. `DashboardService.CollectPendingKnowledgePromotionsAsync()` - collection logic for pending proposals.
4. `DashboardViewModel` promotion properties - MVVM bindings for cards and list.
5. `DashboardPage.xaml` pending promotions section - always-visible dashboard panel with empty state + list details.

### Data Source and Boundaries
- Primary source: `IAgentPromotionProposalService.GetPendingProposalsAsync()` when available.
- Fallback source: `AgentPromotionProposalRepository.GetPendingProposalsAsync()` via scoped resolution.
- Null/empty behavior: section remains visible with `Pending = 0` and "No pending promotion proposals." message.

## Detailed Implementation

### Data Contracts
**Location:** `src/Daiv3.App.Maui/Models/DashboardData.cs`

- Added `DashboardData.PendingKnowledgePromotions`.
- Added `PendingKnowledgePromotionsStatus`:
	- `PendingCount`
	- `HighConfidenceCount` (>= 0.8)
	- `Proposals`
	- `HasPendingPromotions`
	- `OldestPendingProposal`
- Added `PendingPromotionSummary`:
	- Proposal identity (`ProposalId`, `LearningId`)
	- Provenance (`ProposingAgent`, `SourceTaskId`)
	- Scope movement (`FromScope`, `SuggestedTargetScope`)
	- Confidence + timestamps (`ConfidenceScore`, `CreatedAtUtc`)
	- Display helpers (`ConfidencePercentText`, `AgeText`)

### Service Integration
**Location:** `src/Daiv3.App.Maui/Services/DashboardService.cs`

- `CollectDashboardDataAsync()` now populates `PendingKnowledgePromotions`.
- Added `CollectPendingKnowledgePromotionsAsync()`:
	- Loads pending proposals from service/repository.
	- Converts Unix timestamps to `DateTimeOffset` for UI display.
	- Sorts newest-first for the dashboard list.
	- Returns safe empty status on failure and logs warning.

### ViewModel Integration
**Location:** `src/Daiv3.App.Maui/ViewModels/DashboardViewModel.cs`

- Added promotion properties:
	- `PendingPromotionCount`
	- `HighConfidencePromotionCount`
	- `OldestPendingPromotionAge`
	- `PendingPromotions`
	- `HasPendingPromotions`
- `UpdateUIFromDashboardData()` now maps `data.PendingKnowledgePromotions` to these bindings.

### MAUI UI
**Location:** `src/Daiv3.App.Maui/Pages/DashboardPage.xaml`

- Added new "Pending Knowledge Promotions" section with:
	- Summary cards (Pending, High Confidence, Oldest)
	- Empty-state message when none pending
	- CollectionView of pending proposals with:
		- Learning ID
		- Confidence badge
		- Scope transition (`From -> To`)
		- Age
		- Proposing agent + optional justification

## Testing Plan

### Unit Tests
**File:** `tests/unit/Daiv3.App.Maui.Tests/DashboardServiceTests.cs`

- Added `GetDashboardDataAsync_WithPromotionProposalService_ShouldPopulatePendingPromotions`.
- Added `GetDashboardDataAsync_WithoutPromotionProposalService_ShouldReturnNoPendingPromotions`.
- Existing base validation test now asserts `PendingKnowledgePromotions` is present.

**File:** `tests/unit/Daiv3.App.Maui.Tests/DashboardViewModelTests.cs`

- Added `PendingPromotionProperties_WhenSet_ShouldUpdateValues`.

### Verification Commands Run
```powershell
dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal
dotnet build src/Daiv3.App.Maui/Daiv3.App.Maui.csproj --framework net10.0-windows10.0.26100 --nologo --verbosity minimal
```

### Verification Results
- `Daiv3.App.Maui.Tests`: 200 total, 198 passed, 2 skipped, 0 failed.
- MAUI build: succeeded (no new errors introduced).
- Full-suite regression gate (`dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`): failed due to unrelated known performance-threshold tests in `Daiv3.Knowledge.Embedding.IntegrationTests.VectorSimilarityPerformanceBenchmarkTests` (2 failures, outside CT-REQ-009 scope).

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Describe how this capability is invoked or configured.
- List user-visible effects and any UI surfaces involved.
- Specify operational constraints (offline mode, budgets, permissions).

## Dependencies
- KLC-REQ-011

## Related Requirements
- None

## Usage and Operational Notes

- Pending promotions are displayed directly in the main dashboard with no additional configuration.
- High-confidence proposals are surfaced as a separate count to support faster human review triage.
- If orchestration-level proposal service is unavailable, dashboard still resolves proposals through persistence repository fallback.
