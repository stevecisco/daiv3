# CT-ACC-003

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
Users can see token usage vs budget per provider.

## Implementation Status
**Status:** ✅ COMPLETE  
**Completion Date:** March 8, 2026

## Overview
This acceptance criterion validates that CT-REQ-007 implements complete visibility into online provider token usage and budget tracking. Users must be able to see per-provider token consumption (daily/monthly), budget limits, utilization percentages, and budget alert indicators across all configured online providers (OpenAI, Azure OpenAI, Anthropic).

**Parent Requirement:** [CT-REQ-007](CT-REQ-007.md) - Online Provider Token Usage Dashboard Display

## Acceptance Criteria

### Functional Requirements
✅ **AC-1: Users can view token usage for all configured online providers**
- Token usage displayed per provider (OpenAI, Azure OpenAI, Claude/Anthropic)
- Daily and monthly token consumption shown separately
- Input and output tokens tracked independently
- Real-time updates as tokens are consumed

✅ **AC-2: Users can see budget limits and remaining budget**
- Daily token budget limits displayed (input/output combined)
- Monthly token budget limits displayed (input/output combined)
- Remaining budget calculated and shown per period
- Budget percentages calculated as (used / limit) × 100

✅ **AC-3: Users receive budget alerts when approaching limits**
- Alert indicator shown when any provider ≥80% utilization
- Visual differentiation between:
  - OK status (< 80% utilization) - Green
  - Near Budget (80-99% utilization) - Yellow/Warning
  - Over Budget (≥100% utilization over any limit) - Red/Error
- Budget alert banner displayed in MAUI dashboard when any provider near/over budget

✅ **AC-4: Multi-provider support**
- All configured providers displayed simultaneously
- Providers without usage (0 tokens) still shown with status "OK"
- Aggregate totals calculated across all providers (daily/monthly)
- Highest utilization provider identified

✅ **AC-5: Graceful handling when no providers configured**
- Dashboard section hidden when no online providers exist
- CLI command returns "No providers configured" message
- No errors or exceptions when IOnlineProviderRouter is null

✅ **AC-6: Error resilience**
- Individual provider errors logged as warnings but don't fail entire collection
- Other providers continue to display correctly even if one provider errors
- Total collection failure returns empty OnlineProviderUsage with appropriate flags

## Test Coverage

### Unit Tests
**File:** `tests/unit/Daiv3.App.Maui.Tests/DashboardServiceTests.cs`

**Tests Added (7 total):**
1. ✅ `CollectOnlineProviderUsageAsync_NoRouter_ReturnsEmptyUsage`
   - **Validates:** AC-5 (no providers configured)
   - **Assertion:** HasOnlineProviders = false, HasActiveUsage = false, Providers empty

2. ✅ `CollectOnlineProviderUsageAsync_NoProviders_ReturnsHasOnlineProvidersFalse`
   - **Validates:** AC-5 (router exists but no providers configured)
   - **Assertion:** HasOnlineProviders = false after empty provider list

3. ✅ `CollectOnlineProviderUsageAsync_WithProviders_ReturnsUsageSummaries`
   - **Validates:** AC-1, AC-2, AC-4 (multi-provider token usage tracking)
   - **Assertion:** Correct provider count, usage values, display names mapped

4. ✅ `CollectOnlineProviderUsageAsync_WithActiveUsage_SetsFlagCorrectly`
   - **Validates:** AC-1 (active usage detection)
   - **Assertion:** HasActiveUsage = true when any tokens consumed

5. ✅ `CollectOnlineProviderUsageAsync_ProviderNearBudget_SetsBudgetAlert`
   - **Validates:** AC-3 (budget alert at 80-99% utilization)
   - **Assertion:** HasBudgetAlert = true, HighestUsagePercent ≥ 80

6. ✅ `CollectOnlineProviderUsageAsync_ProviderOverBudget_SetsBudgetAlert`
   - **Validates:** AC-3 (budget alert at ≥100% utilization)
   - **Assertion:** HasBudgetAlert = true, IsOverBudget = true, HighestUsagePercent ≥ 100

7. ✅ `CollectOnlineProviderUsageAsync_ProviderError_ContinuesOtherProviders`
   - **Validates:** AC-6 (error resilience)
   - **Assertion:** Other providers still collected when one provider throws exception

**Test Results:** All 7 tests pass (verified March 8, 2026)

### Manual Verification Checklist

#### CLI Command Test
```powershell
daiv3 dashboard online
```
**Expected Output:**
- Per-provider breakdown with daily/monthly usage
- Progress bars with color coding (Green/Yellow/Red)
- Budget remaining calculations
- Aggregate totals section
- "No providers configured" message when appropriate

#### MAUI Dashboard Test
1. Launch MAUI app → navigate to Dashboard page
2. Verify "Online Provider Token Usage" section visible/hidden appropriately
3. Execute online provider requests to consume tokens
4. Refresh dashboard and verify:
   - Token counters increment
   - Usage percentages update
   - Progress bars fill appropriately
   - Budget alert banner appears when ≥80% utilization
5. Test with multiple providers simultaneously

## Implementation Details

### Components Implemented (CT-REQ-007)
- **OnlineProviderUsage Model** - Data contract with aggregation logic
- **ProviderUsageSummary Model** - Per-provider usage with budget calculations
- **DashboardService.CollectOnlineProviderUsageAsync()** - Token usage collection
- **DashboardViewModel Properties** - MVVM bindings for UI
- **DashboardPage XAML Section** - UI with progress bars and alerts
- **CLI Command: `dashboard online`** - Console view of token usage

### Budget Calculation Formula
```csharp
// Daily usage percentage
DailyUsagePercent = (DailyInputTokens + DailyOutputTokens) / (DailyInputLimit + DailyOutputLimit) × 100

// Monthly usage percentage
MonthlyUsagePercent = (MonthlyInputTokens + MonthlyOutputTokens) / (MonthlyInputLimit + MonthlyOutputLimit) × 100

// Highest usage (for alert determination)
HighestUsagePercent = Math.Max(DailyUsagePercent, MonthlyUsagePercent)

// Budget alert triggered if:
HighestUsagePercent >= 80
```

### Display Name Mapping
| Provider Name | Display Name |
|---|---|
| `openai` | OpenAI |
| `azure-openai` | Azure OpenAI |
| `anthropic` | Claude (Anthropic) |
| (other) | (unchanged) |

## Usage and Operational Notes

### CLI Usage
```powershell
# View online provider token usage and budget status
daiv3 dashboard online
```

**Output Format:**
- Provider-by-provider breakdown
- Daily and monthly usage with progress bars
- Input/output token breakdown
- Budget remaining per period
- Color-coded status indicators

### MAUI Dashboard
- **Location:** Dashboard page → "Online Provider Token Usage" section
- **Auto-refresh:** Updates every 3 seconds (configurable)
- **Visibility:** Section automatically hidden when no providers configured
- **Budget Alerts:** Red banner at top when any provider ≥80% utilization

### Configuration
Online providers configured in `appsettings.json`:
```json
{
  "OnlineProviders": {
    "Providers": {
      "openai": {
        "ApiKey": "sk-...",
        "DailyInputTokenLimit": 10000,
        "DailyOutputTokenLimit": 5000,
        "MonthlyInputTokenLimit": 100000,
        "MonthlyOutputTokenLimit": 50000
      }
    }
  }
}
```

## Dependencies
- **CT-REQ-007:** Online provider token usage dashboard (parent requirement)
- **MQ-REQ-012:** Online provider routing and token budget management
- **IOnlineProviderRouter:** Provides `ListProvidersAsync()` and `GetTokenUsageAsync()` methods

## Related Requirements
- **ES-ACC-002:** Users can enable online providers and must see usage/budget indicators
- **ES-NFR-003:** System SHOULD provide clear visibility into online calls and token usage

## Verification Summary
✅ All acceptance criteria met  
✅ Unit tests pass (7/7)  
✅ Manual CLI testing complete  
✅ MAUI dashboard section implemented and functional  
✅ Budget alerts working at 80% threshold  
✅ Multi-provider support verified  
✅ Error resilience confirmed  

**Acceptance Test Status:** PASS
