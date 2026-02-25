# KM-REQ-014

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL support nomic-embed-text or all-MiniLM-L6-v2 models.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults for per-model selection.
- Introduce an embedding model registry with model metadata and tokenizer mapping.
- Add discovery and download workflows for embedding models.
- Implement selection and validation logic (Tier 1 and Tier 2 model choices).
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Configuration selects the active embedding model for Tier 1 and Tier 2.
- Each embedding model entry MUST declare tokenizer configuration and vocab bounds.
- Selection MUST validate model signature compatibility (input/output tensor names, pooling, dimensions).
- Models may be discovered from `%LOCALAPPDATA%\\Daiv3\\models\\embeddings\\` and optionally downloaded.
- If auto-download is disabled, missing models MUST produce a clear error with remediation steps.
- UI surfaces (CLI/MAUI) SHOULD show model pros/cons and selection status.

## Blocking Tasks / Open Questions
- Define the embedding model registry schema and required metadata fields (tokenizer config, vocab bounds, tensor names).
- Decide the Hugging Face download policy (allowlist only vs user-provided model id) and checksum requirements.
- Define re-index behavior when Tier 1 or Tier 2 model selection changes.
- Backlog: dynamic model switching during runtime (non-happy-path).

## Dependencies
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004

## Related Requirements
- KM-REQ-015
- KM-REQ-016
- KM-EMB-MODEL-001
- KM-EMB-MODEL-002
- KM-EMB-MODEL-003
