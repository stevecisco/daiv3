# LM-ACC-003

Source Spec: 9. Learning Memory - Requirements

## Requirement
Users can suppress a learning and it is no longer injected.

## Implementation Summary

**Status:** ✅ Complete (100%)

This acceptance criterion is validated end-to-end in orchestration integration tests by proving a learning is injected while active, then excluded after suppression.

### Core Test
**Location:** [tests/integration/Daiv3.Orchestration.IntegrationTests/LearningInjectionAcceptanceTests.cs](../../../tests/integration/Daiv3.Orchestration.IntegrationTests/LearningInjectionAcceptanceTests.cs)

**Test:** `AcceptanceTest_SuppressedLearning_IsNoLongerInjected`

### What the Test Validates
1. Creates a high-confidence, semantically relevant learning (status `Active`).
2. Executes an agent task and confirms the learning appears in injected prompt context.
3. Suppresses that learning via `LearningStorageService.SuppressLearningAsync(learningId)`.
4. Verifies persisted status changes to `Suppressed`.
5. Re-runs the same task and verifies the suppressed learning is no longer injected.

### Implementation Path Validated
- `LearningStorageService.SuppressLearningAsync` updates learning status to `Suppressed`.
- `LearningRetrievalService.RetrieveLearningsAsync` filters to `Status == Active` only.
- `AgentManager.RetrieveAndFormatLearningsAsync` injects only retrieved learnings.
- Result: suppressed learnings are excluded from prompt injection.

## Testing Plan

### Automated Tests ✅
- [x] `LearningInjectionAcceptanceTests.AcceptanceTest_SuppressedLearning_IsNoLongerInjected`
- [x] Verifies pre-suppression injection occurs for the same task
- [x] Verifies post-suppression injection excludes the suppressed learning

### Manual Verification (CLI)
- [ ] Create learning: `learning create ...`
- [ ] Confirm injection on similar task (agent execution)
- [ ] Suppress learning: `learning suppress --id <id>`
- [ ] Re-run similar task and confirm learning is absent from injected context

## Usage and Operational Notes

### User Flow
1. Create or identify an existing learning.
2. Suppress it using CLI lifecycle command (`learning suppress`).
3. Run similar agent tasks; suppressed learning is no longer included in context injection.

### User-Visible Effects
- Suppressed learning remains stored and auditable.
- Suppressed learning does not influence future agent prompt context.
- Learning can be re-enabled by editing status back to `Active`.

### Operational Constraints
- Offline compatible (local SQLite + local retrieval only).
- Depends on embedding-based retrieval flow for similarity matching.
- Suppression applies immediately after status update is persisted.

## Dependencies
- ✅ KM-REQ-013 (Embedding infrastructure)
- ✅ LM-REQ-005 (Learning retrieval/injection)
- ✅ LM-REQ-008 (Suppress lifecycle operation)
- ⏸️ CT-REQ-003 (Future UI transparency surface)

## Related Requirements
- LM-REQ-005
- LM-REQ-008
- LM-ACC-002
