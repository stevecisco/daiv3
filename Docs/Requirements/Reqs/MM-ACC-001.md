# MM-ACC-001

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
A user can list all available models in the catalog with variants, device types, and file sizes displayed.

## Acceptance Scenario
- User launches the CLI or accesses the model management UI.
- They select "List Available Models" or navigate to the model browser.
- The system queries Foundry's catalog and displays:
  - Model alias (e.g., "Phi-4-mini-instruct")
  - Available variants (e.g., [CPU v1, GPU v1, CPU v2, NPU v2])
  - File sizes for each variant
  - Current cache status ([CACHED] or [not cached])
- The list is readable within 2 seconds.
- Models are grouped by alias and sorted alphabetically.

## Implementation Status

✅ **COMPLETE** - CLI implementation fully functional, acceptance tests created

### CLI Implementation
- Command: `Daiv3.FoundryLocal.Management.Cli` → Option 1 "List available models"
- Service: `FoundryLocalManagementService.ListAvailableModelsAsync()`
- Features:
  - Groups models by alias (alphabetically sorted)
  - Shows all variants with device types (CPU/GPU/NPU)
  - Displays file sizes in MB
  - Shows cache status [CACHED] marker
  - Auto-select option for best variant
  - Number-based selection for convenience

### Test Coverage
- **Acceptance Test**: `ModelManagementAcceptanceTests.MM_ACC_001_ListAvailableModels_ShowsVariantsDeviceTypesAndFileSizes`
- **Location**: `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs`
- **Verifies**:
  - At least 5 models listed
  - Each model has variants with device types and sizes
  - Models sorted alphabetically by alias
  - Cache status accuracy
  - Performance (< 2 seconds)

## Verification Steps
1. Start the CLI and execute "list available" command.
2. Verify at least 5 models are listed.
3. Verify each model has variants with device types and sizes.
4. Verify caching status is accurate compared to actual cache directory.
5. Verify performance: list completes in < 2 seconds.

## Testing Approach
- ✅ Integration test with actual Foundry service running.
- Mock Foundry service for unit tests (not required, real service available).
- Performance benchmark test (implicitly tested via acceptance criteria).

## Usage Notes
- This is a prerequisite for download and cache management.
- Variant display helps users choose the right model for their device.
- Cache status allows users to skip downloading models they already have.

## Related Requirements
- ✅ MM-REQ-001 (list catalog) - Complete
- MM-REQ-002 (group by alias) - Complete (via MM-REQ-001)
- MM-REQ-006 (file sizes) - Complete (via MM-REQ-001)

## Implementation Files
- `src/Daiv3.FoundryLocal.Management/FoundryLocalManagementService.cs` - Core service
- `src/Daiv3.FoundryLocal.Management/ServiceCatalogClient.cs` - Catalog client
- `src/Daiv3.FoundryLocal.Management/ModelEntries.cs` - Data models
- `src/Daiv3.FoundryLocal.Management.Cli/Program.cs` - CLI interface
- `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelManagementAcceptanceTests.cs` - Acceptance tests

## Status
**Complete (100%)** - March 3, 2026
