# CT-REQ-002

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The settings UI SHALL configure watched directories, model preferences, token budgets, online access rules, agents, skills, scheduling, knowledge paths, and skill marketplace sources.

## Implementation Status
**Status:** Complete  
**Build:** ✅ MAUI project builds (`dotnet build src/Daiv3.App.Maui/Daiv3.App.Maui.csproj --nologo --verbosity minimal`)  
**Unit Tests:** ✅ `Daiv3.App.Maui.Tests` passing (`145/145`)  
**Full Suite Regression Gate:** ✅ Passed (`dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`)

## Implementation Summary

### MAUI Settings ViewModel Expansion (`src/Daiv3.App.Maui/ViewModels/SettingsViewModel.cs`)
Implemented full CT-REQ-002 settings coverage in `SettingsViewModel` with load/save wiring to `ISettingsService`:

- **Watched directories and knowledge paths**
  - `WatchedDirectories` (line-delimited UI, JSON array persistence)
  - `KnowledgeBackPropagationPath`
- **Model preferences**
  - `DefaultModel`, `ChatModel`, `CodeModel`, `ReasoningModel`
- **Online access rules and token budgets**
  - `AllowOnlineProviders`, `OnlineAccessMode`, `OnlineProvidersEnabled`
  - `TokenBudget` (daily), `MonthlyTokenBudget`, `TokenBudgetAlertThreshold`, `TokenBudgetMode`
- **Agents / skills / scheduling**
  - `EnableAgents`, `EnableSkills`
  - `AgentIterationLimit`, `AgentTokenBudget`
  - `EnableScheduling`, `SchedulerCheckInterval`
  - `SkillMarketplaceUrls` (line-delimited UI, JSON array persistence)

Additional implementation details:
- Added robust JSON conversion helpers for multi-line and comma-separated UI inputs.
- Normalized theme values between picker display (`Light/Dark/System`) and storage values (`light/dark/system`).
- Preserved existing settings initialization + reset flow (`ISettingsInitializer`).
- Kept hardware settings and existing path/model-directory behavior intact.

### Settings UI Expansion (`src/Daiv3.App.Maui/Pages/SettingsPage.xaml`)
Updated the MAUI Settings page to expose all CT-REQ-002 configuration areas:

- **Directories**: data/models/foundry paths, watched directories editor, knowledge back-propagation path
- **Model Preferences**: default/chat/code/reasoning model entries
- **Hardware Preferences**: NPU/GPU toggles
- **Online Access & Budget Rules**: online mode picker, enabled providers, daily/monthly budgets, alert threshold, budget mode picker
- **Agents, Skills, Scheduling**: enable toggles, iteration/token/scheduler numeric inputs, marketplace URLs editor
- **Appearance**: existing theme picker retained

## Testing & Validation

### Targeted Tests (Requirement-Scoped)
- Command:
  - `dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal`
- Result:
  - ✅ Passed: `145`
  - ❌ Failed: `0`

### Build Validation
- Command:
  - `dotnet build src/Daiv3.App.Maui/Daiv3.App.Maui.csproj --nologo --verbosity minimal`
- Result:
  - ✅ Build succeeded

### Full Suite Regression Gate
- Command:
  - `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Result:
  - ✅ Passed: `2101`
  - ❌ Failed: `0`
  - ⏭️ Skipped: `13`

Note: During validation, a pre-existing persistence test expectation (`MigrateToLatest_SetsSchemaVersion`) was aligned to the current migration level (schema `10`), after which the full-suite gate passed.

## Verification Checklist

- ✅ Settings UI now configures watched directories
- ✅ Settings UI now configures knowledge path
- ✅ Settings UI now configures model preferences
- ✅ Settings UI now configures token budgets
- ✅ Settings UI now configures online access rules
- ✅ Settings UI now configures agents and skills settings
- ✅ Settings UI now configures scheduling settings
- ✅ Settings UI now configures skill marketplace sources
- ✅ Requirement-scoped unit tests passing
- ✅ Full-suite regression gate passed

## Dependencies
- CT-REQ-001 ✅
- KLC-REQ-011 ✅

## Related Requirements
- CT-ACC-001 (online access rules acceptance)
- ES-REQ-002 (configurable online fallback)
- CT-NFR-002 (safe validation and application of setting changes)
