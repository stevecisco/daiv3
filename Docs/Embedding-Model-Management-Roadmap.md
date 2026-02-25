# Embedding Model Management - Roadmap & Integration

**Overview of v0.1, v0.2, and v0.3 strategies**

---

## Executive Summary

The Daiv3 embedding model system is being built in three phases to balance MVP delivery with long-term extensibility:

| Phase | Version | Timeline | Focus | Key Requirements |
|-------|---------|----------|-------|------------------|
| **Phase 1** | v0.1 | ✅ Complete | Core embedding with 2 hardcoded models | KM-EMB-MODEL-TOKENIZER |
| **Phase 2** | v0.2 | 🟡 Planned (v0.2) | Catalog-driven architecture | KM-EMB-MODEL-CATALOG |
| **Phase 3** | v0.3 | 🟡 Planned (v0.3) | Extensible plugin system | KM-EMB-MODEL-PLUGIN-SYSTEM |

---

## Phase 1: v0.1 - Core Embeddings (✅ Complete)

### What Works
- ✅ Two embedding models (all-MiniLM-L6-v2, nomic-embed-text-v1.5)
- ✅ Model-specific tokenizers (BERT WordPiece, SentencePiece)
- ✅ Automatic downloads from Azure Blob Storage
- ✅ Two-tier embedding architecture (384D and 768D)
- ✅ CLI and MAUI integration
- ✅ Full test coverage (96+ tests passing)

### Architecture
```
App (CLI/MAUI)
  ↓
OnnxEmbeddingGenerator
  ├── EmbeddingTokenizerProvider (hardcoded)
  │   ├── BertWordPieceTokenizer (all-MiniLM-L6-v2)
  │   └── SentencePieceTokenizer (nomic-embed-text-v1.5)
  ├── OnnxEmbeddingModelRunner (ONNX Runtime)
  └── EmbeddingModelDownloadService (Azure)

Models Directory
  ├── all-MiniLM-L6-v2/model.onnx (86 MB)
  └── nomic-embed-text-v1.5/model.onnx (522 MB)
```

### Limitations
- ❌ Adding new models requires recompiling app
- ❌ Tokenizer updates tied to app releases
- ❌ No model versioning or catalog
- ❌ No extensibility for community models

### Suitable For
- MVP (minimum viable product)
- Small number of models (2-5)
- Closed ecosystem (no third-party extensions)

---

## Phase 2: v0.2 - Catalog Foundation (🟡 Planned)

### What Will Be Added
- Catalog file (JSON) defining all models and configurations
- Model discovery from catalog (instead of hardcoded list)
- Model registry extended with catalog metadata
- Model selection UI (CLI and MAUI)
- Version management for models
- Foundation for plugin system

### Architecture
```
App (CLI/MAUI)
  ↓
Catalog Service (downloads catalog.json)
  ├── IEmbeddingModelCatalog (parse)
  ├── ICatalogDownloadService (download + cache)
  └── IModelDiscoveryService (extended)
  ↓
Model Registry (now includes catalog metadata)
  ├── Model metadata (name, tier, dimensions, etc.)
  ├── Download URLs (model, config, tokenizer)
  ├── Tokenizer configuration (type, version, dependencies)
  ├── Compatibility matrix (OS, hardware, framework)
  └── Performance characteristics (latency, memory, etc.)
  ↓
OnnxEmbeddingGenerator (unchanged, still hardcoded tokenizers)
```

### Catalog Example
```json
{
  "version": "1.0",
  "models": [
    {
      "id": "all-MiniLM-L6-v2",
      "tier": 1,
      "dimensions": 384,
      "downloads": {
        "model": { "url": "...", "sha256": "..." },
        "vocab": { "url": "...", "sha256": "..." }
      },
      "tokenizer": {
        "type": "bert_wordpiece",
        "version": "1.0.0"
      }
    },
    { ... more models ... }
  ]
}
```

### Benefits
- ✅ Model list decoupled from app code
- ✅ Add models without recompiling
- ✅ Version management and metadata
- ✅ Foundation for plugin system
- ✅ Model selection UI possible
- ✅ Community can suggest models (PRs to catalog)

### Timeline
- **Spec**: February 25, 2026
- **Implementation**: Estimated March 2026 (~6-8 hours)
- **Integration**: Update KM-EMB-MODEL-001, KM-EMB-MODEL-002, KM-EMB-MODEL-003

### Requirements
- [KM-EMB-MODEL-CATALOG](Reqs/KM-EMB-MODEL-CATALOG.md)
- Updates to KM-EMB-MODEL-001 (registry), KM-EMB-MODEL-002 (discovery), KM-EMB-MODEL-003 (selection)

### Backward Compatibility
- ✅ v0.1 hardcoded models still work
- ✅ Apps can run without catalog (fallback to bundled)
- ✅ No breaking changes
- ✅ Gradual migration possible

---

## Phase 3: v0.3 - Plugin System (🟡 Planned)

### What Will Be Added
- Dynamic DLL-based tokenizer plugins
- Plugin loader service (AssemblyLoadContext)
- Plugin discovery and registration
- Plugin download and versioning
- Plugin dependency resolution
- Fallback to hardcoded tokenizers if plugin unavailable

### Architecture
```
App (CLI/MAUI)
  ↓
Catalog Service (includes plugin DLL URLs)
  ↓
Plugin Manager
  ├── ITokenizerPluginLoader (DLL loading, AssemblyLoadContext)
  ├── IEmbeddingTokenizerPluginProvider (factory)
  ├── ITokenizerPluginDownloadService (download DLLs)
  └── IPluginValidator (SHA256 verification)
  ↓
Plugins Directory
  ├── plugins/Daiv3.Tokenizer.SentencePiece.dll
  ├── plugins/Daiv3.Tokenizer.BertWordPiece.dll
  └── plugins/Daiv3.Tokenizer.Custom.dll (community)
  ↓
OnnxEmbeddingGenerator (uses plugin tokenizers if available)
  └── Fallback to hardcoded tokenizer if plugin unavailable
```

### Plugin Structure
```
Tokenizer Plugin Package:
  plugins/
  ├── Daiv3.Tokenizer.SentencePiece.dll          (plugin DLL)
  ├── Daiv3.Tokenizer.SentencePiece.deps.json    (dependencies)
  ├── README.md                                    (documentation)
  └── LICENSE                                      (license)
```

### Plugin Interface
```csharp
namespace Daiv3.Tokenizer.Abstractions;

public interface ITokenizerPlugin
{
    string PluginName { get; }
    string PluginVersion { get; }
    string[] SupportedTokenizers { get; }
    IEmbeddingTokenizer CreateTokenizer(string tokenizerName, string modelDirectory);
}

public interface IEmbeddingTokenizer
{
    string Name { get; }
    string ModelId { get; }
    int VocabularySize { get; }
    long[] Tokenize(string text);
    bool ValidateTokenIds(long[] tokenIds);
    IReadOnlyDictionary<string, int> GetSpecialTokens();
}
```

### Benefits
- ✅ Add tokenizers without recompiling app
- ✅ Community can create custom tokenizers
- ✅ Independent version management for tokenizers
- ✅ Plugin isolation (AssemblyLoadContext)
- ✅ Plugin unloading and reloading possible
- ✅ Scales to 100+ models easily

### Timeline
- **Spec**: February 25, 2026
- **Implementation**: Estimated April-May 2026 (~10-12 hours)
- **Integration**: Extend KM-EMB-MODEL-PLUGIN-SYSTEM

### Requirements
- [KM-EMB-MODEL-PLUGIN-SYSTEM](Reqs/KM-EMB-MODEL-PLUGIN-SYSTEM.md)
- v0.2 catalog system (prerequisite)

### Backward Compatibility
- ✅ v0.1 hardcoded models continue to work
- ✅ Catalog system unchanged
- ✅ Plugin downgrade to hardcoded tokenizer if needed
- ✅ No breaking changes for v0.2 code

### Future Extensions (v0.4+)
- Plugin marketplace / community hub
- Plugin signing and verification
- Plugin sandboxing with resource limits
- Automatic plugin updates
- Multi-language plugins (C++, Rust, etc.)

---

## Integration Points

### Phase 1 ↔ Phase 2
- No breaking changes
- Add catalog discoveryservice alongside hardcoded lookup
- Both can coexist during transition

### Phase 2 ↔ Phase 3
- Catalog includes plugin DLL URLs
- Plugin loader reads from catalog
- Backward compatible (fallback to hardcoded if plugin unavailable)

### Key Services Over Time

| Service | v0.1 | v0.2 | v0.3 |
|---------|------|------|------|
| `IEmbeddingModelRegistry` | Basic | Extended (catalog metadata) | + Plugin metadata |
| `IModelDiscoveryService` | Directory scan | + Catalog load | + Plugin scan |
| `IEmbeddingTokenizerProvider` | Hardcoded switch | Unchanged | Replaced with `IEmbeddingTokenizerPluginProvider` |
| `IEmbeddingModelDownloadService` | Model + config | + Tokenizer vocab | + Plugin DLLs |
| **NEW** | - | `IEmbeddingModelCatalog` | `ITokenizerPluginLoader` |
| **NEW** | - | `ICatalogDownloadService` | `ITokenizerPluginDownloadService` |

---

## Decision Tree: Which Phase Should I Implement?

```
Do you need MORE THAN 5 embedding models?
├─ Yes → Consider v0.2 (catalog makes management easier)
└─ No  → v0.1 is sufficient

Do you want COMMUNITY TOKENIZER CONTRIBUTIONS?
├─ Yes → Need v0.3 (plugins)
└─ No  → v0.2 catalog is enough

Do you need TOKENIZER UPDATE INDEPENDENCE?
├─ Yes → Need v0.3 (plugins with separate versioning)
└─ No  → v0.2 catalog is enough

Do you want to ADD NEW MODELS MONTHLY?
├─ Yes → v0.2 minimum, v0.3 recommended
└─ No  → v0.1 hardcoded is fine
```

---

## Implementation Status

### v0.1 (✅ Complete - Feb 2026)
- [x] IEmbeddingTokenizer interface
- [x] BertWordPieceTokenizer implementation
- [x] SentencePieceTokenizer implementation
- [x] OnnxEmbeddingGenerator integration
- [x] Automatic downloads from Azure
- [x] Full test suite (96+ tests)
- [x] CLI and MAUI apps working

### v0.2 (📋 Spec Complete, Ready to Implement)
- [x] KM-EMB-MODEL-CATALOG requirement document
- [x] JSON catalog schema documented
- [x] Implementation plan outlined
- [ ] IEmbeddingModelCatalog service
- [ ] ICatalogDownloadService with caching
- [ ] Model discovery from catalog
- [ ] Update registry with catalog metadata
- [ ] Model selection UI
- [ ] Unit and integration tests
- **Estimated**: March 2026 (~6-8 hours)

### v0.3 (📋 Spec Complete, Ready for v0.2 Completion)
- [x] KM-EMB-MODEL-PLUGIN-SYSTEM requirement document
- [x] Plugin architecture designed
- [x] Plugin interface specified
- [ ] ITokenizerPluginLoader service
- [ ] ITokenizerPluginDownloadService
- [ ] Plugin discovery system
- [ ] AssemblyLoadContext implementation
- [ ] Migrate tokenizers to plugins
- [ ] Plugin versioning and dependencies
- [ ] Security (SHA256 verification)
- [ ] Unit and integration tests
- **Estimated**: April-May 2026 (~10-12 hours)

---

## Documentation Structure

```
Docs/Requirements/
├── Reqs/
│   ├── KM-EMB-MODEL-001.md ........................... Model Registry
│   ├── KM-EMB-MODEL-002.md ........................... Model Discovery & Download
│   ├── KM-EMB-MODEL-003.md ........................... Model Selection
│   ├── KM-EMB-MODEL-TOKENIZER.md .................... v0.1 Tokenizer Abstraction ✅
│   ├── KM-EMB-MODEL-CATALOG.md ....................... v0.2 Catalog System (NEW)
│   └── KM-EMB-MODEL-PLUGIN-SYSTEM.md ............... v0.3 Plugin System (NEW)
├── Architecture/
│   └── decisions/
│       └── ADD-20260225-Plugin-Architecture-for-Models.md  (NEW)
├── Master-Implementation-Tracker.md ................. Updated with v0.2/v0.3 backlog
└── Embedding-Model-Management-Roadmap.md .......... This document (NEW)
```

---

## Next Steps

1. **Immediate (v0.1 Completion)**
   - ✅ Finalize tokenizer implementation
   - ✅ Complete all tests (96+ passing)
   - ✅ Document v0.2 and v0.3 specifications
   - ✅ Create architecture decision document

2. **Before v0.2 Starts**
   - Review v0.2 catalog spec with team
   - Finalize catalog JSON schema
   - Plan model maintenance strategy
   - Determine community contribution process

3. **v0.2 Implementation**
   - Build catalog download service
   - Extend model registry
   - Implement model discovery from catalog
   - Create model selection UI
   - Update documentation

4. **After v0.2 Completion**
   - Review v0.3 plugin system spec
   - Plan plugin developer documentation
   - Create plugin templates
   - Determine security/verification strategy

5. **v0.3 Implementation**
   - Build plugin loader and discovery
   - Migrate tokenizers to separate DLLs
   - Implement version management
   - Security verification (SHA256)
   - Comprehensive testing

---

## Questions & Clarifications

**Q: Can we skip v0.2 and go straight to v0.3?**  
A: Not recommended. v0.2 foundation is much simpler and provides value independently. v0.3 depends on v0.2 catalog.

**Q: When should we start v0.2?**  
A: After v0.1 stabilizes (current) and when we're sure we need catalog features. Current timeline: March 2026.

**Q: What if a v0.3 plugin fails to load?**  
A: System falls back to hardcoded tokenizer (graceful degradation). User sees warning in logs but embedding still works.

**Q: Can community create plugins now?**  
A: Only after v0.3 is complete. For now, they can file requests to add models to the catalog.

**Q: Does v0.2 require changing app code?**  
A: No. Catalog loads in parallel to existing hardcoded lookup. Can migrate gradually.

---

**Document Version**: 1.0  
**Status**: Active - Guiding embedded model management roadmap  
**Last Updated**: February 25, 2026

**References**:
- [Architecture Decision: Plugin System](Architecture/decisions/ADD-20260225-Plugin-Architecture-for-Models.md)
- [v0.2 Catalog Requirements](Reqs/KM-EMB-MODEL-CATALOG.md)
- [v0.3 Plugin System Requirements](Reqs/KM-EMB-MODEL-PLUGIN-SYSTEM.md)
- [v0.1 Tokenizer Implementation](Reqs/KM-EMB-MODEL-TOKENIZER.md)
