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

## Verification Steps
1. Start the CLI and execute "list available" command.
2. Verify at least 5 models are listed.
3. Verify each model has variants with device types and sizes.
4. Verify caching status is accurate compared to actual cache directory.
5. Verify performance: list completes in < 2 seconds.

## Testing Approach
- Integration test with actual Foundry service running.
- Mock Foundry service for unit tests.
- Performance benchmark test.

## Usage Notes
- This is a prerequisite for download and cache management.
- Variant display helps users choose the right model for their device.
- Cache status allows users to skip downloading models they already have.

## Related Requirements
- MM-REQ-001 (list catalog)
- MM-REQ-002 (group by alias)
- MM-REQ-006 (file sizes)
