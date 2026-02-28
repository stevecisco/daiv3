# MQ-ACC-003

**Status:** ✅ COMPLETE

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
Online tasks respect token budget rules.

## Acceptance Criteria (VERIFIED)

This acceptance criterion validates that online task execution enforces provider token budgets before dispatching requests.

**Verified by:**
- **MQ-REQ-012** - Online routing with budget-aware provider selection
- **MQ-REQ-014** - Confirmation and budget-aware execution flow

### Test Coverage

**Budget Enforcement Tests:** [OnlineProviderRouterTests.cs](../../../tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterTests.cs)
1. ✅ `ExecuteAsync_ExceedingBudget_ThrowsTokenBudgetExceededException` (daily input)
2. ✅ `ExecuteAsync_ExceedingDailyOutputBudget_ThrowsTokenBudgetExceededException` (daily output)
3. ✅ `ExecuteAsync_ExceedingMonthlyInputBudget_ThrowsTokenBudgetExceededException` (monthly input)
4. ✅ `ExecuteAsync_ExceedingMonthlyOutputBudget_ThrowsTokenBudgetExceededException` (monthly output)

**Related Confirmation/Budget Tests:** [OnlineProviderRouterConfirmationTests.cs](../../../tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterConfirmationTests.cs)
1. ✅ `RequiresConfirmation_AutoWithinBudget_WithinBudget_ReturnsFalse`
2. ✅ `RequiresConfirmation_AutoWithinBudget_ExceedsBudget_ReturnsTrue`

### Implementation Summary

- `OnlineProviderRouter.CheckTokenBudgetAsync(...)` now enforces all four budget dimensions before provider execution:
	- Daily input tokens
	- Daily output tokens
	- Monthly input tokens
	- Monthly output tokens
- Provider fallback selection now uses comprehensive budget availability (`IsProviderWithinBudget`) instead of daily-only checks.
- Budget validation remains on the hot path for `ExecuteAsync(...)` and `ExecuteWithConfirmationAsync(...)` via shared execution flow.

### Verification

- **Targeted tests:** 84 passing, 0 failing (OnlineProviderRouter suites)
- **Full-suite note:** workspace-wide test run includes unrelated integration fixture failures in Foundry Local tests (`FoundryLocalManager has already been created`), not introduced by MQ-ACC-003 changes.

## Implementation Plan
- Complete.

## Testing Plan
- Automated tests implemented and passing for budget rule enforcement.

## Usage and Operational Notes
- Configure provider budgets in `OnlineProviderOptions.Providers[*]`:
	- `DailyInputTokenLimit`, `DailyOutputTokenLimit`
	- `MonthlyInputTokenLimit`, `MonthlyOutputTokenLimit`
- Requests that exceed configured limits throw `TokenBudgetExceededException` and are not dispatched.
- Confirmation mode `AutoWithinBudget` prompts for user confirmation only when a request exceeds remaining daily budget.

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- MQ-REQ-012
- MQ-REQ-014
