# LM-ACC-001

Source Spec: 9. Learning Memory - Requirements

## Requirement
A corrected answer results in a new learning entry.

## Implementation Summary

**Status:** ✅ Complete (100%)

This acceptance criterion is satisfied by the LM-REQ-001 implementation of self-correction learning creation.

### Core Test
**Location:** [tests/integration/Daiv3.Orchestration.IntegrationTests/LearningServiceIntegrationTests.cs](../../../tests/integration/Daiv3.Orchestration.IntegrationTests/LearningServiceIntegrationTests.cs#L66)

**Test:** `CreateSelfCorrectionLearning_PersistsToDatabase`

### What the Test Validates
1. Creates a `SelfCorrectionTriggerContext` with:
   - Failed iteration details (output, reason)
   - Successful iteration details (output)
   - Source agent and task provenance
2. Calls `ILearningService.CreateSelfCorrectionLearningAsync(context)`
3. Verifies that a new learning entry is created and persisted to the database
4. Asserts correct metadata:
   - Unique `LearningId`
   - Title and description from context
   - `TriggerType = "SelfCorrection"`
   - Default confidence level (0.8 for self-correction)
   - Scope, tags, and provenance fields
   - Embedding generated automatically (via LM-REQ-004)

### Integration Points
- **LM-REQ-001:** Learning creation with trigger contexts (prerequisite)
- **LM-REQ-003:** Learning storage in SQLite (database persistence)
- **LM-REQ-004:** Automatic embedding generation for semantic retrieval

## Testing Plan

### Automated Tests ✅
- [x] `LearningServiceIntegrationTests.CreateSelfCorrectionLearning_PersistsToDatabase`
- [x] `LearningServiceTests.CreateSelfCorrectionLearningAsync_CreatesLearningWithCorrectTriggerType`
- [x] Database persistence verification
- [x] Metadata validation (trigger type, confidence, scope)

### Manual Verification (Future)
When AgentManager integration is complete:
- [ ] Run agent task that produces incorrect output
- [ ] Agent self-corrects and produces correct output
- [ ] Verify learning entry created automatically in `learnings` table
- [ ] Use `learning list` CLI command to view the new learning
- [ ] Verify learning can be retrieved for similar future tasks (LM-REQ-005)

## Usage and Operational Notes

### How It's Invoked
Currently: Programmatically via `ILearningService.CreateSelfCorrectionLearningAsync()`

**Future (AgentManager Integration):**
- Agent attempts a task and produces incorrect output
- Agent analyzes failure and tries again with corrected approach
- Upon success, agent automatically creates learning via `SelfCorrectionTriggerContext`
- Learning is available for retrieval in future similar tasks

### User-Visible Effects
- New entry in `learnings` table (visible via CLI)
- Learning appears in `learning list` and `learning stats` commands
- Learning can be viewed with `learning view <id>`
- Learning will be automatically injected into future agent prompts when semantically similar tasks are executed (LM-REQ-005)

### Operational Constraints
- Requires embedding model to be available (Tier 1 or Tier 2)
- Gracefully handles embedding generation failures (learning created without embedding)
- Offline mode: works fully (no external dependencies)
- No special permissions required

## Dependencies
- ✅ KM-REQ-013 (ONNX embedding generation)
- ✅ CT-REQ-003 (Not a blocker; transparency dashboard future enhancement)
- ✅ LM-REQ-001 (Learning creation service - COMPLETE)
- ✅ LM-REQ-003 (Learning storage - COMPLETE)
- ✅ LM-REQ-004 (Embedding generation - COMPLETE)

## Related Requirements
- LM-REQ-001: Learning creation with trigger contexts
- LM-REQ-005: Learning retrieval and injection (enables future usage)
- LM-ACC-002: Acceptance test for learning injection
- AST-REQ-001: Agent system (future integration point)

## Files Validated
- `tests/integration/Daiv3.Orchestration.IntegrationTests/LearningServiceIntegrationTests.cs`
- `tests/unit/Daiv3.UnitTests/Orchestration/LearningServiceTests.cs`
- `src/Daiv3.Orchestration/LearningService.cs`
- `src/Daiv3.Orchestration/Models/LearningTriggerContext.cs`
- `src/Daiv3.Persistence/Repositories/LearningRepository.cs`

## Acceptance Criteria Met ✅
- [x] A corrected answer (via SelfCorrectionTriggerContext) results in a new learning entry
- [x] Learning entry is persisted to the database
- [x] Learning entry has correct metadata (trigger type, confidence, scope, provenance)
- [x] Embedding is generated automatically for semantic retrieval
- [x] Learning can be retrieved via LearningRepository
- [x] Integration test validates end-to-end flow

**Status:** Complete - All acceptance criteria met via LM-REQ-001 implementation and integration tests.

