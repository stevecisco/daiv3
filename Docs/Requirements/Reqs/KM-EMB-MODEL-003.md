# KM-EMB-MODEL-003

Source Spec: 4. Embedding Model Management - Requirements

## Requirement
The system SHALL select and validate the active embedding model for Tier 1 and Tier 2 embeddings, including tokenizer plugin validation.

## Implementation Plan (v0.1 - MVP)
- Define settings options for Tier 1 and Tier 2 model ids.
- Validate selections against the model registry (see KM-EMB-MODEL-001).
- Validate that required tokenizer plugin is available (see KM-EMB-MODEL-TOKENIZER).
- For v0.1, default to bundled nomic-embed-text-v1.5 for both Tier 1 and Tier 2.
- Log the active model, dimensions, tokenizer, and execution provider at startup.

## Future Implementation (v0.2+ - Backlog)
- Support selection changes during runtime (currently startup-only).
- Trigger re-indexing when model selection changes.
- Provide CLI and MAUI UIs for model selection and metadata display.

## Testing Plan (v0.1)
- Unit tests for selection validation against registry and tokenizer plugin availability.
- Integration tests for applying bundled model selection during embedding generation.
- Negative tests for invalid model ids or missing tokenizer plugins.

## Usage and Operational Notes
- At startup, validate that selected Tier 1 and Tier 2 models exist and tokenizers are available.
- Settings MUST persist model selection to disk.
- Fail fast with clear error if selected model or tokenizer plugin unavailable.

## Blocking Tasks / Open Questions
- Confirm default model ids (both Tier 1 and Tier 2 for MVP).
- Define settings storage location and format (JSON config, database, or other).
- (Future) Define re-index behavior and user confirmation when switching models.

## Dependencies
- KLC-REQ-002
- KM-EMB-MODEL-001
- KM-EMB-MODEL-002

## Related Requirements
- KM-REQ-014
- KM-REQ-015
- KM-REQ-016
