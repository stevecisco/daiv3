# MM-REQ-014

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL enumerate all cached models in the Foundry Local directory.

## Rationale
Users need to see which models they've already downloaded to make space management decisions and avoid redundant downloads. This is performed by scanning the cache directory.

## Implementation Plan
- Recursively scan the model cache directory structure.
- Identify model directories (typically under publisher subdirectories like `Microsoft/`).
- For each directory, check if it contains model files (indicates a downloaded model).
- Return list of cached model names with paths and sizes.
- Handle empty cache and scan errors gracefully.

## Testing Plan
- Unit test to parse sample directory structure.
- Integration test to scan actual cache with multiple models.
- Edge case tests: empty cache, symbolic links, permission errors.
- Performance test: scan time with 100+ cached models.

## Usage and Operational Notes
- Can be called frequently; results should be cached briefly.
- User-visible: "Cached Models" list in CLI or UI showing download status.
- Disks usage calculation based on directory sizes.
- Supports space reclamation workflow.

## Dependencies
- MM-REQ-007 (shared cache directory setup)
- MM-REQ-015 (directory name matching)

## Related Requirements
- MM-REQ-015 (matching to catalog)
- MM-REQ-016 (file size detection)
- MM-REQ-019 (deletion)
