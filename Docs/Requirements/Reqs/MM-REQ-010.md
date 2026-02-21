# MM-REQ-010

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL support downloading a specific model variant by alias and version.

## Rationale
Users may have specific version compatibility requirements or want a particular device variant. The download mechanism must support filtering by taxonomy.

## Implementation Plan
- Accept model identifier: alias (e.g., "phi-3") or full ID (e.g., "phi-3-mini:2").
- Accept optional version filter: "--version 2".
- Accept optional device filter: "--device GPU".
- Query catalog and filter candidates by these criteria.
- If multiple matches, select by version/device priority.
- Fail clearly if no match found.

## Testing Plan
- Unit test to filter catalog by alias, version, device.
- Integration test to download specific variants.
- Edge case: no matches, ambiguous matches, invalid filters.

## Usage and Operational Notes
- User-visible: "download phi-3 --version 2 --device GPU"
- Error messages should indicate why no match was found.
- Supports both shorthand (alias) and full ID syntax.

## Dependencies
- MM-REQ-001 (catalog listing)
- MM-REQ-010 (variant resolution)

## Related Requirements
- MM-REQ-011 (auto-selection when not specified)
- MM-REQ-027 (explicit variant selection)
