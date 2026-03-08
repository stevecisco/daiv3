# GLO-REQ-001

Source Spec: 14. Glossary - Requirements

## Requirement
The system SHALL define and use the following terms consistently: Chunk, Embedding, Foundry Local, MCP, NPU, ONNX Runtime, Learning Memory, RAG, SLM, Tier 1 / Tier 2, TensorPrimitives.

## Implementation Status
**COMPLETE** (100% - Canonical glossary and automated consistency checks implemented)

## Implementation Details

### Canonical Glossary Source
- Added authoritative glossary document: `Docs/Requirements/Glossary.md`
- Includes all required terms with:
  - Canonical term spelling
  - Definition
  - Related terms
- Added canonical usage rules for preferred spellings and capitalization.

### Specification Linkage
- Updated `Docs/Requirements/Specs/14-Glossary.md` with a canonical source section pointing to:
  - `Docs/Requirements/Glossary.md`

### Automated Consistency Validation
- Added architecture-level test suite:
  - `tests/unit/Daiv3.Architecture.Tests/GlossaryConsistencyTests.cs`
- Validation coverage includes:
  - Canonical glossary file exists
  - All required terms are explicitly defined in glossary table
  - Preferred spellings are enforced for key terms (for example `Foundry Local` and `ONNX Runtime`)
  - Design document contains canonical term set

## Testing Plan
- ✅ Unit tests for glossary consistency implemented in architecture test suite
- ✅ Positive coverage for required term definitions
- ✅ Negative coverage for known non-canonical spellings

## Verification Commands
```powershell
dotnet test tests/unit/Daiv3.Architecture.Tests/Daiv3.Architecture.Tests.csproj --nologo --verbosity minimal
```

## Usage and Operational Notes
- Canonical glossary updates should be made in `Docs/Requirements/Glossary.md`.
- New domain terms should be added there first, then referenced in requirements/UI/docs.
- Consistency validation runs in architecture unit tests and should remain green before requirement completion.

## Dependencies
- GLO-DATA-001 (glossary persistence schema and repository)

## Related Requirements
- GLO-REQ-002 (UI labels and documentation alignment)
- GLO-ACC-001 (public documentation consistency)
- GLO-ACC-002 (glossary accessibility)
