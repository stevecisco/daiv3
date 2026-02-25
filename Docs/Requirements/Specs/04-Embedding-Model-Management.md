# 4. Embedding Model Management - Requirements

## Overview
This document defines requirements and architecture for managing embedding models used by the Knowledge Layer. It focuses on model discovery, selection, tokenizer alignment, and optional downloads. This is specific to embedding models and does not cover Foundry Local model management.

## Goals
- Provide a clear registry of supported embedding models.
- Allow selection of Tier 1 and Tier 2 embedding models.
- Ensure tokenizer configuration matches the selected model.
- Support local discovery and optional download of embedding models.

## Functional Requirements
- KM-REQ-014: The system SHALL support nomic-embed-text or all-MiniLM-L6-v2 models.
- KM-EMB-MODEL-001: The system SHALL maintain a registry of supported embedding models with metadata required for selection, validation, and tokenizer alignment.
- KM-EMB-MODEL-002: The system SHALL discover embedding models in the local embeddings directory and optionally download approved models from Hugging Face.
- KM-EMB-MODEL-003: The system SHALL allow selection of the active embedding model for Tier 1 and Tier 2 embeddings and persist the selection in settings.

## Architecture

### Core Components
- EmbeddingModelRegistry: Loads model metadata and validates registry entries.
- EmbeddingModelCatalog: Lists local models discovered in the embeddings directory.
- EmbeddingModelDownloader: Optional download workflow with checksum validation.
- EmbeddingModelSelector: Applies active model selection for Tier 1 and Tier 2.
- EmbeddingTokenizerFactory: Builds tokenizers using per-model configuration.

### Data and Storage
Default layout under `%LOCALAPPDATA%\\Daiv3\\models\\embeddings\\`:

- embeddings/
  - registry.json
  - nomic-embed-text-v1.5/
    - model.onnx
    - model.json
  - all-MiniLM-L6-v2/
    - model.onnx
    - model.json

Notes:
- registry.json defines supported models and tokenizer settings.
- model.json defines model-specific metadata and optional checksums.

### Configuration
Example configuration options:

```
EmbeddingModels:
  Tier1ModelId: "all-MiniLM-L6-v2"
  Tier2ModelId: "nomic-embed-text-v1.5"
  ModelsPath: "%LOCALAPPDATA%\\Daiv3\\models\\embeddings"
  RegistryPath: "%LOCALAPPDATA%\\Daiv3\\models\\embeddings\\registry.json"
  AutoDownloadEnabled: false
```

### Tokenizer Alignment
- Each model entry MUST declare tokenizer configuration.
- Tokenizer settings MUST be validated against model vocab bounds.
- If tokenizer mapping is invalid, the system MUST fail fast with a clear error.
- Model selection MUST verify input/output tensor names and pooling defaults.

## CLI and UI Surfaces
- CLI should allow listing available embedding models and selecting Tier 1 and Tier 2 models.
- MAUI settings should surface model selection and show pros/cons.

## Risks and Open Questions
- Confirm tokenizer strategy for nomic-embed-text and all-MiniLM-L6-v2 using Microsoft.ML.Tokenizers.
- Define checksum policy for downloaded models.
- Decide whether model selection changes require automatic re-index.
