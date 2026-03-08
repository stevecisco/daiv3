# GLO-REQ-002

Source Spec: 14. Glossary - Requirements

## Requirement
UI labels and documentation SHALL align with glossary definitions.

## Implementation Status
**COMPLETE** (100%)

## Implementation Details

### Automated Consistency Validation
Created `GlossaryAlignmentTests.cs` in `Daiv3.Persistence.Tests` with three test methods:

1. **`XamlUiLabels_UseCanonicalGlossaryTerms()`**
   - Scans all XAML files in `Daiv3.App.Maui` for UI labels
   - Validates canonical term usage (e.g., "ONNX Runtime" not "OnnxRuntime")
   - Skips code identifiers (namespaces, binding paths, class names)
   - Flags non-canonical variants in user-visible labels

2. **`Documentation_UsesCanonicalGlossaryTerms()`**
   - Scans README files and specification documents (`Docs/Requirements/Specs/*.md`)
   - Validates prose terminology against glossary rules
   - Skips code blocks, inline code, package names, and dotted identifiers
   - Tracks code block boundaries to avoid false positives
   - Flags violations like "FoundryLocal" in prose (should be "Foundry Local")

3. **`XamlLabels_QualifyFoundryAsFoundryLocal()`**
   - Specifically validates "Foundry" references are qualified as "Foundry Local"
   - Ensures UI labels don't use ambiguous "Foundry" shorthand
   - Exempts code identifiers and binding expressions

### Term Corrections Applied
1. **XAML UI Label**: Changed "Foundry Models Directory:" to "Foundry Local Models Directory:" in `SettingsPage.xaml`
2. **Documentation Clarity**: Added backticks around code identifier references in `Solution-Structure.md` (lines 108, 114) to distinguish code from prose

### Excluded Contexts (By Design)
- **Code identifiers**: Project names, namespaces, class names (e.g., `Daiv3.FoundryLocal.Management`)
- **Package names**: NuGet package identifiers (e.g., `Microsoft.ML.OnnxRuntime.DirectML`)
- **JSON schema keys**: Data structure field names (e.g., `"tier1"`, `"tier2"`)
- **Code blocks**: Content between triple backticks or inline code (backticks)
- **Binding expressions**: XAML data binding paths and x:DataType declarations

### Coverage
- **XAML files**: All 13 XAML files in `Daiv3.App.Maui` (Pages, Styles, App definitions)
- **Documentation**: README.md and all specification documents in `Docs/Requirements/Specs/`
- **Glossary terms validated**: All 11 canonical terms plus common incorrect variants

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Testing Status
**3 automated tests created and passing** (312 total in Daiv3.Persistence.Tests)

### Test Verification Commands
```powershell
# Run glossary alignment tests specifically
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --filter "FullyQualifiedName~GlossaryAlignmentTests" --nologo

# Run full persistence unit test suite
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo
```

### Test Traceability
- ✅ **`XamlUiLabels_UseCanonicalGlossaryTerms`** - Validates UI label terminology (XAML files)
- ✅ **`Documentation_UsesCanonicalGlossaryTerms`** - Validates documentation prose terminology
- ✅ **`XamlLabels_QualifyFoundryAsFoundryLocal`** - Validates "Foundry" qualification in UI labels

## Usage and Operational Notes
- Tests run as part of standard unit test suite (no special configuration needed)
- Tests fail with clear violation messages showing file path, line number, and required correction
- False positives avoided through context-aware filtering (code vs. prose)
- Developers can run glossary alignment tests before committing UI/documentation changes

### Continuous Compliance
- Automated tests prevent regression of terminology inconsistencies
- Violations detected during PR builds
- Clear error messages guide developers to correct usage

## Dependencies
- GLO-REQ-001: Canonical glossary document exists (`Docs/Requirements/Glossary.md`)
- KLC-REQ-011: MAUI UI implementation complete (XAML files exist to validate)

## Related Requirements
- GLO-ACC-001: All public documentation uses glossary terms consistently (validated by these tests)
- GLO-NFR-001: Glossary updates should be backward compatible
