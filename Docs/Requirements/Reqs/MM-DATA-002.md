# MM-DATA-002

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
Cached model directory structure SHALL follow the Foundry Local convention: `%LOCALAPPDATA%/.foundry/cache/models/<publisher>/<model-name>/` where publisher is a folder (e.g., `Microsoft`) and each model has its own subdirectory.

## Rationale
The Foundry Local service uses a specific directory layout to organize cached models. The model management system must correctly parse this structure to enumerate and identify models.

## Implementation Notes
- Typical structure:
  ```
  %LOCALAPPDATA%/.foundry/cache/models/
    Microsoft/
      Phi-3-mini/             (directory with model files)
      Phi-3-mini-instruct-cpu/
      Phi-4-mini-instruct-generic-cpu/
    Other-Publisher/
      ModelName/
  ```
- Each model is stored in its own subdirectory under the publisher folder.
- Version information may be encoded in the model name itself (e.g., "Phi-3-mini" vs "Phi-3-mini-instruct").
- Model identifier parsing:
  - Extract from publisher/model-name pattern.
  - Match directory structure to catalog entries.
  - Empty directories (incomplete downloads) should be distinguishable from complete models.

## Testing Plan
- Unit test to parse various directory names.
- Integration test to scan actual Foundry cache and match to catalog.
- Verify version suffix handling.
- Verify partial/complete detection.

## Acceptance Criteria
- Can correctly identify "Phi-3-mini" and "Phi-3-mini-instruct" from directory names.
- Correctly strips "-1", "-2" suffixes.
- Maps to correct catalog entry.
- Handles publisher folders transparently.

## Related Requirements
- MM-REQ-014 (enumerate cache)
- MM-REQ-015 (match to catalog)
