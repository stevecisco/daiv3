# MM-ACC-002

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
A user can download a model by name, version, or device type to the shared cache without error.

## Acceptance Scenario
- User selects "Download Model" in the CLI or UI.
- They provide a model identifier (e.g., "phi-3" or "phi-3-mini:2").
- Optionally, they specify a device type (e.g., "GPU").
- The system resolves the model from the catalog.
- Download begins with progress bar (0-100%).
- Upon completion, the model is available in the cache.
- The system confirms: "Downloaded phi-3-mini (v2, GPU) - 7.5 GB"

## Verification Steps
1. Start the CLI and execute "download phi-3".
2. Verify download progresses (progress bar updates).
3. Verify model appears in cached models list after download.
4. Verify file size matches catalog specification.
5. Verify Foundry service can load the downloaded model.

## Testing Approach
- Integration test with small test model.
- Mock progress reporting.
- Verify idempotency (downloading same model twice doesn't fail).

## Usage Notes
- Download may take minutes to hours depending on model size.
- Cancellation should be supported (Ctrl+C or UI button).
- Users appreciate real-time progress feedback.
- Error messages should be clear (e.g., disk space, network, permissions).

## Related Requirements
- MM-REQ-007 (cache directory)
- MM-REQ-010 (variant selection)
- MM-REQ-011 (auto-selection)
- MM-REQ-012 (progress tracking)
