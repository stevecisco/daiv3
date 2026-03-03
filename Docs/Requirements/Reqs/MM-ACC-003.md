# MM-ACC-003

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
A user can list all cached models and see which are currently available on disk.

## Acceptance Scenario
- User issues "list cached" command or opens the cached models view.
- The system scans the Foundry model cache directory.
- It displays all downloaded models:
  - Model ID (e.g., "Phi-4-mini-instruct-generic-cpu")
  - Device type (CPU, GPU, NPU)
  - File size
  - Last modified date (optional)
- The list is sorted by model name and size is shown in MB or GB.
- Completion time is < 2 seconds even with 100+ cached models.

## Implementation Status

✅ **COMPLETE** - CLI implementation fully functional, acceptance tests created

### CLI Implementation
- Command: `Daiv3.FoundryLocal.Management.Cli` → Option 3 "List cached models"
- Service: `FoundryLocalManagementService.ListCachedModelsAsync()`
- Features:
  - Scans Foundry model cache directory
  - Shows model ID, device type, file size
  - Fast performance (< 2 seconds even with 100+ models)
  - Cross-referenced with catalog for accurate metadata
  - Sorted display for easy navigation

### Test Coverage
- **Acceptance Test**: `ModelManagementAcceptanceTests.MM_ACC_003_ListCachedModels_ShowsDownloadedModelsWithSizes`
- **Location**: `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs`
- **Verifies**:
  - All cached models listed
  - Each has ID, device type, and size
  - Performance < 2 seconds
  - No false positives
  - Cross-check with catalog for accuracy

## Verification Steps
1. Download at least 3 different models.
2. Execute "list cached" command.
3. Verify all downloaded models are listed.
4. Verify file sizes are accurate (spot-check against disk).
5. Verify no false positives (partially downloaded or stale models).

## Testing Approach
- ✅ Integration test with multiple cached models.
- ✅ Directory scanning correctly identifies model files.
- ✅ Performance test with large number of cached models (verified via < 2s requirement).

## Usage Notes
- Users rely on this to make deletion decisions.
- Accurate sizes are important for space planning.
- List should refresh quickly (no long scans).
- Integration with cache statistics (total size, free space).

## Related Requirements
- ✅ MM-REQ-014 (enumerate cache) - Complete
- MM-REQ-015 (match to catalog) - Complete (via MM-REQ-014)
- MM-REQ-016 (file size detection) - Complete (via MM-REQ-014)

## Implementation Files
- `src/Daiv3.FoundryLocal.Management/FoundryLocalManagementService.cs` - Core service
- `src/Daiv3.FoundryLocal.Management/ServiceCatalogClient.cs` - Cache enumeration
- `src/Daiv3.FoundryLocal.Management.Cli/Program.cs` - CLI interface
- `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs` - Acceptance tests

## Status
**Complete (100%)** - March 3, 2026
