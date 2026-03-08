# ES-ACC-002

Source Spec: 1. Executive Summary - Requirements

## Requirement
Users can enable online providers and must see usage and budget indicators.

**Extended Requirements (implemented March 8, 2026):**
1. Users can systematically enable/disable individual online providers (OpenAI, Azure OpenAI, Anthropic)
2. System displays real-time token usage and budget status for all enabled providers
3. Usage indicators show daily and monthly consumption with budget limits
4. Budget alerts appear when any provider reaches >=80% utilization
5. Both CLI and MAUI interfaces support provider configuration and usage viewing

**Additional Scope Addendum (requested March 8, 2026 - pending implementation):**
6. The system SHALL support .NET 10 single-file executable C# skills (file-based scripts) that can be created and managed by the Skill Creator flow.
7. Skill metadata in markdown SHALL define invocation contract (parameters, expected inputs, outputs, and execution policy) for each executable skill file.
8. The platform SHALL generate and store tamper-detection integrity hashes for executable skill files and validate hashes before execution.
9. Executable skills SHALL require administrative approval before first execution and after any file/hash-changing update.
10. The system SHALL provide an approval/audit record for executable skill lifecycle events (create, approve, revoke, execute).
11. Future capability: skills marked `RequiresIsolatedEnvironment` SHOULD run in an isolated Docker-hosted .NET 10 runtime with restricted path/permission mapping and ephemeral container teardown.

## Implementation Status
**Status:** IN PROGRESS  
**Completion Target (Original Scope AC1-AC9):** March 8, 2026 ✅ Complete  
**Completion Target (Addendum AC10-AC16):** TBD - Sequenced implementation phases defined below

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

### Addendum Acceptance Scenarios (Pending Implementation)

#### AC10: Create Executable .NET 10 Skill File
**Action:** Use Skill Creator to scaffold executable skill files
- Skill creator produces at least one runnable `.cs` skill file with argument parsing entry point
- Skill metadata markdown is generated/updated with parameter contract and usage example
- Verifiable via: file generation in configured skill path and metadata lint validation

#### AC11: Execute File-Based Skill via Skill Runtime
**Action:** Invoke an approved file-based skill with parameters
- Runtime resolves skill metadata, validates parameter contract, and launches file-based execution
- Skill receives expected input parameters and returns structured output
- Verifiable via: execution logs and output payload contract checks

#### AC12: Skill Hash Integrity Validation
**Action:** Modify executable skill file after approval, then execute
- Runtime compares stored hash with current file hash before execution
- Execution is blocked when hash mismatch is detected
- System reports integrity violation with actionable remediation guidance
- Verifiable via: security log and denied execution result

#### AC13: Administrative Approval Gate
**Action:** Attempt to run non-approved executable skill
- Runtime denies execution until admin approval is granted
- Approval status is persisted and queryable from CLI/MAUI admin surface
- Verifiable via: denied run prior to approval, successful run after approval

#### AC14: Re-Approval on Skill Update
**Action:** Update executable skill file or metadata contract
- Existing approval is automatically revoked or marked stale
- Skill returns to `PendingApproval` state until admin re-approves
- Verifiable via: lifecycle state transitions and audit log entries

#### AC15: Audit Trail Coverage
**Action:** Create, approve, execute, revoke executable skill
- Audit trail records actor, timestamp, operation, result, and hash/version metadata
- Audit entries are retrievable for compliance review
- Verifiable via: CLI/API query of audit records

#### AC16: Future - Isolated Execution Policy
**Action:** Mark skill `RequiresIsolatedEnvironment` when Docker runtime is available
- Runtime schedules execution in ephemeral .NET 10 container with explicit read/write path mapping
- Container is stopped and removed after execution
- If Docker unavailable, system fails safely with clear operator guidance
- Verifiable via: runtime logs and container lifecycle telemetry

## Implementation Sequence (Addendum Scope AC10-AC16)

### Phase 1: Foundation - Data Model + Hash Service

**Deliverables:**
1. `ExecutableSkill` entity with fields: `Id`, `Name`, `FilePath`, `FileHash`, `MetadataPath`, `ApprovalStatus`, `CreatedBy`, `CreatedAt`, `ApprovedBy`, `ApprovedAt`, `LastModifiedAt`
2. `ISkillHashService` interface with `ComputeHashAsync(filePath)`, `ValidateHashAsync(skill)`, `UpdateHashAsync(skill)`
3. `SkillHashService` implementation using SHA256 hash of file contents
4. Database migration for `executable_skills` table (via `Daiv3.Persistence` schema migrations)

**Affected Projects:**
- `src/Daiv3.Core/` - Add `ExecutableSkill` domain model, `ApprovalStatus` enum (`PendingApproval`, `Approved`, `Revoked`, `Stale`)
- `src/Daiv3.Orchestration/` - Add `ISkillHashService` interface
- `src/Daiv3.Orchestration/Services/` - Add `SkillHashService` implementation
- `src/Daiv3.Persistence/Migrations/` - Add `executable_skills` table schema

**Test Coverage:**
- Unit tests: `SkillHashServiceTests` (10+ tests)
  - Stable hash for same content
  - Different hash for modified content
  - Hash validation passes for matching hash
  - Hash validation fails for mismatched hash
  - Async file read edge cases (locked files, missing files)
- Integration tests: `ExecutableSkillRepositoryTests` (5+ tests)
  - CRUD operations on executable_skills table
  - Query by approval status
  - Query by name/path

**Acceptance Gate:**
- [ ] All unit tests passing (project-scoped)
- [ ] Database migration applies cleanly (up/down)
- [ ] `ExecutableSkill` entity retrievable via repository
- [ ] Hash service validates known test file integrity

**Mapped Acceptance Criteria:** AC12 (partial - hash validation foundation)

---

### Phase 2: Approval Workflow + Authorization

**Deliverables:**
1. `IExecutableSkillApprovalService` interface with `RequestApprovalAsync(skillId, requestorId)`, `ApproveSkillAsync(skillId, approverAdminId)`, `RevokeApprovalAsync(skillId, adminId)`, `GetApprovalStatusAsync(skillId)`
2. `ExecutableSkillApprovalService` implementation with admin role validation
3. Authorization rules: only principals with `SkillAdministrator` role can approve/revoke
4. State transitions: `PendingApproval` → `Approved`, `Approved` → `Revoked`, file-change triggers `Approved` → `Stale` → `PendingApproval`

**Affected Projects:**
- `src/Daiv3.Orchestration/Services/` - Add `ExecutableSkillApprovalService`
- `src/Daiv3.Core/` - Add `SkillApprovalRequest` model, admin role constants
- `src/Daiv3.Persistence/Repositories/` - Extend `IExecutableSkillRepository` with approval methods

**Test Coverage:**
- Unit tests: `ExecutableSkillApprovalServiceTests` (12+ tests)
  - Non-admin cannot approve skill
  - Admin can approve pending skill
  - Cannot approve already-approved skill (idempotent/no-op)
  - Revoking approved skill transitions to Revoked
  - File hash change invalidates approval (Approved → Stale)
  - Stale skill requires re-approval
- Integration tests: `ExecutableSkillApprovalWorkflowTests` (5+ tests)
  - End-to-end: create skill → request approval → admin approve → validate status persisted
  - End-to-end: approve → modify file → detect stale → deny execution

**Acceptance Gate:**
- [ ] All unit tests passing
- [ ] Non-admin approval attempt is denied with clear error
- [ ] Admin approval workflow completes and persists status
- [ ] File modification triggers stale detection

**Mapped Acceptance Criteria:** AC13 (admin approval gate), AC14 (re-approval on update)

---

### Phase 3: Runtime Enforcement + Execution

**Deliverables:**
1. `IExecutableSkillRunner` interface with `ExecuteAsync(skillId, parameters)`, `ValidateBeforeExecutionAsync(skillId)`
2. `ExecutableSkillRunner` implementation:
   - Pre-execution checks: approval status = Approved, hash validation passes
   - Execution via `dotnet run` on single `.cs` file with parameter binding
   - Capture stdout/stderr and parse structured output (JSON)
   - Return `SkillExecutionResult` with success/failure, output, logs
3. Integration with `SkillCatalog` to register executable skills from metadata

**Affected Projects:**
- `src/Daiv3.Orchestration/Skills/` - Add `ExecutableSkillRunner`
- `src/Daiv3.Orchestration/Skills/` - Extend `SkillCatalog` to include executable skill lookup
- `src/Daiv3.App.Cli/Commands/` - Add `skill execute` command for manual testing

**Test Coverage:**
- Unit tests: `ExecutableSkillRunnerTests` (10+ tests)
  - Deny execution if skill not approved
  - Deny execution if hash validation fails
  - Allow execution if approved + hash valid
  - Parameter binding: map CLI args to skill entry point
  - Output parsing: JSON stdout → `SkillExecutionResult`
  - Stderr capture for error diagnostics
- Integration tests: `ExecutableSkillExecutionTests` (8+ tests)
  - Execute simple "Hello World" skill with parameters
  - Execute skill that reads file (verify input path accessible)
  - Execute skill that writes output (verify output captured)
  - Execute tampered skill → denied with integrity error
  - Execute non-approved skill → denied with approval error

**Acceptance Gate:**
- [ ] All unit tests passing
- [ ] Integration tests pass with real `.cs` skill files
- [ ] CLI `daiv3 skill execute <name> --param key=value` works end-to-end
- [ ] Denied executions log structured error with remediation steps

**Mapped Acceptance Criteria:** AC10 (partial - runtime execution), AC11 (skill execution), AC12 (hash enforcement)

---

### Phase 4: Skill Creator Integration + Audit Trail

**Deliverables:**
1. Extend `SkillCreator` skill to scaffold executable skill files:
   - Generate `.cs` file with argument parsing entry point (`args[]` handling)
   - Generate/update skill metadata `.md` with parameter contract (YAML frontmatter)
   - Automatically compute and store initial hash
   - Auto-create approval request record
2. `ISkillAuditService` interface with `LogEventAsync(skillId, eventType, actorId, metadata)`
3. `SkillAuditService` implementation persisting to `skill_audit_log` table
4. Audit event types: `Created`, `ApprovalRequested`, `Approved`, `Revoked`, `Executed`, `ExecutionDenied`, `HashMismatch`, `FileModified`

**Affected Projects:**
- `skills/global/SkillCreator.md` - Extend with executable skill scaffolding workflow
- `src/Daiv3.Orchestration/Skills/` - Extend skill creator logic (if code-based) or CLI integration
- `src/Daiv3.Persistence/Repositories/` - Add `ISkillAuditRepository`, `SkillAuditLogRepository`
- `src/Daiv3.Persistence/Migrations/` - Add `skill_audit_log` table
- `src/Daiv3.App.Cli/Commands/` - Add `skill audit` command to query audit trail

**Test Coverage:**
- Unit tests: `SkillCreatorExecutableTests` (8+ tests)
  - Scaffold generates valid `.cs` file
  - Scaffold generates matching `.md` metadata
  - Initial hash is computed and stored
  - Approval request is auto-created
- Unit tests: `SkillAuditServiceTests` (6+ tests)
  - Audit log entries persist with correct schema
  - Query by skill ID returns full lifecycle
  - Query by event type filters correctly
- Integration tests: `SkillCreatorEndToEndTests` (4+ tests)
  - Create executable skill → verify all artifacts on disk
  - Approve created skill → verify audit trail has Create + Approve events
  - Execute approved skill → verify audit trail has Executed event
  - Modify skill file → verify audit trail has FileModified + hash stale events

**Acceptance Gate:**
- [ ] All unit tests passing
- [ ] Skill creator produces valid executable skill + metadata + hash + audit record
- [ ] CLI `daiv3 skill audit <name>` displays full lifecycle
- [ ] Audit log queryable by skill, event type, date range

**Mapped Acceptance Criteria:** AC10 (skill creation), AC15 (audit trail)

---

### Phase 5: Docker Isolation Policy (Future-Ready Stub)

**Deliverables:**
1. `RequiresIsolatedEnvironment` flag in skill metadata (YAML frontmatter)
2. Docker detection service: `IDockerRuntimeService.IsDockerAvailableAsync()`
3. Execution policy enforcement in `ExecutableSkillRunner`:
   - If `RequiresIsolatedEnvironment=true` and Docker unavailable → fail with clear error
   - If Docker available → defer to stub implementation (logs "isolated execution not yet implemented")
4. Future extension point: `IIsolatedSkillExecutor` interface definition (not implemented)

**Affected Projects:**
- `src/Daiv3.Core/` - Add `RequiresIsolatedEnvironment` property to skill metadata model
- `src/Daiv3.Infrastructure.Shared/` - Add `IDockerRuntimeService`, `DockerRuntimeService` (detection only)
- `src/Daiv3.Orchestration/Skills/` - Extend `ExecutableSkillRunner` with isolation policy check
- `src/Daiv3.Orchestration/Skills/` - Define `IIsolatedSkillExecutor` interface (stub)

**Test Coverage:**
- Unit tests: `DockerRuntimeServiceTests` (4+ tests)
  - Detect Docker available (mocked `docker --version` success)
  - Detect Docker unavailable (command fails)
  - Docker detection timeout handling
- Unit tests: `ExecutableSkillRunnerIsolationTests` (6+ tests)
  - Skill with `RequiresIsolatedEnvironment=false` executes normally
  - Skill with `RequiresIsolatedEnvironment=true` + Docker unavailable → fails with clear error
  - Skill with `RequiresIsolatedEnvironment=true` + Docker available → logs stub message (not implemented)

**Acceptance Gate:**
- [ ] All unit tests passing
- [ ] Docker detection service works on Windows 11
- [ ] Execution policy enforces Docker requirement
- [ ] Error message provides actionable remediation (install Docker, or set flag to false)
- [ ] Stub interface defined for future implementation

**Mapped Acceptance Criteria:** AC16 (isolated execution policy stub)

---

### Implementation Summary

**Sequential Dependency Flow:**
```
Phase 1 (Foundation) → Phase 2 (Approval) → Phase 3 (Execution) → Phase 4 (Audit) → Phase 5 (Docker Stub)
```

**Estimated Effort:**
- Phase 1: 8-13 story points (data model + crypto hashing + migrations)
- Phase 2: 5-8 story points (approval workflow + state machine)
- Phase 3: 13-21 story points (runtime execution + process management + error handling)
- Phase 4: 8-13 story points (skill creator extension + audit infrastructure)
- Phase 5: 3-5 story points (detection stub + policy gate)

**Total Estimate:** 37-60 story points (~2-3 sprints if dedicated)

**Critical Path Projects:**
1. `Daiv3.Core` (domain models)
2. `Daiv3.Persistence` (schema + repositories)
3. `Daiv3.Orchestration` (services + runtime)
4. `Daiv3.App.Cli` (user-facing commands)
5. Test projects: `Daiv3.Orchestration.Tests`, `Daiv3.Orchestration.IntegrationTests`

**Non-Goals (Explicitly Out of Scope):**
- Full Docker container orchestration (Phase 5 is detection + policy stub only)
- Skill marketplace/distribution (covered by FUT-REQ-003)
- Multi-language skill support (Python, JavaScript) - .NET 10 C# only
- Remote skill execution or distributed compute
- Skill versioning beyond hash integrity (no semantic versioning yet)

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

### Addendum Test Plan (Executable Skills)

**Unit Tests (planned):**
- Skill metadata contract parser validation (required parameters, types, defaults)
- Hash generation and verification service tests (stable hash, mismatch detection)
- Approval policy tests (deny unapproved, allow approved, invalidate on update)
- Runtime argument-binding and output-shaping tests for file-based skills

**Integration Tests (planned):**
- End-to-end: create file-based skill -> approve -> execute -> verify output and audit trail
- Tamper test: edit skill file post-approval -> verify blocked execution and re-approval requirement
- Policy test: mark skill `RequiresIsolatedEnvironment` and verify Docker gating behavior

**Security/Compliance Verification (planned):**
- Verify audit entries exist for create, approve, execute, deny, revoke
- Verify hash validation is executed on every run path (including retries)
- Verify non-admin principals cannot approve executable skills

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
- ES-REQ-005: Modular runtime skills loading (foundation for executable skill registration)
- AST-ACC-001: Add skill without app rebuild (runtime extension behavior)

## Related Requirements
- CT-ACC-001: Settings can be configured via UI or CLI (covers "enable providers" part)
- CT-ACC-003: Dashboard displays real-time system status (covers "see indicators" part)
- ES-REQ-001: Local-first routing (integration point for policy checks)
- CT-REQ-008: Cost tracking dashboard (future - currency/cost display)
- FUT-REQ-003: Future skill marketplace trust/review model alignment
- FUT-ACC-001: Deferred extension points for isolated skill execution
