# MM-REQ-001

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL list all available models from the Foundry service catalog without requiring a download.

## Rationale
Users need to see all models available for download before deciding which to cache locally. This must work offline by querying the local catalog endpoint.

**Architectural Context:** This requirement is foundational because Foundry Local **automatically manages hardware-optimized variants** for each model (NPU, GPU, CPU optimizations). By querying the catalog, DAIv3 gains visibility into what Foundry Local provides, allowing it to display variant metadata (execution provider, device type, file size) to users without having to maintain separate variant definitions or hardware detection logic. The catalog is the single source of truth for model availability and optimization opportunities.

## Implementation Plan
- Call Foundry service `/foundry/list` endpoint.
- Parse JSON response containing model metadata.
- Return structured list of models with aliases and variants.
- Handle service unavailability gracefully.

## Testing Plan
- Unit test to verify parsing of sample catalog JSON.
- Integration test to call actual Foundry service and verify response structure.
- Error handling test for unavailable service.

## Usage and Operational Notes
- Invoked by UI to populate model selection lists.
- Requires Foundry service running locally.
- Results can be cached briefly (e.g., 30 seconds) to avoid repeated calls.
- User-visible: "Available Models" view in CLI or UI.

## Dependencies
- KLC-REQ-005 (Foundry Local integration)
- Foundry service running

## Related Requirements
- MM-REQ-002 (variant grouping)
- MM-REQ-003 (metadata parsing)
- MM-REQ-004 (device type detection)
