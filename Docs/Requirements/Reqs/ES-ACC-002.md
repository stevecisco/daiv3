# ES-ACC-002

Source Spec: 1. Executive Summary - Requirements

## Requirement
Users can enable online providers and must see usage and budget indicators.

**Extended Requirements (implemented March 8, 2026):**
1. Users can systematically enable/disable individual online providers (OpenAI, Azure OpenAI, Anthropic)
2. System displays real-time token usage and budget status for all enabled providers
3. Usage indicators show daily and monthly consumption with budget limits
4. Budget alerts appear when any provider reaches ≥80% utilization
5. Both CLI and MAUI interfaces support provider configuration and usage viewing

## Implementation Status
**Status:** IN PROGRESS  
**Completion Target:** March 8, 2026

## Architecture & Design

### Underlying Components (Prerequisites)

**ES-REQ-002: Online Access Policy**
- Location: `src/Daiv3.Persistence/Services/OnlineAccessPolicyService.cs`
- Provides: `IsOnlineAccessAllowedAsync()`, access mode enforcement, provider enablement validation
- Status: **✅ COMPLETE** - Policy enforcement ready

**CT-REQ-007: Token Usage Dashboard**
- Location: `src/Daiv3.App.Maui/Services/DashboardService.cs`
- Provides: `CollectOnlineProviderUsageAsync()`, real-time usage collection, budget calculations
- Components: `OnlineProviderUsage` model, `ProviderUsageSummary` model, dashboard XAML bindings
- Status: **✅ COMPLETE** - Dashboard display ready

**CT-REQ-003: Real-Time Transparency Dashboard**
- Location: `src/Daiv3.App.Maui/Pages/DashboardPage.xaml`
- Provides: MVVM ViewModel, monitoring loop, async data binding
- Status: **✅ COMPLETE** - Infrastructure ready

### Acceptance Test Scenarios

#### AC1: Enable Online Provider (CLI)
**Command:** `daiv3 config set online_providers_enabled '["openai"]'`
- Provider OpenAI should be marked enabled in settings
- System should allow online routing to OpenAI
- Verifiable via: `daiv3 config get online_providers_enabled`

#### AC2: Enable Multiple Providers (CLI)
**Command:** `daiv3 config set online_providers_enabled '["openai","azure-openai","anthropic"]'`
- All three providers should be enabled
- System should route to any available provider matching request profile
- Verifiable via: `daiv3 config get online_providers_enabled`

#### AC3: Enable Online Access Mode (CLI)
**Command:** `daiv3 config set online_access_mode 'auto_within_budget'`
- System should allow online access when cost within budget
- Policy service returns `IsAllowed=true` for valid requests
- Verifiable via: Settings UI or CLI display

#### AC4: View Online Provider Usage (MAUI Dashboard)
**Action:** Open Dashboard page in MAUI UI
- Section "Online Provider Token Usage" displays
- Shows provider name (OpenAI, Azure OpenAI, Claude)
- Shows daily and monthly token usage
- Shows budget limits and remaining tokens
- Shows usage percentage (daily/monthly)
- Verifiable via: MAUI Dashboard visual inspection

#### AC5: Budget Alert Display (MAUI Dashboard)
**Precondition:** Provider with ≥80% utilization
- Orange or red alert badge displays on provider card
- Alert banner shows at top of provider section
- Indicates "Near Budget" or "Over Budget" status
- Links to Settings for budget adjustment
- Verifiable via: MAUI Dashboard visual inspection

#### AC6: Usage Indicators Update in Real-Time (MAUI Dashboard)
**Action:** Consume tokens via online provider, observe dashboard
- Dashboard refreshes every 3 seconds (default)
- Token counts update without page reload
- Usage percentages recalculate
- Budget alerts appear/disappear as conditions change
- Verifiable via: Manual testing with test API calls

#### AC7: CLI Dashboard Command (CLI)
**Command:** `daiv3 dashboard online`
- Displays table of online providers
- Shows provider name, daily tokens, monthly tokens, budget limits
- Shows usage percentages and budget status
- Colored status indicators (✓ OK, ⚠ Near, ✗ Over)
- Verifiable via: CLI console output

#### AC8: No Providers Configured Graceful Degradation (MAUI Dashboard)
**Precondition:** `online_providers_enabled = "[]"`
- Dashboard loads without errors
- Online provider section hidden or shows "Not configured" message
- No null reference or exception errors
- Verifiable via: MAUI UI stability

#### AC9: Offline Mode Suppresses Online Indicators (MAUI Dashboard)
**Precondition:** `force_offline_mode = true`
- Dashboard displays but online provider section hidden
- System logs indicate offline mode active
- No online requests attempted
- Verifiable via: MAUI UI and log files

## Testing Plan

### Unit Tests (Existing - No New Required)
**Covered by prerequisites:**
- OnlineAccessPolicyServiceTests: 16 tests - policy enforcement ✅
- DashboardServiceTests: 24+ tests - usage collection ✅

### Integration Tests (New - ES-ACC-002 Specific)

**OnlineProviderAcceptanceTests.cs** (`tests/integration/Daiv3.Persistence.IntegrationTests/`)
```csharp
[Collection("Sequential")]
public class OnlineProviderAcceptanceTests
{
    // AC1: Enable single provider
    [Fact]
    public async Task EnableSingleProvider_ConfiguresAndRoutesCorrectly()
    
    // AC2: Enable multiple providers
    [Fact]
    public async Task EnableMultipleProviders_AllRoutable()
    
    // AC3: Online access mode enforcement
    [Fact]
    public async Task OnlineAccessMode_AutoWithinBudget_AllowsRouting()
    
    // AC8: Graceful degradation
    [Fact]
    public async Task NoProvidersConfigured_DashboardStable()
    
    // AC9: Offline mode override
    [Fact]
    public async Task ForceOfflineMode_SupressesOnlineRouting()
    
    // Full workflow: Enable -> Use -> Check Budget
    [Fact]
    public async Task EndToEnd_EnableProviders_UseTokens_VerifyBudgetDisplay()
}
```

**Expected Results:**
- All 6 integration tests passing
- Tests run on both `net10.0` and `net10.0-windows10.0.26100` TFM
- No new build errors introduced
- Full suite passes: `dotnet test Daiv3.FoundryLocal.slnx --nologo`

### Manual Verification Checklist (MAUI UI)

**Setup:**
- [ ] Run `daiv3 config set online_providers_enabled '["openai"]'` (requires valid OpenAI key)
- [ ] Run `daiv3 config set online_access_mode 'auto_within_budget'`
- [ ] Set token budget: `daiv3 config set openai_budget_daily 10000`
- [ ] Launch MAUI: `.\run-maui.bat`

**Verification:**
- [ ] Dashboard page loads without errors
- [ ] "Online Provider Token Usage" section visible
- [ ] OpenAI provider card shows (provider name, tokens, budget)
- [ ] Usage percentages calculated correctly (current/budget * 100)
- [ ] No null reference errors in logs (`%LOCALAPPDATA%\Daiv3\logs\`)
- [ ] Refresh works by navigating away and back to Dashboard
- [ ] Settings changes (budget/mode) take effect immediately

**Offline Test:**
- [ ] Run `daiv3 config set force_offline_mode true`
- [ ] Restart MAUI
- [ ] Online provider section hidden or shows "offline mode" indicator
- [ ] No budget alerts display
- [ ] System remains stable

### CLI Verification Checklist

**Commands to Test:**
```powershell
# Set up providers
daiv3 config set online_providers_enabled '["openai","anthropic"]'
daiv3 config set online_access_mode 'auto_within_budget'

# Verify configuration
daiv3 config get online_providers_enabled
daiv3 config get online_access_mode

# View dashboard
daiv3 dashboard online

# Test offline mode
daiv3 config set force_offline_mode true
daiv3 config get force_offline_mode
```

**Expected Output:**
- Configuration commands return current settings
- Dashboard shows provider list with usage and budgets
- Offline mode toggle works without errors

## Usage and Operational Notes

### How to Enable Online Providers

#### Via CLI (Recommended for Scripting)
```powershell
# Set online access mode
daiv3 config set online_access_mode 'auto_within_budget'  # or 'ask', 'never', 'per_task'

# Enable specific providers (JSON array)
daiv3 config set online_providers_enabled '["openai","azure-openai","anthropic"]'

# Set provider budgets (tokens)
daiv3 config set openai_budget_daily 50000
daiv3 config set azure_openai_budget_monthly 1000000

# Verify
daiv3 config get online_providers_enabled
```

#### Via MAUI UI (Recommended for End Users)
1. Open Settings page
2. Navigate to "Providers" section
3. Toggle each provider on/off
4. Set budget limits (daily/monthly)
5. Choose access mode (Never, Ask, Auto Within Budget, Per-Task)
6. Click "Save" - changes take effect immediately

### How to View Usage Indicators

#### Via MAUI Dashboard
1. Open Dashboard page (default on app launch)
2. Scroll to "Online Provider Token Usage" section
3. View:
   - Provider name (OpenAI, Azure OpenAI, Claude)
   - Daily tokens consumed / daily budget limit
   - Monthly tokens consumed / monthly budget limit
   - Usage percentages (colored: green <50%, yellow 50-80%, red >80%)
   - Budget status badge (✓ OK, ⚠ Near Budget, ✗ Over Budget)
4. Dashboard auto-refreshes every 3 seconds

#### Via CLI Dashboard Command
```powershell
daiv3 dashboard online

# Output example:
# Provider          Daily Tokens  Daily Budget  Daily %   Monthly Tokens  Monthly Budget  Monthly %  Status
# ─────────────────────────────────────────────────────────────────────────────────────────────────────
# OpenAI            12,345        50,000        24.7%     450,000         1,000,000       45.0%      ✓ OK
# Azure OpenAI      5,678         100,000       5.7%      95,000          2,000,000       4.8%       ✓ OK
# Claude            0             25,000        0.0%      0               500,000         0.0%       ✓ OK
```

### User-Visible Effects

| Action | Effect | Observability |
|--------|--------|---|
| Enable provider | System allows routing to that provider | Settings UI reflects change, routing works in chat |
| Reach 80% budget | Alert badge appears (⚠ Near Budget) | Dashboard shows orange/yellow warning |
| Exceed budget | Alert badge (✗ Over Budget), may trigger confirmation | Dashboard shows red alert, policy may deny auto-routing |
| Disable provider | System no longer routes to that provider | Settings UI reflects change, no more usage for that provider |
| Set offline mode | All online providers suppressed | Dashboard section hidden, logs show "offline mode active" |
| Change budget | Dashboard updates immediately | Percentages and alerts recalculate without restart |

### Operational Constraints

**Prerequisites:**
- CT-REQ-001, CT-REQ-002 (Settings infrastructure for configuration storage)
- ES-REQ-002 (Online access policy for enforcement)
- CT-REQ-003, CT-REQ-007 (Dashboard service for usage display)
- Valid API keys configured for each enabled provider

**Limitations:**
- Token usage is approximate (based on provider's Token Usage API, if available)
- Budget alerts are advisory (system may allow over-budget requests in exceptional cases)
- Dashboard refresh interval is configurable but minimum 1000ms recommended
- Offline mode (`force_offline_mode=true`) takes precedence over all other settings
- Currency/cost display deferred to CT-REQ-008 (future requirement)

**Performance:**
- Dashboard data collection: ~200-500ms per refresh (network calls to each provider)
- Dashboard refresh cycle: 3 seconds (configurable via `DashboardConfiguration.RefreshIntervalMs`)
- Usage indicator updates: <100ms after collection completes
- No impact on model execution latency (asynchronous monitoring)

## Dependencies
- ES-REQ-002: Online Access Policy (configuration and enforcement)
- CT-REQ-003: Real-Time Transparency Dashboard (infrastructure)
- CT-REQ-007: Online Token Usage Display (dashboard display component)
- CT-REQ-001: Settings Infrastructure (configuration storage) [implicit via ES-REQ-002]
- CT-REQ-002: Settings Versioning (configuration history) [implicit via ES-REQ-002]

## Related Requirements
- CT-ACC-001: Settings can be configured via UI or CLI (covers "enable providers" part)
- CT-ACC-003: Dashboard displays real-time system status (covers "see indicators" part)
- ES-REQ-001: Local-first routing (integration point for policy checks)
- CT-REQ-008: Cost tracking dashboard (future - currency/cost display)
