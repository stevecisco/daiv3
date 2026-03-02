# KBP-ACC-001

Source Spec: 6. Knowledge Back-Propagation - Requirements

## Requirement
User can promote task learnings to project scope.

## Implementation Summary

**Status:** ✅ Complete (100%)

This acceptance criterion validates that users can promote learnings from completed tasks directly to project scope (or any target scope), making valuable knowledge available across the entire project. The implementation supports direct promotion to any scope level, not just one level at a time.

### Core Functionality Verified

#### 1. Task Learning Listing
**Method:** `LearningStorageService.GetLearningsBySourceTaskAsync(string taskId)`
- Retrieves all learnings generated during a specific task
- Returns learnings in creation order (most recent first)
- Filters by source_task_id column

#### 2. Direct Scope Promotion
**Method:** `LearningStorageService.PromoteLearningsFromTaskAsync(...)`
- Accepts `LearningPromotionSelection` with explicit TargetScope
- Supports direct promotion: Skill → Project (skipping Agent level)
- Updates learning scope directly to target (no iterative promotion)
- Records promotion in promotions table with provenance

**Implementation:** [src/Daiv3.Persistence/LearningService.cs](../../../src/Daiv3.Persistence/LearningService.cs#L343-L445)

**Key Features:**
- **Scope Hierarchy:** Skill → Agent → Project → Domain → Global
- **Direct Promotion:** Can skip intermediate levels (e.g., Skill → Project)
- **Validation:** Prevents demotion (e.g., Project → Skill is rejected)
- **Provenance:** Records source_task_id, promoted_by, notes, timestamp
- **Batch Operations:** Multiple learnings promoted in single transaction

#### 3. Promotion Validation
- Target scope must be valid: Skill, Agent, Project, Domain, Global
- Learning must exist and be from the specified task
- Cannot promote to same or lower scope level
- Returns detailed error codes: LearningNotFound, InvalidTargetScope, AlreadyAtOrBeyondTargetScope

#### 4. Promotion History Tracking
**Database:** `promotions` table (Migration 005 - KBP-DATA-001)
- promotion_id (PRIMARY KEY)
- learning_id (FOREIGN KEY → learnings)
- from_scope, to_scope
- promoted_at (Unix timestamp)
- promoted_by (user/agent identifier)
- source_task_id (task where promotion originated)
- notes (optional explanation)

## Testing Plan

### Acceptance Tests ✅ 6/6 Passing
**Location:** [tests/integration/Daiv3.Persistence.IntegrationTests/LearningPromotionAcceptanceTests.cs](../../../tests/integration/Daiv3.Persistence.IntegrationTests/LearningPromotionAcceptanceTests.cs)

**Test 1:** `AcceptanceTest_UserCanListTaskLearnings`
- Verifies user can retrieve all learnings from a completed task
- Creates 3 learnings from same task
- Confirms all are returned via GetLearningsBySourceTaskAsync

**Test 2:** `AcceptanceTest_UserCanPromoteTaskLearningsToProjectScope` ⭐ Primary Test
- Verifies direct promotion from Skill → Project scope
- Creates 2 skill-level learnings from a task
- Promotes both to "Project" scope in single operation
- Validates scope change and promotion recording
- **Key Assertion:** Learning scope changes from "Skill" to "Project"

**Test 3:** `AcceptanceTest_PromotedLearningsAvailableProjectWide`
- Verifies promoted learnings are accessible project-wide
- Learning created by "FrontendAgent" promoted to Project scope
- Confirms learning retains source_agent for provenance
- Validates learning is now available to all agents on project

**Test 4:** `AcceptanceTest_UserCanPromoteThroughMultipleScopeLevels`
- Verifies sequential promotions through scope hierarchy
- Promotes Skill → Agent, then Agent → Project
- Demonstrates incremental promotion workflow
- Each promotion recorded separately in promotions table

**Test 5:** `AcceptanceTest_UserCanSelectivelyPromoteLearnings`
- Verifies user can choose which learnings to promote
- Creates 3 learnings (high/medium/low quality)
- Promotes only high-quality learning to Project
- Others remain at original Skill scope
- **Validates:** Selective promotion, not bulk

**Test 6:** `AcceptanceTest_PromotionIncludesOptionalNotes`
- Verifies promotion notes are stored for context
- Adds detailed explanation for why learning was promoted
- Notes stored in promotions table for audit trail
- Enables future users to understand promotion rationale

### Integration Test Coverage
**Existing Tests:** [PromotionSelectionIntegrationTests.cs](../../../tests/integration/Daiv3.Persistence.IntegrationTests/PromotionSelectionIntegrationTests.cs) (5 tests)
- Batch promotion success
- Multi-task isolation
- Promotion history tracking
- Error handling (invalid IDs, invalid scopes)

### Test Results
```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

All acceptance criteria met with comprehensive test coverage.

## Usage and Operational Notes

### How It's Invoked

#### CLI Commands
**List Task Learnings:**
```bash
dotnet run --project src/Daiv3.App.Cli -- learning-promote list-from-task <task-id>
```

**Promote Learnings:**
```bash
dotnet run --project src/Daiv3.App.Cli -- learning-promote from-task <task-id> \
    --learning-ids <id1>,<id2> \
    --target-scopes Project,Project \
    --notes "Promoting to project scope for team-wide benefit"
```

#### Programmatic Usage
```csharp
var learningService = serviceProvider.GetRequiredService<ILearningStorageService>();

// Get learnings from completed task
var learnings = await learningService.GetLearningsBySourceTaskAsync(taskId);

// Select learnings to promote
var promotions = new List<LearningPromotionSelection>
{
    new()
    {
        LearningId = learnings[0].LearningId,
        TargetScope = "Project",
        Notes = "Critical pattern for database performance"
    }
};

// Execute promotion
var result = await learningService.PromoteLearningsFromTaskAsync(
    taskId,
    promotions.AsReadOnly(),
    promotedBy: "user@example.com");

// Check results
if (result.AllSucceeded)
{
    Console.WriteLine($"Promoted {result.SuccessfulPromotions.Count} learnings");
}
else
{
    foreach (var (selection, error) in result.FailedPromotions)
    {
        Console.WriteLine($"Failed: {error.ErrorCode} - {error.Message}");
    }
}
```

### User-Visible Effects

#### Learning Scope Change
- Learning's `scope` column updated to target scope
- Learning's `updated_at` timestamp refreshed
- Learning remains tied to original source_task_id and source_agent

#### Promotion History
- New row added to `promotions` table
- Records: from_scope, to_scope, promoted_by, source_task_id, notes
- Enables audit trail and reversion (future: KBP-NFR-001)

#### Future Learning Retrieval
- Promoted learnings available to queries matching new scope
- Example: Learning promoted to "Project" returned for any agent in project
- Scope filter: Global OR Project OR Agent-specific OR Skill-specific

#### Observability
- Structured logs: "Promoted learning {id} from {old} to {new}"
- Metrics collector event: OnLearningPromotedAsync (if registered)
- Future: Dashboard visibility (CT-REQ-009)

### Operational Constraints

#### Scope Hierarchy
- Cannot demote: Project → Agent promotion rejected
- Can skip levels: Skill → Project is valid
- Hierarchy: Skill → Agent → Project → Domain → Global

#### Task Association
- Learnings can only be promoted via their original source task
- Multi-task isolation: Task A learnings cannot be promoted via Task B
- Rationale: Maintains clear provenance and task boundaries

#### Permissions
- Currently: No permission checks (v0.1 single-user)
- Future: Role-based promotion (e.g., only project leads can promote to Project)

#### Performance
- Batch promotion: O(n) database updates for n learnings
- Promotion history: One INSERT per promotion
- No embedding regeneration required

#### Database Requirements
- SQLite database with migrations 004 (learnings) and 005 (promotions)
- Foreign key constraints: promotions.learning_id → learnings.learning_id
- Cascade delete: If learning deleted, promotions auto-deleted

### Error Handling

#### Validation Errors
- **LearningNotFound:** Learning ID not found from specified task
- **InvalidTargetScope:** Target scope not in valid list
- **AlreadyAtOrBeyondTargetScope:** Cannot promote to same/lower scope

#### Batch Behavior
- Partial success: Some promotions succeed, others fail
- Result object tracks successful vs failed promotions
- Failed promotions include error code and message
- Transaction per learning (not atomic across batch)

## Dependencies
- ✅ CT-REQ-009: Task completion tracking (provides task IDs)
- ✅ LM-REQ-001: Learning memory (provides learnings to promote)
- ✅ LM-REQ-003: Learning storage (GetLearningsBySourceTaskAsync, UpdateAsync)
- ✅ KBP-REQ-002: Batch promotion selection (prerequisite - provides infrastructure)
- ✅ KBP-DATA-001: Promotions table (stores promotion history)

## Related Requirements
- KBP-REQ-002: System SHALL allow users to select promotion targets (prerequisite)
- KBP-REQ-003: Agent-proposed promotions (future: agents suggest promotions)
- KBP-REQ-004: Knowledge promotion summaries (generates summary on promotion)
- KBP-ACC-002: Promotion actions recorded and visible in dashboard (future UI)
- KBP-NFR-001: Promotions SHOULD be transparent and reversible (future)
- LM-REQ-008: Suppress/promote/supersede operations (single-level promotion)

## Files Validated
- `src/Daiv3.Persistence/LearningService.cs` (PromoteLearningsFromTaskAsync, GetLearningsBySourceTaskAsync)
- `src/Daiv3.Persistence/ILearningStorageService.cs` (interface contracts)
- `src/Daiv3.Persistence/Repositories/LearningRepository.cs` (UpdateAsync)
- `src/Daiv3.Persistence/Repositories/PromotionRepository.cs` (AddAsync)
- `tests/integration/Daiv3.Persistence.IntegrationTests/LearningPromotionAcceptanceTests.cs` (6 acceptance tests)
- `src/Daiv3.App.Cli/Program.cs` (CLI command handlers)

## Acceptance Criteria Met ✅
- [x] User can list learnings from a completed task
- [x] User can promote learnings from any scope directly to Project scope
- [x] Promoted learnings are stored with updated scope
- [x] Promotion history is recorded with provenance (task ID, promoted by, timestamp)
- [x] Users can provide optional notes explaining promotion rationale
- [x] Selective promotion: Users choose which learnings to promote (not forced bulk)
- [x] Multi-level promotion: Can promote through hierarchy (Skill → Agent → Project)
- [x] Promoted learnings available project-wide (not tied to specific agent)
- [x] Validation prevents invalid operations (demotion, invalid scopes)
- [x] 6/6 comprehensive acceptance tests passing

## Implementation Notes

### Code Changes Made

#### Enhancement to PromoteLearningsFromTaskAsync
**Before:** Only promoted one level at a time (ignored TargetScope parameter)
**After:** Directly promotes to specified TargetScope

**Key Changes:**
- Removed call to `PromoteLearningAsync()` (single-level promotion)
- Added scope hierarchy validation (prevents demotion)
- Directly updates learning.Scope to normalized target scope
- Records promotion with actual from/to scopes (not one-level increment)
- Maintains backward compatibility with existing single-level promotion workflows

**File:** [src/Daiv3.Persistence/LearningService.cs](../../../src/Daiv3.Persistence/LearningService.cs#L343-L445)

### Why Two Promotion Methods Exist

**PromoteLearningAsync (single-level):**
- Used by LM-REQ-008 CLI command: `learning promote <learning-id>`
- Promotes one level at a time: Skill → Agent → Project → Domain → Global
- Simpler workflow for incremental promotion
- No target scope parameter (auto-advances one level)

**PromoteLearningsFromTaskAsync (direct to target):**
- Used by KBP-REQ-002 CLI command: `learning-promote from-task <task-id>`
- Allows direct promotion to any target scope
- Batch operation across multiple learnings
- Explicit target scope per learning in LearningPromotionSelection
- Task-scoped: Only promotes learnings from specified task

Both methods record promotion history and fire observability events.

**Status:** Complete - All acceptance criteria met, comprehensive test coverage, production-ready.
