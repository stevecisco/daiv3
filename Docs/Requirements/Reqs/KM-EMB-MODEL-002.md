# KM-EMB-MODEL-002

Source Spec: 4. Embedding Model Management - Requirements

## Requirement
The system SHALL discover locally installed embedding model packages in the embeddings directory.

## Implementation Plan (v0.1 - MVP)
- Define the model package directory layout and discovery mechanism.
- Implement discovery that scans `%LOCALAPPDATA%\Daiv3\models\embeddings\` for installed packages.
- Validate package structure (required files: ONNX, metadata, tokenizer artifacts).
- For v0.1, only the bundled nomic-embed-text-v1.5 model is pre-installed.
- Return available models to registry and selection system.

## Future Implementation (v0.2+ - Backlog)
- Support downloading/installing model packages from DAIv3 package repository.
- Add package installer with extraction, validation, and registration.
- See KM-EMB-MODEL-PACKAGING for package format specification.

## Testing Plan (v0.1)
- Unit tests for directory scanning with bundled model.
- Integration tests for discovery with real package structure.
- Negative tests for missing/invalid metadata files.

## Usage and Operational Notes
- System scans embeddings directory at startup and builds available models list.
- If a selected model is missing, system MUST surface remediation steps.
- Discovery validates tokenizer plugin is available (see KM-EMB-MODEL-TOKENIZER).

## Blocking Tasks / Open Questions
- Define model package directory structure (folder naming convention).
- Define package metadata format (JSON, config, or other).
- (Future) Define package source and distribution mechanism for v0.2.

## Dependencies
- KLC-REQ-004
- KM-EMB-MODEL-001

## Related Requirements
- KM-REQ-014
- KM-EMB-MODEL-003
