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

## Verification Steps
1. Get initial free disk space.
2. Download a model (note its size).
3. Verify it's listed in cached models.
4. Delete the model.
5. Verify it no longer appears in cached list.
6. Verify disk free space increased by approximately the model size.

## Testing Approach
- Integration test with a real model.
- Verify directory deletion works correctly.
- Test matching logic (partial names, exact names, IDs).
- Cascading deletion test (multiple variants of same model).

## Usage Notes
- Deletion is irreversible; confirmation is essential.
- Users appreciate confirmation of freed space.
- Should warn if model is currently loaded.
- Multiple variants of same model can be deleted together with a flag.

## Related Requirements
- MM-REQ-019 (deletion logic)
- MM-REQ-021 (cascading deletion)
- MM-REQ-022 (warning for loaded models)
