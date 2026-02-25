# KM-EMB-MODEL-CATALOG

Source Spec: 4. Embedding Model Management - Requirements

**Status: BACKLOG (v0.2 - Plugin Foundation)**

## Requirement
The system SHALL support a catalog file that defines available embedding models, their configurations, locations, and tokenizer requirements, enabling extensible model discovery and selection without requiring application recompilation.

## Rationale
As the embedding model ecosystem grows, maintaining hardcoded model lists becomes a scalability bottleneck. A catalog-driven architecture enables:
- **Extensibility**: Add new models to the catalog without rebuilding the app
- **Flexibility**: Change model URLs, configurations, or tokenizers via catalog updates
- **Multi-tiered Discovery**: Support user-installed models, system models, and community models
- **Version Management**: Track model versions and dependencies

## Specification (v0.2 - MVP)

### 1. Catalog File Format

**File Location**: Central hosted catalog
```
https://stdaiv3.blob.core.windows.net/models/embedding/catalog.json
```

**File Format**: JSON with the following schema

```json
{
  "$schema": "https://stdaiv3.blob.core.windows.net/schemas/embedding-catalog-v1.json",
  "version": "1.0",
  "catalogVersion": "2026-02-25",
  "application": "Daiv3",
  "description": "Embedding models catalog for Daiv3 knowledge system",
  "lastUpdated": "2026-02-25T12:00:00Z",
  
  "models": [
    {
      "id": "nomic-embed-text-v1.5",
      "name": "Nomic Embed Text v1.5",
      "version": "1.5.0",
      "tier": 2,
      "description": "High-dimensional dense embedding model optimized for semantic similarity search. 768 dimensions.",
      "dimensions": 768,
      "vocabularySize": 32000,
      "license": "CC-BY-NC-4.0",
      "source": "https://nomicfoundation.io/",
      "sourceRepository": "https://huggingface.co/nomic-ai/nomic-embed-text-v1.5",
      
      "downloads": {
        "model": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/model.onnx",
          "format": "onnx",
          "size": 547521536,
          "sha256": "abc123def456...",
          "estimatedDurationSeconds": 120,
          "retryPolicy": { "maxRetries": 3, "backoffMultiplier": 2.0 }
        },
        "config": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/config.json",
          "format": "json",
          "size": 2048,
          "sha256": "def456abc123..."
        },
        "vocab": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/sentencepiece.model",
          "format": "binary",
          "size": 788448,
          "sha256": "ghi789jkl012..."
        }
      },

      "tokenizer": {
        "type": "plugin",
        "description": "SentencePiece tokenizer for nomic embedding model",
        "version": "1.0.0",
        "minimumVersion": "1.0.0",
        "maximumVersion": "2.0.0"
      },

      "onnxRuntime": {
        "minimumVersion": "1.17.0",
        "providers": ["DirectML", "QNN", "CPU"],
        "preferredProvider": "Auto"
      },

      "metadata": {
        "trainingData": "High-quality semantic text pairs",
        "trainedDate": "2024-06-01",
        "requiresAttentionMask": true,
        "requiresTokenTypeIds": false,
        "poolingStrategy": "mean",
        "normalizeEmbeddings": true
      },

      "performance": {
        "inferenceTimeMs": 45,
        "memoryUsageMb": 1024,
        "hardwareTier": "Standard",
        "recommendedBatchSize": 32
      },

      "compatibility": {
        "minOsVersion": "Windows 10 21H2",
        "targetFramework": "net10.0",
        "requiredCapabilities": ["DirectML"]
      }
    },

    {
      "id": "all-MiniLM-L6-v2",
      "name": "All MiniLM-L6-v2",
      "version": "1.0.0",
      "tier": 1,
      "description": "Lightweight sentence-transformers model for fast topic/summary embeddings. 384 dimensions.",
      "dimensions": 384,
      "vocabularySize": 30522,
      "license": "Apache-2.0",
      "source": "https://www.sbert.net/",
      "sourceRepository": "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2",
      
      "downloads": {
        "model": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/model.onnx",
          "format": "onnx",
          "size": 90237952,
          "sha256": "jkl012mno345...",
          "estimatedDurationSeconds": 30,
          "retryPolicy": { "maxRetries": 3, "backoffMultiplier": 2.0 }
        },
        "config": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/config.json",
          "format": "json",
          "size": 1856,
          "sha256": "mno345pqr678..."
        },
        "vocab": {
          "url": "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/vocab.txt",
          "format": "text",
          "size": 430756,
          "sha256": "pqr678stu901..."
        }
      },

      "tokenizer": {
        "type": "plugin",
        "description": "BERT WordPiece tokenizer for all-MiniLM",
        "version": "1.0.0",
        "minimumVersion": "1.0.0",
        "maximumVersion": "2.0.0"
      },

      "onnxRuntime": {
        "minimumVersion": "1.17.0",
        "providers": ["DirectML", "QNN", "CPU"],
        "preferredProvider": "Auto"
      },

      "metadata": {
        "trainingData": "Diverse semantic text pairs",
        "trainedDate": "2022-08-15",
        "requiresAttentionMask": true,
        "requiresTokenTypeIds": true,
        "poolingStrategy": "mean",
        "normalizeEmbeddings": true
      },

      "performance": {
        "inferenceTimeMs": 8,
        "memoryUsageMb": 256,
        "hardwareTier": "Lite",
        "recommendedBatchSize": 64
      },

      "compatibility": {
        "minOsVersion": "Windows 10 21H2",
        "targetFramework": "net10.0",
        "requiredCapabilities": ["CPU"]
      }
    }
  ],

  "metadata": {
    "totalModels": 2,
    "totalSizeBytes": 637759488,
    "updateFrequency": "Monthly",
    "supportEmail": "support@daiv3.example.com"
  }
}
```

### 2. Catalog Discovery & Loading

**Services Required** (v0.2):
- `IEmbeddingModelCatalog` - Load, parse, and cache catalog
- `ICatalogDownloadService` - Download catalog from Azure with caching
- `ICatalogValidator` - Validate catalog schema and integrity

**Implementation Details**:
- Load catalog on application startup (with 5-minute cache)
- Validate catalog schema against embedded JSON schema
- Log all available models and metadata
- Cache catalog locally with timestamp: `%LOCALAPPDATA%\Daiv3\cache\embedding-catalog-latest.json`
- Fall back to last-known-good catalog if download fails
- Retry catalog download up to 3 times with exponential backoff (5s, 10s, 20s)

**Catalog Validation**:
- Validate JSON schema (structure)
- Validate all URLs are HTTPS and accessible (ping test)
- Validate SHA256 checksums format
- Verify model IDs are unique
- Verify no duplicate tier assignments within model type

### 3. Model Discovery from Catalog

**Discovery Process**:
1. Load catalog from `%LOCALAPPDATA%\Daiv3\cache\embedding-catalog-latest.json`
2. For each model in catalog:
   - Check if installed at: `%LOCALAPPDATA%\Daiv3\models\embeddings\{modelId}\model.onnx`
   - If installed, mark as "installed" and verify tokenizer is available
   - If not installed, mark as "available for download"
3. Return filtered list based on query (e.g., "Tier 2 models", "installed models")
4. Update "last checked" timestamp for each model

**Catalog Update Service**:
- Run on application startup
- Check for newer catalog (LastUpdated > local cached version)
- Download if newer version available
- Log all changes (new models, deprecated models, updated versions)

### 4. Model Metadata in Registry

**Update KM-EMB-MODEL-001 (Registry)** to include:
- Catalog entry metadata (dimensions, tier, source, license)
- Download URLs for model, config, tokenizer artifacts
- Performance characteristics
- Tokenizer type and version requirements
- Compatibility matrix (OS version, hardware tier, framework version)

### 5. Error Handling

**Catalog Download Failures**:
- If catalog download fails on startup: Use cached catalog (if available) or bundled fallback
- Log warning but allow app to start (graceful degradation)
- Attempt catalog refresh every 5 minutes in background

**Corrupted Catalog**:
- If cached catalog is invalid: Delete cache and retry download
- If download still fails: Use bundled minimal fallback catalog
- Alert user: "Model catalog unavailable; using offline mode"

**Missing Models**:
- If user selects a model from catalog that's not installed and can't be downloaded: Offer menu of alternative models
- If no viable alternative: Suggest downloading model manually

### 6. Configuration & Settings

**Settings Location**: `%LOCALAPPDATA%\Daiv3\config\models.json`

```json
{
  "catalogUrl": "https://stdaiv3.blob.core.windows.net/models/embedding/catalog.json",
  "catalogCacheTtlMinutes": 5,
  "catalogLastUpdated": "2026-02-25T12:00:00Z",
  "downloadTimeoutSeconds": 300,
  "maxRetries": 3,
  "verifyChecksums": true,
  "tier1ModelId": "all-MiniLM-L6-v2",
  "tier2ModelId": "nomic-embed-text-v1.5",
  "autoDownloadModels": true
}
```

## Future Implementation (v0.3 - Plugin System)
- Plugin DLL URLs in catalog with version management
- Dynamic DLL loading and tokenizer plugin instantiation
- Plugin dependency resolution (e.g., tokenizer v1.2 requires ONNX Runtime 1.17+)
- Add remove/uninstall functionality with cleanup
- Support community-contributed models catalog

## Testing Plan (v0.2)

### Unit Tests
- JSON schema validation (valid and invalid catalogs)
- Catalog parsing and model extraction
- URL validation and format checking
- Caching logic (expiry, fallback, refresh)
- Query filtering (by tier, installation status, etc.)

### Integration Tests
- Download catalog from test Azure Blob Storage
- Parse and validate actual catalog file
- Update registry with catalog entries
- Handle network timeouts and failures
- Fallback to cached/bundled catalog

### Negative Tests
- Malformed JSON catalog
- Missing required fields in model entries
- Invalid URLs or checksums
- Network failures during download
- Catalog version mismatches

### Performance Tests
- Catalog parsing performance (should be <100ms even for 100+ models)
- Memory usage when caching large catalogs
- Background refresh thread doesn't block UI

## Usage and Operational Notes

- Catalog download happens on application startup (non-blocking if cached)
- Users can manually refresh catalog via Settings menu (future v0.3 feature)
- All model recommendations come from catalog (future: community-driven)
- Catalog is signed/verified before loading (future v0.3 security feature)
- Deprecation notices in catalog will warn users before models are removed
- Model versioning allows pinning to specific versions (future v0.4)

## Acceptance Criteria

- [ ] Catalog schema defined and validated
- [ ] Catalog parser implemented and tested (all valid schemas parse correctly)
- [ ] Catalog cache implemented with TTL and fallback
- [ ] Model discovery reflects catalog contents
- [ ] Model registry updated with catalog metadata
- [ ] Error handling for all failure modes
- [ ] Logging for all catalog operations
- [ ] Documentation for catalog maintainers (URL format, versioning strategy)
- [ ] Backward compatibility: App still works with hardcoded models if catalog unavailable

## Dependencies
- KM-EMB-MODEL-001 (Registry, needs update)
- KM-EMB-MODEL-002 (Discovery, needs update)
- KM-EMB-MODEL-003 (Selection, needs update)

## Related Requirements
- KM-EMB-MODEL-PLUGIN-SYSTEM (v0.3 - extends this)
- KLC-REQ-013 (Model management UI)

## Blocking Tasks / Open Questions
- [ ] Determine catalog ownership and maintenance responsibility
- [ ] Define frequency and process for catalog updates
- [ ] Confirm Azure Blob Storage URL for catalog distribution
- [ ] Decide on community model contribution process (v0.3+)
- [ ] Define signature/verification mechanism for catalog trust (v0.3+)
- [ ] Determine whether specialized hardware (NPU, GPU) affects model recommendations
