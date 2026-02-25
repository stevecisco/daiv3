# ADD-20260225-Plugin-Architecture-for-Models

**Architecture Decision Document**

**Status**: Approved (for v0.2-v0.3 implementation)  
**Decision Date**: February 25, 2026  
**Version**: 1.0

---

## Context

The Daiv3 system aims to support multiple embedding models with different tokenization requirements. The current v0.1 implementation uses hardcoded tokenizer implementations (BERT WordPiece and SentencePiece) bundled with the application.

As the ecosystem grows, maintaining tokenizer implementations within the main codebase becomes:
- **Unscalable**: Adding new tokenizers requires recompiling the entire application
- **Inflexible**: Tokenizer updates are tied to application updates
- **Closed**: Community cannot contribute new tokenizers without recompiling
- **Coupled**: Application logic tightly bound to tokenizer implementations

## Decision

**Implement a three-phase plugin architecture for extensible tokenizer support:**

### Phase 1 (v0.1 - Current)
- Hardcoded tokenizer implementations (BERT, SentencePiece)
- Simple and reliable for MVP
- No external plugin infrastructure required

### Phase 2 (v0.2 - Catalog Foundation)
- JSON catalog file defining available models, versions, and downloads
- Catalog-driven model discovery
- Foundation for future plugin system
- No dynamic DLL loading yet

### Phase 3 (v0.3 - Full Plugin System)
- Dynamic DLL-based tokenizer plugin loading
- Separate tokenizer implementations into plugin packages
- AssemblyLoadContext-based plugin isolation
- Plugin discovery, versioning, and dependency management

---

## Rationale

### Why Three Phases?

1. **v0.1 Stability**: Hardcoded approach is proven, predictable, no external dependencies
2. **v0.2 Extensibility**: Catalog groundwork without complexity of dynamic loading
3. **v0.3 Flexibility**: Full plugin system when ecosystem matures

This staged approach:
- ✅ Delivers value immediately (v0.1: working embeddings)
- ✅ Prepares infrastructure (v0.2: catalog, model registry)
- ✅ Enables extensibility (v0.3: community plugins)
- ✅ Minimizes risk (each phase independent and testable)

### Why Not Implement Full Plugins Now?

**Complexity Trade-offs**:
| Aspect | v0.1 (Hardcoded) | v0.3 (Full Plugins) |
|--------|------------------|-------------------|
| Implementation | ~500 lines | ~3000 lines |
| Testing | Basic unit tests | Complex loader/version tests |
| Stability | Proven | New infrastructure risk |
| Deployment | Single EXE | EXE + DLLs + catalog |
| Community Ready | No | Yes, but needs v0.2 first |

**Justification**: 
- v0.1 delivers core functionality with minimal risk
- v0.2 builds foundation for v0.3
- v0.3 can be implemented independently when ready

---

## Architecture

### v0.2 Catalog Design

**Goal**: Enable model discovery and versioning without dynamic loading

```
Application
├── Model Registry (hardcoded tokenizers)
├── Catalog Loader (downloads catalog.json)
├── Model Discovery (scans %APPDATA%\models\embeddings)
└── Configuration (persists model selection)
```

**Key Components**:
- `IEmbeddingModelCatalog` - Parse catalog metadata
- `ICatalogDownloadService` - Download and cache catalog
- `IModelDiscoveryService` - Scan for installed models
- Catalog file (JSON) with model definitions
- Backward compatible with v0.1 hardcoded models

**Benefits**:
- ✅ Prepare for plugin system without breaking v0.1
- ✅ Enable model selection UI in v0.2
- ✅ Foundation for v0.3 plugin discovery
- ✅ Minimal new dependencies

### v0.3 Plugin System Design

**Goal**: Support extensible tokenizers without recompilation

```
Application
├── Plugin Loader (AssemblyLoadContext)
├── Model Registry (now includes plugins)
├── Catalog (includes plugin DLL URLs)
├── Plugin Downloader (downloads DLLs)
└── Plugin Discovery (loads .deps.json files)
```

**Key Components**:
- `ITokenizerPluginLoader` - Load DLLs with isolation
- `ITokenizerPluginDownloadService` - Download plugin DLLs
- `IEmbeddingTokenizerPluginProvider` - Factory for plugin instances
- Plugin interface (`IEmbeddingTokenizer`, `ITokenizerPlugin`)
- Plugin attribute system for discovery

**Benefits**:
- ✅ Add tokenizers without recompilation
- ✅ Community can create custom tokenizers
- ✅ Independent version management for tokenizers
- ✅ Isolated plugin loading (AssemblyLoadContext)

---

## Key Design Decisions

### 1. AssemblyLoadContext Isolation (v0.3)
**Decision**: Load each plugin into its own AssemblyLoadContext

**Rationale**:
- Allows unloading and reloading plugins
- Prevents version conflicts between plugins
- Isolates plugin failures from core application
- Standard .NET approach for plugin systems

**Alternatives Considered**:
- ❌ Load all into default context (can't unload, version conflicts)
- ❌ AppDomain isolation (not available in .NET Core)

### 2. Well-Known Plugin Interface

**Decision**: Define explicit `IEmbeddingTokenizer` and `ITokenizerPlugin` interfaces

**Rationale**:
- Clear contract for plugin developers
- Enables discovery and validation
- Type-safe instantiation
- Versioning via assembly attributes

**Alternatives Considered**:
- ❌ Reflection-only discovery (fragile, no type checking)
- ❌ Scripting language (slower, harder to deploy)

### 3. Catalog-First Model Discovery (v0.2)

**Decision**: Define models primarily through JSON catalog, secondarily from local scan

**Rationale**:
- Enables version management and metadata
- Single source of truth for model definitions
- Supports community model contributions
- Makes model selection UX possible

**Alternatives Considered**:
- ❌ Pure directory scanning (no versioning, metadata)
- ❌ Database-based (too heavyweight for MVP)
- ❌ Configuration files per model (hard to maintain)

### 4. DLL Versioning via Separate Plugins

**Decision**: Each tokenizer type is a separate DLL with independent versioning

**Rationale**:
- BERT tokenizer updates don't require SentencePiece rebuild
- Reduces deployment burden (only updated DLL needed)
- Supports side-by-side versions if needed

**Alternatives Considered**:
- ❌ Single tokenizer DLL (couples unrelated tokenizers)
- ❌ Inline implementations (defeats plugin purpose)

### 5. SHA256 Trust Model (v0.3)

**Decision**: Verify plugin DLLs via SHA256 hash from catalog

**Rationale**:
- Fast verification (hash files, not signatures)
- Catalog is source of truth
- Detects corruption or tampering
- Prepares for cryptographic signing in v0.4+

**Alternatives Considered**:
- ❌ Cryptographic signatures (overhead, key management)
- ❌ Size check only (insufficient security)
- ❌ No verification (unsafe)

---

## Phased Implementation Plan

### Phase 1 (v0.1 - Current)
**Timeline**: January-February 2026  
**Scope**: Hardcoded tokenizers for 2 models

**Deliverables**:
- ✅ IEmbeddingTokenizer interface
- ✅ BertWordPieceTokenizer implementation
- ✅ SentencePieceTokenizer implementation
- ✅ OnnxEmbeddingGenerator integration
- ✅ 10 unit tests, 4 integration tests

**Effort**: ~2-3 hours (completed)

---

### Phase 2 (v0.2 - Catalog Foundation)
**Timeline**: March 2026 (estimated)  
**Scope**: Catalog-driven model discovery

**Deliverables**:
- `IEmbeddingModelCatalog` service
- `ICatalogDownloadService` with caching
- JSON catalog format (schema + examples)
- `IModelDiscoveryService` extended
- Update `IEmbeddingModelRegistry`
- Model selection UI (CLI and MAUI)
- 20+ unit tests, 5+ integration tests
- Catalog documentation for maintainers

**Effort**: ~6-8 hours

**Risks**:
- Catalog schema may need iteration based on real usage
- Cache invalidation complexity (TTL strategy)

**Dependencies**:
- v0.1 embedding infrastructure (complete)
- Azure Blob Storage for catalog hosting

---

### Phase 3 (v0.3 - Full Plugin System)
**Timeline**: April-May 2026 (estimated)  
**Scope**: Dynamic plugin loading and management

**Deliverables**:
- `ITokenizerPluginLoader` service
- `ITokenizerPluginDownloadService`
- `IEmbeddingTokenizerPluginProvider` factory
- Plugin attribute system
- AssemblyLoadContext implementation
- Migrate current tokenizers to plugins
- 30+ unit tests, 10+ integration tests
- Plugin developer documentation
- Example plugin template

**Effort**: ~10-12 hours

**Risks**:
- AssemblyLoadContext complexity per platform
- Plugin dependency resolution edge cases
- Plugin loader edge cases (missing DLL, version conflict)

**Dependencies**:
- v0.2 catalog system (complete)
- Extended model registry with plugin metadata

---

## Migration Path

### v0.1 → v0.2
**Breaking Changes**: None
- Existing hardcoded tokenizers continue to work
- Catalog system runs alongside hardcoded lookup
- No UI changes required (optional v0.2 feature)
- Gradual rollout possible

### v0.2 → v0.3
**Breaking Changes**: None (with compatibility layer)
- Plugin system runs alongside hardcoded tokenizers
- If plugin unavailable, fallback to hardcoded
- Current hardcoded tokenizers wrapped as "built-in" plugins
- Graceful degradation if plugin fails to load

### Long-term
- Deprecate hardcoded tokenizers (v0.4+)
- Require plugin system exclusively
- Support community tokenizer marketplace

---

## Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Plugin DLL version conflicts | Medium | High | Side-by-side versioning, dependency resolver |
| Plugin load failures at runtime | Medium | High | Fallback to hardcoded, clear error messages |
| Catalog becomes stale/corrupt | Low | Medium | TTL + fallback to bundled minimal catalog |
| Community plugins with bugs | High | Medium | Plugin review process, sandboxing (v0.4) |
| Assembly unloading issues | Low | High | Minimize plugin lifetime, unit tests |
| Cross-platform plugin loading | Medium | High | Test on Windows/Linux/macOS, AssemblyLoadContext isolation |

---

## Monitoring & Observability

### Logging Strategy
- Log all plugin load/unload events
- Log catalog downloads and cache hits/misses
- Log model selection and tokenizer initialization
- Alert on version mismatches or missing plugins

### Telemetry
- Track plugin load times
- Monitor plugin failure rates
- Survey community tokenizer usage
- A/B test catalog vs. hardcoded discovery

---

## Future Considerations (v0.4+)

1. **Plugin Marketplace**: Community-contributed tokenizer hub
2. **Plugin Signing**: Cryptographic signatures for trust
3. **Plugin Sandboxing**: Restrict filesystem/network access
4. **Automatic Updates**: Plugin auto-discovery of newer versions
5. **Dependency Trampolining**: Automatic dependency resolution library
6. **Performance Profiling**: Plugin load/execution benchmarks
7. **Multi-Language Support**: Plugins in C++, Rust, etc. (via FFI)

---

## References

- [KM-EMB-MODEL-TOKENIZER](../Reqs/KM-EMB-MODEL-TOKENIZER.md) - v0.1 Tokenizer abstraction
- [KM-EMB-MODEL-CATALOG](../Reqs/KM-EMB-MODEL-CATALOG.md) - v0.2 Catalog system
- [KM-EMB-MODEL-PLUGIN-SYSTEM](../Reqs/KM-EMB-MODEL-PLUGIN-SYSTEM.md) - v0.3 Plugin system
- [.NET Plugin Architecture Patterns](https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- [AssemblyLoadContext Documentation](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)

---

## Approval

**Architecture Review**: ✅ Approved for v0.2-v0.3 implementation  
**Status**: Active - Phased implementation underway

---

**Document Version**: 1.0  
**Last Updated**: February 25, 2026  
**Next Review**: After v0.2 specification review (estimated March 2026)
