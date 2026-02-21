# MM-REQ-019

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL support deletion of cached models by name or ID.

## Rationale
Users need to free up disk space by removing models they no longer need. The system facilitates cache cleanup.

## Implementation Plan
- Accept model identifier (name, alias, or ID).
- Locate matching cached model directories.
- Validate that the model is not currently loaded by Foundry.
- Recursively delete the directory and all contents.
- Report disk space freed.
- Handle errors (in-use models, permission issues) gracefully.

## Testing Plan
- Unit test to verify directory deletion.
- Integration test to delete model and verify it's gone.
- Edge case: attempt to delete in-use model (should warn).
- Permission error handling (read-only filesystem).

## Usage and Operational Notes
- User-visible: Confirmation dialog before deletion.
- Warning if model is currently loaded.
- Freed space is reported to user.
- Irreversible operation; recommend confirmation.

## Dependencies
- MM-REQ-014 (enumerate cached models)
- File system write access

## Related Requirements
- MM-REQ-015 (matching names to directories)
- MM-REQ-023 (cache statistics)
