# KM-EMB-MODEL-002

Source Spec: 4. Embedding Model Management - Requirements

## Requirement
The system SHALL discover locally installed embedding model packages in the embeddings directory and download both Tier 1 and Tier 2 embedding models from Azure Blob Storage on first initialization.

## Implementation Plan (v0.1 - MVP)
- Define the model package directory layout: `%LOCALAPPDATA%\Daiv3\models\embeddings\`
- Implement discovery that scans `%LOCALAPPDATA%\Daiv3\models\embeddings\` for installed packages.
- Implement model downloader that retrieves both models from Azure Blob Storage on first run.
- **Tier 1 Model (Topic/Summary - 384 dimensions):**
  - Model: all-MiniLM-L6-v2
  - Download URL: `https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/model.onnx`
  - Target path: `%LOCALAPPDATA%\Daiv3\models\embeddings\all-MiniLM-L6-v2\model.onnx`
  - Size: ~86 MB
- **Tier 2 Model (Chunk - 768 dimensions):**
  - Model: nomic-embed-text-v1.5
  - Download URL: `https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/model.onnx`
  - Target path: `%LOCALAPPDATA%\Daiv3\models\embeddings\nomic-embed-text-v1.5\model.onnx`
  - Size: ~522 MB
- Display download progress to user (percentage, bytes downloaded, total size).
- Both CLI and MAUI apps SHALL invoke download on initialization if models are missing.
- Validate downloaded files (size check, basic integrity).
- Validate package structure (required files: ONNX, metadata, tokenizer artifacts).
- Return available models to registry and selection system.

## Future Implementation (v0.2+ - Backlog)
- Support downloading additional model packages from DAIv3 package repository.
- Add package installer with extraction, validation, and registration.
- Add SHA256 checksum validation for downloaded models.
- Support resumable downloads for large models.
- See KM-EMB-MODEL-PACKAGING for package format specification.

## Testing Plan (v0.1)
- Unit tests for directory scanning with installed models.
- Unit tests for download URL construction and HTTP client handling.
- Integration tests for discovery with real package structure.
- Integration tests for both model downloads from Azure Blob Storage (test environment).
- Integration tests for progress reporting during download.
- Negative tests for missing/invalid metadata files.
- Negative tests for network failures and corrupted downloads.
- Test initialization logic in both CLI and MAUI contexts.
- Test skip logic when models already exist.

## Usage and Operational Notes
- On first startup, if either model is not present, system MUST download from Azure Blob Storage.
- Download progress MUST be displayed to user (CLI: console output, MAUI: progress UI).
- System scans embeddings directory at startup and builds available models list.
- If download fails, system MUST display clear error with retry instructions.
- If a selected model is missing and download fails, system MUST surface remediation steps.
- Discovery validates tokenizer plugin is available (see KM-EMB-MODEL-TOKENIZER).
- Network connectivity is REQUIRED for first-time initialization.
- Both models use the same tokenizer (r50k_base encoding via Microsoft.ML.Tokenizers).

## Blocking Tasks / Open Questions
- Confirm download timeout and retry policy (recommended: 5 minute timeout, 3 retries).
- Define error handling for network failures during initialization.
- Determine if downloads should be sequential or parallel (current: sequential).
- Define UI/UX for progress display in both CLI and MAUI applications.
- (Future) Confirm if all-MiniLM-L6-v2 truly outputs 384 dimensions or needs projection from 384.

## Dependencies
- KLC-REQ-004
- KM-EMB-MODEL-001

## Related Requirements
- KM-REQ-014
- KM-EMB-MODEL-003
