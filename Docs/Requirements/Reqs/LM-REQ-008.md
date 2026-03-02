# LM-REQ-008

Source Spec: 9. Learning Memory - Requirements

## Requirement
Users SHALL suppress, promote, or supersede learnings.

## Implementation Summary

**Status:** ✅ Complete (100%)

### Overview
Implemented three lifecycle operations for learning management:
1. **Suppress:** Prevent learning injection by setting status to "Suppressed"
2. **Promote:** Advance learning scope through hierarchy (Skill → Agent → Project → Domain → Global)
3. **Supersede:** Mark learning as replaced by newer approach (status "Superseded")

### Architecture

**Components:**
- **Persistence Layer:** `LearningStorageService.PromoteLearningAsync()` (new method)
- **CLI Layer:** Three new commands in `Daiv3.App.Cli/Program.cs`
  - `learning suppress --id <guid>`
  - `learning promote --id <guid>`
  - `learning supersede --id <guid>`

### Implementation Details

#### 1. Suppress Operation
**Method:** `LearningStorageService.SuppressLearningAsync(string learningId)`
- Sets `Status = "Suppressed"`
- Updates `UpdatedAt` timestamp
- Learning excluded from active learning retrieval
- Already implemented in LM-REQ-003, now exposed via CLI

**CLI Command:**
```bash
.\run-cli.bat learning suppress --id <guid>
```

#### 2. Promote Operation
**Method:** `LearningStorageService.PromoteLearningAsync(string learningId)` (NEW)
- Implements scope hierarchy: Skill → Agent → Project → Domain → Global
- Returns new scope string or null if already at Global
- Case-insensitive scope comparison
- Updates `UpdatedAt` timestamp
- Unknown scopes promoted to Global as fallback

**Scope Promotion Rules:**
- Skill → Agent
- Agent → Project
- Project → Domain
- Domain → Global
- Global → (no change, returns null)

**CLI Command:**
```bash
.\run-cli.bat learning promote --id <guid>
```

#### 3. Supersede Operation
**Method:** `LearningStorageService.SupersedeLearningAsync(string learningId)`
- Sets `Status = "Superseded"`
- Updates `UpdatedAt` timestamp
- Learning excluded from active learning retrieval
- Already implemented in LM-REQ-003, now exposed via CLI

**CLI Command:**
```bash
.\run-cli.bat learning supersede --id <guid>
```

### Error Handling
- Non-existent learning ID: Logs warning, returns gracefully (no exception)
- Already suppressed/superseded: Shows message, no change
- Already at Global scope (promote): Shows message, returns null

### User Feedback
All CLI commands provide:
- Clear confirmation messages
- Current and new state display
- Helpful context (e.g., scope hierarchy, reactivation instructions)
- Warning symbols (⚠) for no-op cases
- Success symbols (✓) for operations completed

## Testing Summary

### Unit Tests (13 tests - LearningLifecycleTests.cs)
1. ✓ SuppressLearningAsync_ActiveLearning_SetsStatusToSuppressed
2. ✓ SuppressLearningAsync_NonExistentLearning_DoesNotThrow
3. ✓ SupersedeLearningAsync_ActiveLearning_SetsStatusToSuperseded
4. ✓ SupersedeLearningAsync_NonExistentLearning_DoesNotThrow
5. ✓ PromoteLearningAsync_SkillScope_PromotesToAgent
6. ✓ PromoteLearningAsync_AgentScope_PromotesToProject
7. ✓ PromoteLearningAsync_ProjectScope_PromotesToDomain
8. ✓ PromoteLearningAsync_DomainScope_PromotesToGlobal
9. ✓ PromoteLearningAsync_GlobalScope_ReturnsNull
10. ✓ PromoteLearningAsync_NonExistentLearning_ReturnsNull
11. ✓ PromoteLearningAsync_MultipleLevels_PromotesSequentially
12. ✓ PromoteLearningAsync_UpdatesTimestamp
13. ✓ SuppressLearningAsync_UpdatesTimestamp (and SupersedeLearningAsync_UpdatesTimestamp)

### Integration Tests (6 tests - LearningManagementWorkflowTests.cs)
1. ✓ SuppressLearning_ChangesStatusAndPreventsInjection
2. ✓ SupersedeLearning_MarksAsReplaced
3. ✓ PromoteLearning_ProgressesThroughScopeHierarchy
4. ✓ PromoteLearning_FromDifferentStartingScopes
5. ✓ LifecycleOperations_MaintainDataIntegrity

### Manual Verification
- CLI command execution with valid/invalid learning IDs
- Scope promotion sequence (Skill → Global)
- Status transitions (Active → Suppressed, Active → Superseded)
- Error messages and user guidance

## Usage and Operational Notes

### When to Use Each Operation

**Suppress:**
- Temporarily disable a learning without marking it as outdated
- Prevent injection during specific project phases
- Deactivate learnings that caused issues in certain contexts
- Can be reactivated later via `learning edit --status Active`

**Promote:**
- Share task-specific insights across broader context
- Elevate agent-learned patterns to project level
- Build organizational knowledge from local improvements
- Create global standards from proven approaches

**Supersede:**
- Mark old approaches when better patterns discovered
- Replace incomplete learnings with refined versions
- Maintain historical record while preventing outdated guidance
- Track evolution of best practices

### Scope Hierarchy Context
- **Skill:** Single skill or capability (narrowest)
- **Agent:** All tasks by one agent
- **Project:** All agents in a project
- **Domain:** Multiple related projects
- **Global:** Universal applicability (broadest)

### CLI Workflow Examples
```bash
# Suppress a learning that's causing issues
.\run-cli.bat learning suppress --id abc123...

# Promote valuable insight from skill to project level
.\run-cli.bat learning promote --id xyz789...

# Supersede outdated pattern
.\run-cli.bat learning supersede --id def456...

# View result
.\run-cli.bat learning view --id abc123...
```

## Dependencies
- ✅ KM-REQ-013: Embedding model infrastructure (completed)
- 🔜 CT-REQ-003: Transparency dashboard (future integration point)

## Related Requirements
- ✅ LM-REQ-003: Learning storage (completed)
- ✅ LM-REQ-007: Learning management CLI (completed)
- 🔜 LM-ACC-003: Acceptance test for suppression (pending)

## Files Modified
1. `src/Daiv3.Persistence/LearningService.cs` - Added `PromoteLearningAsync()` method
2. `src/Daiv3.App.Cli/Program.cs` - Added 3 CLI commands + handlers
3. `tests/unit/Daiv3.UnitTests/Persistence/LearningLifecycleTests.cs` - New test file (13 tests)
4. `tests/integration/Daiv3.Persistence.IntegrationTests/LearningManagementWorkflowTests.cs` - Added 6 integration tests
5. `Docs/CLI-Command-Examples.md` - Documented suppress/promote/supersede commands
6. `Docs/Requirements/Reqs/LM-REQ-008.md` - This file (implementation documentation)
7. `Docs/Requirements/Master-Implementation-Tracker.md` - Status update

## Future Enhancements
- Bulk operations (suppress/promote multiple learnings)
- Promote with custom target scope (skip levels)
- Supersede with replacement learning link
- UI workflow for learning lifecycle management (CT-REQ-003)
- Learning promotion recommendations based on confidence/usage
