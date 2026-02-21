# 4. Model Management & Lifecycle - Requirements

## Overview
This document specifies requirements for managing Small Language Models (SLMs) on the local device. Model management covers discovery, download, caching, variant selection, and lifecycle operations for models used by Foundry Local. The implementation leverages the Foundry service's shared model cache directory (`%LOCALAPPDATA%/.foundry/cache/models`) to avoid duplicate storage and optimize disk usage.

## Goals
- Provide a unified interface for model discovery and lifecycle management.
- Minimize disk space waste by using the shared Foundry Local cache directory.
- Enable efficient variant selection (CPU/GPU/NPU) based on device capabilities.
- Support progress tracking and cancellation for long-running operations.
- Provide transparent model inventory and usage information.

## Functional Requirements

### Model Discovery & Catalog
- MM-REQ-001: The system SHALL list all available models from the Foundry service catalog without requiring a download.
- MM-REQ-002: The system SHALL group models by alias and display available variants for each model.
- MM-REQ-003: The system SHALL parse model metadata including ID, display name, publisher, version, and provider type.
- MM-REQ-004: The system SHALL detect and display device type capabilities (CPU, GPU, NPU) for each model variant.
- MM-REQ-005: The system SHALL determine execution provider (e.g., CPU, ONNX DirectML, CUDA) for each variant.
- MM-REQ-006: The system SHALL indicate file size for each model variant to inform download decisions.

### Model Download & Caching
- MM-REQ-007: The system SHALL download models to the Foundry Local service's shared model cache directory (typically `%LOCALAPPDATA%/.foundry/cache/models`).
- MM-REQ-008: The system SHALL NOT duplicate model storage; use the existing cache if available.
- MM-REQ-009: The system SHALL verify directory structure and create directories as needed.
- MM-REQ-010: The system SHALL support downloading a specific model variant by alias and version.
- MM-REQ-011: The system SHALL support automatic variant selection when version/device is not specified (NPU > GPU > CPU priority).
- MM-REQ-012: The system SHALL track download progress and report completion percentage.
- MM-REQ-013: The system SHALL support cancellation of in-flight downloads.

### Model Cache Inventory
- MM-REQ-014: The system SHALL enumerate all cached models in the Foundry Local directory.
- MM-REQ-015: The system SHALL match cached model directories to catalog entries by parsing directory names and removing version suffixes.
- MM-REQ-016: The system SHALL detect and report model file sizes from cached directories.
- MM-REQ-017: The system SHALL support filtering cached models by alias, version, or device type.
- MM-REQ-018: The system SHALL distinguish between fully downloaded and partially downloaded models.

### Model Deletion & Cache Management
- MM-REQ-019: The system SHALL support deletion of cached models by name or ID.
- MM-REQ-020: The system SHALL recursively delete model directories to free disk space.
- MM-REQ-021: The system SHALL support cascading deletion when multiple variants match a deletion request.
- MM-REQ-022: The system SHALL warn the user before deleting a currently loaded model.
- MM-REQ-023: The system SHALL track disk usage and provide cache statistics.

### Model Metadata & Manifest
- MM-REQ-024: The system SHALL parse model metadata from the Foundry service catalog (name, alias, URI, provider, prompt template).
- MM-REQ-025: The system SHALL store and manage prompt templates associated with models.
- MM-REQ-026: The system SHALL support version identification from model IDs (parsed from colon suffix, e.g., "phi-3-mini:3").

### Variant Selection
- MM-REQ-027: The system SHALL allow explicit selection of model variants by device type.
- MM-REQ-028: The system SHALL provide a default selection algorithm: prefer NPU > GPU > CPU based on availability.
- MM-REQ-029: The system SHALL display the selected variant prominently in model listings.
- MM-REQ-030: The system SHALL support switching between variants of the same model.

## Non-Functional Requirements
- MM-NFR-001: Model listing operations SHOULD complete in <2 seconds on devices with 10+ cached models.
- MM-NFR-002: Download operations SHOULD support progress reporting at least once per second.
- MM-NFR-003: Model cache enumeration SHOULD scan directories efficiently and cache results.
- MM-NFR-004: The system SHOULD support concurrent download operations for different models.
- MM-NFR-005: Model selection logic SHOULD be deterministic and reproducible.
- MM-NFR-006: The system SHOULD provide clear feedback for download cancellation.

## Data Requirements
- MM-DATA-001: Model metadata SHALL include: ID, alias, display name, publisher, URI, file size, device type, execution provider, and prompt template.
- MM-DATA-002: Cached model directory structure SHALL follow the Foundry Local convention: `%LOCALAPPDATA%/.foundry/cache/models/<publisher>/<model-name>/` where publisher is a folder (e.g., `Microsoft`) and each model has its own subdirectory containing the model files and metadata.
- MM-DATA-003: The system SHALL not require persistent metadata storage for models; all information is derived from the live catalog and file system.
- MM-DATA-004: Downloads SHALL be idempotent; re-downloading a model that is already cached SHALL succeed without error.

## Dependencies
- Microsoft.AI.Foundry.Local SDK for service integration.
- Foundry service running locally with required endpoints:
  - `/foundry/list` endpoint to discover available models from the catalog
  - `/openai/download` endpoint to download models to the cache directory
  - `/openai/status` endpoint to retrieve model cache directory path
- File system access to read model cache directory.

## Acceptance Criteria
- MM-ACC-001: A user can list all available models in the catalog with variants, device types, and file sizes displayed.
- MM-ACC-002: A user can download a model by name, version, or device type to the shared cache without error.
- MM-ACC-003: A user can list all cached models and see which are currently available on disk.
- MM-ACC-004: A user can delete a cached model and reclaim disk space.
- MM-ACC-005: Download progress is visible and matches actual data transfer.
- MM-ACC-006: Model variant selection correctly prioritizes NPU > GPU > CPU when device is not specified.

## Out of Scope
- Multi-user or shared cache management across accounts.
- Remote model repositories beyond Foundry's built-in catalog.
- Model optimization, quantization, or compression.
- Custom model creation or fine-tuning.
- Model rollback or version history management.
- Automatic cache cleanup policies.

## Risks and Open Questions
- **Risk:** Foundry service crash or unavailability blocks model discovery and download. *Mitigation:* Error handling and retry logic with user feedback.
- **Risk:** File system permissions prevent cache directory access. *Mitigation:* Early validation and user guidance to ensure correct permissions.
- **Question:** Should the system support alternative cache directories? *Decision:* Use Foundry's configured directory; document how to change it via Foundry settings.
- **Question:** Should models be downloaded in parallel? *Decision:* Single queue for now; extensible for future parallel downloads.
- **Question:** How are model updates (new versions) handled? *Decision:* Treat as distinct catalog entries; user explicitly downloads new versions; old versions remain until deleted.
