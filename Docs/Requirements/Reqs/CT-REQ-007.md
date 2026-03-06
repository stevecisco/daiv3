# CT-REQ-007

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display online token usage and budget status.

## Implementation Status
**Status:** ✅ COMPLETE  
**Completion Date:** March 6, 2026

## Architecture Overview

### Core Components
1. **OnlineProviderUsage Model** - Data contract for provider token usage status
2. **ProviderUsageSummary Model** - Per-provider usage details with budget calculations
3. **DashboardService.CollectOnlineProviderUsageAsync** - Token usage collection logic
4. **DashboardViewModel Properties** - MVVM bindings for online provider usage
5. **DashboardPage XAML Section** - UI for online provider token usage display
6. **CLI Command: `dashboard online`** - Console view of token usage and budget status

### Design Principles
- **Real-time Monitoring:** Token usage refreshed every 3 seconds with dashboard updates
- **Budget Awareness:** Visual indicators for providers near (≥80%) or over budget
- **Multi-Provider Support:** Display usage across all configured online providers (OpenAI, Azure OpenAI, Anthropic)
- **Daily/Monthly Tracking:** Separate daily and monthly token budgets with progress visualization
- **Graceful Degradation:** No online providers configured = section hidden in UI

## Detailed Implementation

### OnlineProviderUsage Model
**Location:** `src/Daiv3.App.Maui/Models/DashboardData.cs`

**Key Properties:**
```csharp
public class OnlineProviderUsage
{
    public bool HasOnlineProviders { get; set; }
    public bool HasActiveUsage { get; set; }
    public List<ProviderUsageSummary> Providers { get; set; }
    public long TotalDailyTokens => Providers.Sum(p => p.DailyInputTokens + p.DailyOutputTokens);
    public long TotalMonthlyTokens => Providers.Sum(p => p.MonthlyInputTokens + p.MonthlyOutputTokens);
    public bool HasBudgetAlert => Providers.Any(p => p.HighestUsagePercent >= 80);
}
```

**Responsibilities:**
- Aggregate token usage across all configured providers
- Track daily and monthly totals
- Identify budget alert conditions (≥80% utilization)
- Support graceful fallback when no providers configured

### ProviderUsageSummary Model
**Location:** `src/Daiv3.App.Maui/Models/DashboardData.cs`

**Key Features:**
- **Token Tracking:** Daily/monthly input/output tokens with budget limits
- **Usage Percentages:** Automatic calculation of daily/monthly utilization
- **Budget Status:** Over Budget / Near Budget / OK classification
- **Remaining Budgets:** Calculate tokens remaining per period
- **Display Names:** User-friendly provider names (e.g., "Azure OpenAI")

### DashboardService Integration
**Location:** `src/Daiv3.App.Maui/Services/DashboardService.cs`

**Method:** `CollectOnlineProviderUsageAsync()`

**Logic:**
1. Check if IOnlineProviderRouter is available (null = no online providers)
2. List all configured providers via `ListProvidersAsync()`
3. For each provider, call `GetTokenUsageAsync()` and convert to ProviderUsageSummary
4. Map provider names to friendly display names (OpenAI, Azure OpenAI, Claude)
5. Detect active usage (any tokens consumed)
6. Return aggregated OnlineProviderUsage object

**Error Handling:**
- Provider-level errors logged as warnings, continue collection for other providers
- Total collection failure returns empty OnlineProviderUsage with flags set to false

### DashboardViewModel Properties
**Location:** `src/Daiv3.App.Maui/ViewModels/DashboardViewModel.cs`

**Properties Added:**
- `HasOnlineProviders` - Show/hide provider section
- `HasActiveUsage` - Show "no usage" message when false
- `Providers` - List of ProviderUsageSummary for CollectionView binding
- `TotalDailyTokens` / `TotalMonthlyTokens` - Aggregate totals
- `HasBudgetAlert` - Show alert banner when any provider ≥80%
- `TotalDailyTokensText` / `TotalMonthlyTokensText` - Formatted for display (K/M suffixes)

**Update Method:** `UpdateUIFromDashboardData()`
- Updates properties from `data.OnlineUsage`
- Triggers property changed notifications for formatted text properties
- Always marshaled to main thread for UI safety

### Dashboard XAML UI
**Location:** `src/Daiv3.App.Maui/Pages/DashboardPage.xaml`

**Section:** "Online Provider Token Usage (CT-REQ-007)"

**Features:**
- **Visibility:** Section hidden when `HasOnlineProviders` is false
- **Budget Alert Banner:** Red banner shown when `HasBudgetAlert` is true
- **No Usage Message:** Info message shown when no tokens consumed yet
- **Total Summary Cards:** Daily/monthly aggregates with color-coded badges
- **Per-Provider Cards:** CollectionView with detailed breakdown per provider
  - Provider name and budget status badge
  - Daily usage: tokens, percentage, progress bar
  - Monthly usage: tokens, percentage, progress bar
  - Budget remaining (daily/monthly)
  - Progress bar color: Green (OK), Yellow (≥80%), Red (≥100%)

**Required Converters:**
- `InverseBoolConverter` - Hide elements when HasActiveUsage = true
- `BoolToStrokeConverter` - Border stroke color based on budget alert
- `BudgetStatusColorConverter` - Badge color (Green/Yellow/Red)
- `PercentToProgressConverter` - Convert percentage to 0-1 for ProgressBar
- `UsagePercentColorConverter` - Progress bar color based on utilization

### CLI Command: `dashboard online`
**Location:** `src/Daiv3.App.Cli/Program.cs`

**Command:** `daiv3 dashboard online`

**Output Format:**
```
═══════════════════════════════════════════════════════════
      ONLINE PROVIDER TOKEN USAGE (CT-REQ-007)             
═══════════════════════════════════════════════════════════

CONFIGURED PROVIDERS: 3

─── OpenAI ───

  Status: OK

  Daily:
    Used:      1,234 tokens (12.3%)
    Limit:     10,000 tokens
    Remaining: 8,766 tokens
    [████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░]

  Monthly:
    Used:      45,678 tokens (45.7%)
    Limit:     100,000 tokens
    Remaining: 54,322 tokens
    [██████████████████░░░░░░░░░░░░░░░░░░░░]

  Input/Output Breakdown:
    Daily:   800 in / 434 out
    Monthly: 30,000 in / 15,678 out

  Daily Reset: 2026-03-07 00:00:00

═══════════════════════════════════════════════════════════
SUMMARY:
  Today's Total:      1.2K tokens
  This Month's Total: 45.7K tokens
  Active Usage:       Yes
  Budget Alert:       No
```

**Features:**
- Progress bars with color-coding (Green/Yellow/Red)
- Formatted token counts (K/M suffixes for large numbers)
- Per-provider detailed breakdown
- Summary section with aggregate totals
- "No providers configured" fallback messaging

## Dependencies
- **CT-REQ-003:** Dashboard foundation (DashboardService, DashboardViewModel, DashboardPage)
- **MQ-REQ-012:** Online provider routing and token budget management (IOnlineProviderRouter interface)
- **IOnlineProviderRouter:** Provides `ListProvidersAsync()` and `GetTokenUsageAsync()` methods

## Related Requirements
- **CT-ACC-003:** Users can see token usage vs budget per provider (acceptance criterion)
- **ES-ACC-002:** Users can enable online providers and must see usage/budget indicators
- **ES-NFR-003:** System SHOULD provide clear visibility into online calls and token usage

## Testing Plan

### Unit Tests
**File:** `tests/unit/Daiv3.App.Maui.Tests/Services/DashboardServiceTests.cs`

**Test Coverage:**
1. `CollectOnlineProviderUsageAsync_NoRouter_ReturnsEmptyUsage` - Verify fallback when IOnlineProviderRouter is null
2. `CollectOnlineProviderUsageAsync_NoProviders_ReturnsHasOnlineProvidersFalse` - Verify empty provider list handling
3. `CollectOnlineProviderUsageAsync_WithProviders_ReturnsUsageSummaries` - Verify provider usage collection
4. `CollectOnlineProviderUsageAsync_WithActiveUsage_SetsFlagCorrectly` - Verify HasActiveUsage detection
5. `CollectOnlineProviderUsageAsync_ProviderNearBudget_SetsBudgetAlert` - Verify budget alert (≥80%)
6. `CollectOnlineProviderUsageAsync_ProviderOverBudget_SetsBudgetAlert` - Verify budget alert (≥100%)
7. `CollectOnlineProviderUsageAsync_ProviderError_ContinuesOtherProviders` - Verify error resilience

### Integration Tests
**Tests rely on existing OnlineProviderRouter tests:**
- Token usage accumulation verified in `OnlineProviderRouterTests.ExecuteAsync_TracksTokenUsage`
- Multi-provider tracking verified in `OnlineProviderRouterTests.ExecuteAsync_DifferentProviders_TrackedSeparately`

### Manual Verification
1. **CLI Command Test:**
   ```powershell
   daiv3 dashboard online
   ```
   - Verify output format
   - Verify "no providers" fallback message

2. **MAUI Dashboard Test:**
   - Launch MAUI app → navigate to Dashboard
   - Verify "Online Provider Token Usage" section visibility
   - Execute online provider requests, refresh dashboard
   - Verify token counters increment
   - Verify budget alert banner appears when ≥80%

## Usage and Operational Notes

### CLI Usage
```powershell
# View online provider token usage and budget status
daiv3 dashboard online
```

**Output:** Per-provider breakdown with daily/monthly usage, budgets, and progress bars.

### MAUI Dashboard
- **Location:** Dashboard page → "Online Provider Token Usage" section
- **Auto-refresh:** Updates every 3 seconds along with other dashboard metrics
- **Visibility:** Section automatically hidden when no online providers configured
- **Budget Alerts:** Red banner appears when any provider ≥80% utilization

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

**Token Budget Limits:**
- **Daily:** Reset at midnight UTC
- **Monthly:** Reset on first day of month
- **Budget exceeded:** ExecuteAsync() throws `TokenBudgetExceededException`

## Implementation Details

### Display Name Mapping
```csharp
private static string GetProviderDisplayName(string providerName)
{
    return providerName?.ToLowerInvariant() switch
    {
        "openai" => "OpenAI",
        "azure-openai" => "Azure OpenAI",
        "anthropic" => "Claude (Anthropic)",
        _ => providerName ?? "Unknown"
    };
}
```

### Budget Status Classification
```csharp
public string BudgetStatus
{
    get
    {
        if (IsOverBudget) return "Over Budget";
        if (IsNearBudget) return "Near Budget"; // ≥80%
        return "OK";
    }
}
```

### Token Count Formatting
```csharp
private static string FormatTokenCount(long tokens)
{
    return tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000.0:F1}M",
        >= 1_000 => $"{tokens / 1_000.0:F1}K",
        _ => $"{tokens}"
    };
}
```

## Acceptance Criteria (CT-ACC-003)
✅ Users can see token usage vs budget per provider  
✅ Daily and monthly token usage displayed separately  
✅ Budget utilization shown as percentage and progress bar  
✅ Alert indicator when any provider ≥80% utilization  
✅ Multi-provider support (OpenAI, Azure OpenAI, Anthropic)  
✅ CLI command for console-based monitoring  
✅ MAUI dashboard with auto-refresh (3 second interval)  
✅ Graceful handling when no providers configured

## Future Enhancements
- Per-provider cost estimation (tokens × rate)
- Historical usage charts (daily/monthly trends)
- Budget threshold notifications (push alerts at 80%, 90%, 95%)
- Export usage data to CSV/JSON
- Budget adjustment UI (modify limits without editing appsettings.json)
- Provider-specific rate limiting visibility (requests/minute)
