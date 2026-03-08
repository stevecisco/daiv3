# GLO-ACC-001

Source Spec: 14. Glossary - Requirements

## Requirement
All public documentation uses glossary terms consistently.

## Implementation Status
**COMPLETE** (100%)

## Implementation Details

This acceptance criterion validates the implementation of GLO-REQ-002 (UI labels and documentation SHALL align with glossary definitions).

### Verification Approach
Automated validation via `GlossaryAlignmentTests` in `Daiv3.Persistence.Tests`:

1. **XAML UI Labels Consistency**
   - Test: `XamlUiLabels_UseCanonicalGlossaryTerms()`
   - Validates: All 13 XAML files in `Daiv3.App.Maui` use canonical term spellings
   - Coverage: Pages, Styles, App definitions
   - Context-aware: Excludes code identifiers (namespaces, binding paths, class names)

2. **Specification Documents Consistency**
   - Test: `Documentation_UsesCanonicalGlossaryTerms()`
   - Validates: README files and all spec documents (`Docs/Requirements/Specs/*.md`) use canonical terms
   - Coverage: High-level design docs viewed by users
   - Context-aware: Excludes code blocks, inline code, package names, JSON keys

3. **Foundry Terminology Qualification**
   - Test: `XamlLabels_QualifyFoundryAsFoundryLocal()`
   - Validates: UI labels use "Foundry Local" not ambiguous "Foundry" shorthand
   - Exempts: Code identifiers and binding expressions

### Canonical Terms Validated
All 11 required terms from `Docs/Requirements/Glossary.md`:
- Chunk, Embedding, Foundry Local, MCP, NPU, ONNX Runtime, Learning Memory, RAG, SLM, Tier 1 / Tier 2, TensorPrimitives

### Acceptance Evidence
- **Automated tests**: 3 methods, all passing (part of 313/313 Persistence.Tests suite)
- **Terminology corrections applied**: 
  - XAML: "Foundry Models Directory" → "Foundry Local Models Directory" in `SettingsPage.xaml`
  - Documentation: Added backticks to code identifier references in `Solution-Structure.md` (lines 108, 114)
- **Continuous validation**: Tests run on every build, preventing regression

## Testing Plan
- Automated test matching the acceptance scenario.
- Manual verification checklist for UI or user flows.

## Testing Status
**3 automated acceptance tests passing** (313 total in Daiv3.Persistence.Tests)

### Test Verification Commands
```powershell
# Run all glossary validation tests
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --filter "FullyQualifiedName~Glossary" --nologo

# Run full test suite
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo
```

### Test Traceability
- ✅ **Acceptance Test 1**: `XamlUiLabels_UseCanonicalGlossaryTerms()` - Validates UI label terminology across 13 XAML files
- ✅ **Acceptance Test 2**: `Documentation_UsesCanonicalGlossaryTerms()` - Validates specification document terminology
- ✅ **Acceptance Test 3**: `XamlLabels_QualifyFoundryAsFoundryLocal()` - Validates Foundry qualification in UI labels

## Usage and Operational Notes

### For Developers
- Run glossary tests before committing UI or documentation changes
- Test failures provide clear violation messages with file path and line number
- Context-aware filtering prevents false positives from code identifiers

### For Reviewers
- Glossary alignment tests serve as automated acceptance gate
- Violations block merge until corrected
- Clear guidance provided for canonical term usage

### Acceptance Criteria Met
✅ **All public documentation uses glossary terms consistently**
- Automated validation confirms compliance across UI labels and specification documents
- Context-aware filtering distinguishes prose from code identifiers
- Continuous testing prevents regression

## Dependencies
- GLO-REQ-002: UI labels and documentation alignment implementation (COMPLETE)
- GLO-REQ-001: Canonical glossary source exists (COMPLETE)

## Related Requirements
- GLO-REQ-002: Parent requirement validated by this acceptance test
- GLO-NFR-001: Glossary updates should be backward compatible
