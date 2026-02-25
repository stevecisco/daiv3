# 4. Embedding Model Packaging & Distribution - Specification

## Overview
This document specifies the format and structure for embedding model packages that are distributed by the DAIv3 team or installed by users. It covers v0.2+ expansion beyond the bundled MVP model.

## Goals
- Define a standard format for bundling ONNX models with their tokenizer artifacts and metadata.
- Enable installation and discovery of multiple embedding models.
- Support future expansion to multiple models without changes to core system.

## Model Package Structure

A model package is either:
1. **Folder-based** (for bundled models during development)
2. **ZIP distribution** (for published releases)
3. **NuGet package** (for production distribution, future)

### Folder Structure

```
%LOCALAPPDATA%\Daiv3\models\embeddings\
├── registry.json                           # Master registry of all installed models
├── nomic-embed-text-v1.5/                 # One folder per model (v0.1 bundled)
│   ├── model.onnx                         # ONNX model file
│   ├── config.json                        # Model metadata and configuration
│   ├── sentencepiece.model                # SentencePiece vocabulary/tokenizer
│   └── README.md                          # Model documentation (optional)
└── all-minilm-l6-v2/                      # Future: additional models (v0.2+)
    ├── model.onnx
    ├── config.json
    ├── wordpiece-vocab.txt
    └── README.md
```

## File Specifications

### config.json (Model Metadata)

```json
{
  "model": {
    "id": "nomic-embed-text-v1.5",
    "displayName": "Nomic Embed Text v1.5 (Recommended)",
    "description": "Fast, efficient semantic search embedding model trained on 235M text pairs",
    "version": "1.5.0",
    "source": "huggingface:nomic-ai/nomic-embed-text-v1.5",
    "license": "openrail",
    "author": "Nomic AI"
  },
  
  "onnx": {
    "file": "model.onnx",
    "fileSize": 265000000,
    "sha256": "abc123def456...",
    "inputTensors": {
      "inputIds": {
        "name": "input_ids",
        "type": "int64",
        "shape": [1, -1]
      },
      "attentionMask": {
        "name": "attention_mask",
        "type": "int64",
        "shape": [1, -1]
      },
      "tokenTypeIds": {
        "name": "token_type_ids",
        "type": "int64",
        "shape": [1, -1]
      }
    },
    "outputTensors": {
      "lastHiddenState": {
        "name": "last_hidden_state",
        "type": "float32",
        "shape": [1, -1, 768]
      }
    },
    "poolingStrategy": "mean",
    "normalizeEmbeddings": true
  },

  "tokenizer": {
    "plugin": "SentencePieceTokenizer",
    "type": "sentencepiece",
    "vocabFile": "sentencepiece.model",
    "vocabSize": 32000,
    "specialTokens": {
      "unknownToken": 0,
      "clsToken": 1,
      "sepToken": 2
    }
  },

  "dimensions": {
    "tier1": {
      "supported": true,
      "dims": 384,
      "recommendedUsage": "Fast coarse semantic search"
    },
    "tier2": {
      "supported": true,
      "dims": 768,
      "recommendedUsage": "Fine-grained chunk similarity"
    }
  },

  "performance": {
    "speed": "fast",
    "quality": "high",
    "sizeOnDisk": "265 MB",
    "memoryUsage": "~530 MB (FP32 model + inference buffer)"
  },

  "metadata": {
    "pros": [
      "Excellent semantic search quality",
      "Fast inference (optimized 4-layer architecture)",
      "Trained on diverse text pairs"
    ],
    "cons": [
      "Larger model size (265 MB)",
      "Higher memory usage than distilled models"
    ],
    "recommendations": "Recommended for production use with good semantic accuracy when size and memory are not constraints.",
    "hardwareOptimizations": {
      "npu": "Supported via DirectML",
      "gpu": "Supported via DirectML",
      "cpu": "Supported with TensorPrimitives fallback"
    }
  }
}
```

### registry.json (Master Registry)

```json
{
  "version": "1.0",
  "registryPath": "%LOCALAPPDATA%\\Daiv3\\models\\embeddings",
  "installedModels": [
    {
      "id": "nomic-embed-text-v1.5",
      "packagePath": "nomic-embed-text-v1.5",
      "status": "installed",
      "installedAt": "2026-02-25T07:32:56Z",
      "version": "1.5.0"
    }
  ],
  "tokenizers": [
    {
      "plugin": "SentencePieceTokenizer",
      "assembly": "Daiv3.Knowledge.Embedding.Tokenizers",
      "fullyQualifiedName": "Daiv3.Knowledge.Embedding.Tokenizers.SentencePieceTokenizer",
      "version": "1.0.0",
      "supportedModels": ["nomic-embed-text-v1.5"]
    }
  ]
}
```

## Package Distribution Formats (Future v0.2+)

### ZIP Distribution
```
daiv3-embedding-model-nomic-v1.5.zip
├── model/
│   ├── model.onnx
│   ├── config.json
│   └── sentencepiece.model
└── INSTALL.md
```

### NuGet Package (Future)
```
Daiv3.EmbeddingModels.Nomic.v1.5 (NuGet Package)
├── embedded resources:
│   ├── model.onnx
│   ├── config.json
│   └── sentencepiece.model
└── PostInstall.ps1 (extract to embeddings directory)
```

## Package Validation

When installing or discovering a package, the system MUST:

1. **Validate directory structure** — Required files present
2. **Validate config.json** — Valid JSON, required fields
3. **Validate ONNX file** — Exists, file size matches config, SHA256 matches (if provided)
4. **Validate tokenizer artifacts** — Vocab file exists and matches config
5. **Validate tokenizer plugin** — Required plugin is registered in system
6. **Validate tensor names** — ONNX tensor names match config.json

## Installation Flow (v0.2+)

```
User downloads: daiv3-embedding-model-all-minilm-v2.zip
                     ↓
         Extract to: %LOCALAPPDATA%\Daiv3\models\embeddings\all-minilm-l6-v2\
                     ↓
         System scans directory (KM-EMB-MODEL-002)
                     ↓
         Validates package structure and tokenizer (KM-EMB-MODEL-003)
                     ↓
         Updates registry.json with installed model
                     ↓
         Available for selection (KM-EMB-MODEL-003)
```

## Versioning

- config.json format version (if schema changes)
- Model version (upstream model version)
- Implementation version (tokenizer/plugin version)

## Security Considerations

- SHA256 checksums for ONNX files (prevent corruption/tampering)
- Signed manifests (future, if distributing via untrusted channels)
- Tokenizer plugins validated at load time (prevent arbitrary code)

## Dependencies
- KM-EMB-MODEL-002 (discovery)
- KM-EMB-MODEL-003 (selection)
- KM-EMB-MODEL-TOKENIZER (tokenizer plugin system)

## Related Requirements
- None (this is an architecture/packaging spec, not a functional requirement)

## Open Questions
- Should NuGet be the distribution mechanism, or custom package format?
- Should registry.json be auto-generated or user-editable?
- What's the policy for model versioning and updates?
