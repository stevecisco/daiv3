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

- Spec owner: Architecture and Documentation maintainers.
- Update trigger: Introduce or rename domain concepts in requirements, code, UI, or operations docs.
- Backward compatibility: When renaming terms, preserve prior aliases in notes and migration guidance.
