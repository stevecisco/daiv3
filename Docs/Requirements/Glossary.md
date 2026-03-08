# DAIv3 Glossary

**Version:** 1.0  
**Last Updated:** 2026-03-08  
**Status:** Active

This glossary defines the canonical terminology used across DAIv3 requirements, architecture docs, UI labels, and operational documentation.

## Version History

| Version | Date | Changes | Author/Context |
|---|---|---|---|
| 1.0 | 2026-03-08 | Initial baseline with 11 canonical terms (Chunk, Embedding, Foundry Local, MCP, NPU, ONNX Runtime, Learning Memory, RAG, SLM, Tier 1 / Tier 2, TensorPrimitives). Added canonical usage rules and governance section. | GLO-REQ-001 implementation |

## Canonical Terms

| Term | Definition | Related Terms |
|---|---|---|
| Chunk | A token-bounded segment of source content used as a Tier 2 retrieval unit, typically produced with overlap during document processing. | Embedding, Tier 2, RAG |
| Embedding | A numeric vector representation of text used for semantic similarity search and ranking. | Chunk, ONNX Runtime, Tier 1 / Tier 2 |
| Foundry Local | Microsoft local model runtime used by DAIv3 to run local SLM workloads on-device. | SLM, NPU, Model Queue |
| MCP | Model Context Protocol used to discover and invoke external tools through standardized tool contracts. | Agent, Skill, Tool Registry |
| NPU | Neural Processing Unit hardware accelerator preferred for supported AI inference workloads. | ONNX Runtime, GPU, CPU |
| ONNX Runtime | In-process model inference runtime used for embeddings and related ML execution in DAIv3. | Embedding, NPU, TensorPrimitives |
| Learning Memory | Persistent store of structured learnings that are retrieved and injected into future agent execution context. | Learning, Agent, Retrieval |
| RAG | Retrieval-Augmented Generation pattern combining retrieval results with generation to improve answer quality and grounding. | Chunk, Embedding, Tier 1 / Tier 2 |
| SLM | Small Language Model used for local reasoning, summarization, and task execution in constrained environments. | Foundry Local, Model Execution |
| Tier 1 / Tier 2 | Two-stage retrieval strategy where Tier 1 is coarse document-level search and Tier 2 is fine-grained chunk-level search on top candidates. | Chunk, Embedding, RAG |
| TensorPrimitives | .NET SIMD-accelerated tensor math API used for CPU-side vector operations such as cosine similarity. | ONNX Runtime, CPU, Similarity |

## Canonical Usage Rules

- Use term spellings exactly as listed in the table above.
- Prefer `Foundry Local` over `FoundryLocal` in user-facing text.
- Prefer `ONNX Runtime` over `OnnxRuntime` in user-facing text.
- Keep `Tier 1` and `Tier 2` capitalization as shown.
- Use `Learning Memory` as the feature name and `learning` for individual records.

## Governance

- **Spec owner:** Architecture and Documentation maintainers.
- **Update trigger:** Introduce or rename domain concepts in requirements, code, UI, or operations docs.

### Backward Compatibility Strategy

To ensure existing documentation remains valid when the glossary evolves, updates are categorized as either backward compatible (minor version) or breaking (major version).

#### Backward Compatible Changes (Minor Version)
The following changes preserve compatibility with existing documentation:
- **Adding new terms** to the Canonical Terms table
- **Clarifying definitions** without changing canonical spellings or capitalization
- **Adding related terms** to existing entries
- **Adding usage notes** or examples in Canonical Usage Rules
- **Adding notes** to existing term definitions

#### Breaking Changes (Major Version)
The following changes require a major version increment and migration guidance:
- **Renaming canonical terms** (changing the term itself)
- **Removing terms** from the Canonical Terms table
- **Changing canonical spellings** (e.g., "Foundry Local" → "FoundryLocal")
- **Changing canonical capitalization** (e.g., "Tier 1" → "tier 1")
- **Redefining terms** in ways that contradict prior usage

#### Deprecation Workflow
When a term must be renamed or removed:
1. Add the old term to the **Deprecated Terms** section below with:
   - The deprecated term name
   - The replacement term (or "None" if retired)
   - The version when deprecated
   - Migration guidance for existing references
2. Increment the major version number
3. Add an entry to the Version History table describing the breaking change
4. Preserve the deprecated term in the Deprecated Terms section indefinitely to aid future readers
5. Update automated validation tests to recognize both old and new terms during the migration period

### Deprecated Terms

This section lists terms that have been renamed, removed, or superseded. Existing documentation may reference these terms, and this section provides migration guidance.

| Deprecated Term | Replacement Term | Deprecated In Version | Migration Guidance |
|---|---|---|---|
| *(None yet)* | — | — | — |
