# Daiv3.Knowledge – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Knowledge management orchestration library. Coordinates the full document indexing pipeline, two-tier vector search (Tier 1: online embeddings; Tier 2: local ONNX embeddings), and knowledge graph construction. Acts as the glue between document processing, embedding generation, and the persistence layer.

## Project Type

Library

## Target Framework

`net10.0` standard; the referenced `Daiv3.Knowledge.Embedding` sub-library handles Windows/NPU TFM switching transparently.

## Key Responsibilities

- Document indexing workflow (ingest → chunk → embed → store)
- Two-tier vector search orchestration
- Knowledge base lifecycle management
- Integration with `Daiv3.Knowledge.DocProc` (parsing) and `Daiv3.Knowledge.Embedding` (vectors)
- Integration with `Daiv3.Persistence` for metadata and vector storage

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.Knowledge.DocProc` | Called for document parsing (PDF, Office, Markdown) |
| `Daiv3.Knowledge.Embedding` | Called for embedding generation (ONNX/DirectML) |
| `Daiv3.Persistence` | Stores document metadata and vector indices |
| `Daiv3.Orchestration` | Consumes knowledge search during task execution |

## Test Projects

```powershell
# Unit tests
dotnet test tests/unit/Daiv3.Knowledge.Tests/Daiv3.Knowledge.Tests.csproj --nologo --verbosity minimal

# Integration tests (real file I/O, real SQLite)
dotnet test tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj --nologo --verbosity minimal
```
