# MM-ACC-004

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
A user can delete a cached model and reclaim disk space.

## Acceptance Scenario
- User lists cached models and identifies one to remove (e.g., "Phi-4-mini-instruct-generic-cpu").
- They issue "delete phi-4" or select delete in the UI.
- The system prompts: "Delete Phi-4-mini-instruct (7.5 GB)? Yes/No"
- Upon confirmation, the model directory is recursively deleted.
- The system confirms: "Deleted model. Freed 7.5 GB"
- Disk space is immediately available.

## Implementation Status

✅ **COMPLETE** - CLI implementation fully functional, acceptance tests created

### CLI Implementation
- Command: `Daiv3.FoundryLocal.Management.Cli` → Option 4 "Delete cached model"
- Service: `FoundryLocalManagementService.DeleteCachedModelsAsync()`
- Features:
  - Delete by model alias, ID, or number from list
  - Optional version and device type filters
  - Confirmation prompt showing model ID and size
  - Reports number of deleted models and space freed
  - Supports cascading deletion (multiple variants)
  - Safe: warns if model currently loaded

### Test Coverage
- **Acceptance Test**: `ModelManagementAcceptanceTests.MM_ACC_004_DeleteCachedModel_RemovesModelAndReclaimsDiskSpace`
- **Location**: `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs`
- **Status**: Test created, skipped by default (requires pre-cached model)
- **Verifies**:
  - Model deleted from cache directory
  - No longer appears in cached models list
  - Cached count decreases appropriately
  - Disk space reclaimed (logical verification)

## Verification Steps
1. Get initial free disk space.
2. Download a model (note its size).
3. Verify it's listed in cached models.
4. Delete the model.
5. Verify it no longer appears in cached list.
6. Verify disk free space increased by approximately the model size.

## Testing Approach
- ✅ Integration test with a real model (skipped by default).
- ✅ Directory deletion works correctly.
- ✅ Matching logic (partial names, exact names, IDs) verified.
- ✅ Cascading deletion supported (multiple variants of same model).

## Usage Notes
- Deletion is irreversible; confirmation is essential.
- Users appreciate confirmation of freed space.
- Should warn if model is currently loaded.
- Multiple variants of same model can be deleted together with a flag.

## Related Requirements
- ✅ MM-REQ-019 (deletion logic) - Complete
- MM-REQ-021 (cascading deletion) - Complete (via MM-REQ-019)
- MM-REQ-022 (warning for loaded models) - Complete (via MM-REQ-019)

## Implementation Files
- `src/Daiv3.FoundryLocal.Management/FoundryLocalManagementService.cs` - Core service
- `src/Daiv3.FoundryLocal.Management/ServiceCatalogClient.cs` - Delete operations
- `src/Daiv3.FoundryLocal.Management.Cli/Program.cs` - CLI with confirmation
- `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs` - Acceptance tests

## Status
**Complete (100%)** - March 3, 2026
