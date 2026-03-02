# LM-REQ-002

Source Spec: 9. Learning Memory - Requirements

## Requirement
Each learning SHALL include fields: id, title, description, trigger_type, scope, source_agent, source_task_id, embedding, tags, confidence, status, times_applied, timestamps, created_by.

## Implementation Summary

### Status: Complete (100%)

LM-REQ-002 is implemented through the existing learning data contract, persistence schema, and learning creation workflow delivered in LM-DATA-001 and LM-REQ-001.

### Field Coverage

All required fields are present and populated in `Learning`:

- `id` → `Learning.LearningId`
- `title` → `Learning.Title`
- `description` → `Learning.Description`
- `trigger_type` → `Learning.TriggerType`
- `scope` → `Learning.Scope`
- `source_agent` → `Learning.SourceAgent`
- `source_task_id` → `Learning.SourceTaskId`
- `embedding` → `Learning.EmbeddingBlob` + `Learning.EmbeddingDimensions`
- `tags` → `Learning.Tags`
- `confidence` → `Learning.Confidence`
- `status` → `Learning.Status`
- `times_applied` → `Learning.TimesApplied`
- `timestamps` → `Learning.CreatedAt`, `Learning.UpdatedAt`
- `created_by` → `Learning.CreatedBy`

### Owning Components

- `src/Daiv3.Persistence/Entities/CoreEntities.cs` (`Learning` entity)
- `src/Daiv3.Persistence/SchemaScripts.cs` (`learnings` table and constraints)
- `src/Daiv3.Persistence/Repositories/LearningRepository.cs` (CRUD + mapping of all fields)
- `src/Daiv3.Orchestration/LearningService.cs` (field population during learning creation)

## Testing Summary

### Unit Tests

- `tests/unit/Daiv3.UnitTests/Orchestration/LearningServiceTests.cs`
	- Added explicit LM-REQ-002 coverage test: `CreateLearningAsync_PopulatesAllLmReq002RequiredFields`
	- Verifies all required fields are populated in created learning records.

### Integration Tests

- `tests/integration/Daiv3.Persistence.IntegrationTests/LearningRepositoryIntegrationTests.cs`
	- `AddAndGetById_PersistsLearningWithProvenanceAndTimestamps`
	- `AddAndGetById_PersistsLearningWithEmbedding`
	- Verifies database persistence and retrieval of required record fields.

### Schema Validation

- SQLite check constraints enforce valid `trigger_type`, `scope`, `status`, and confidence range.

## Usage and Operational Notes

- Learning records are created through `ILearningService` trigger-specific methods.
- Required fields are populated at creation time and persisted unchanged unless explicitly updated.
- Provenance and timestamps are always included for auditability.

## Dependencies
- ✅ LM-REQ-001
- ✅ LM-DATA-001

## Related Requirements
- LM-DATA-001
- LM-REQ-001
- LM-REQ-003
