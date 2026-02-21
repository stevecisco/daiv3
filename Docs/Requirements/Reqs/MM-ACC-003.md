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

## Verification Steps
1. Download at least 3 different models.
2. Execute "list cached" command.
3. Verify all downloaded models are listed.
4. Verify file sizes are accurate (spot-check against disk).
5. Verify no false positives (partially downloaded or stale models).

## Testing Approach
- Integration test with multiple cached models.
- Verify directory scanning correctly identifies model files.
- Performance test with large number of cached models.

## Usage Notes
- Users rely on this to make deletion decisions.
- Accurate sizes are important for space planning.
- List should refresh quickly (no long scans).
- Integration with cache statistics (total size, free space).

## Related Requirements
- MM-REQ-014 (enumerate cache)
- MM-REQ-015 (match to catalog)
- MM-REQ-016 (file size detection)
