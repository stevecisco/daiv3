# MM-REQ-007

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL download models to the Foundry Local service's shared model cache directory (typically `%LOCALAPPDATA%/.foundry/cache/models`).

## Rationale
The Foundry Local service maintains its own model cache directory. Instead of creating a separate cache, integrate with it to avoid wasted disk space and simplify model management. The shared directory is obtained from the Foundry service's `/openai/status` endpoint.

## Implementation Plan
- Discover available models via `/foundry/list` endpoint (MM-REQ-001).
- User selects a model to download by alias or full name.
- Query Foundry service for cache directory path via `/openai/status` endpoint.
- Default to Foundry's standard location if not available.
- Validate directory exists and is writable before download.
- Use Foundry's `/openai/download` endpoint to download models into the cache.
- Report any permission or path issues clearly to the user.

## Testing Plan
- Unit test to verify path extraction from status response.
- Integration test to verify download into actual cache directory.
- Permission error handling test (read-only directory).
- Path validation test (invalid paths).

## Usage and Operational Notes
- Transparent to user: they never see or manage the directory directly.
- Foundry service must be running for downloads to proceed.
- Cache directory is typically: `C:\Users\<user>\AppData\Local\.foundry\cache\models\`
- User-visible: Download progress shown to user, final cache location not exposed.

## Dependencies
- KLC-REQ-005 (Foundry Local integration)
- Foundry service with `/openai/status` endpoint
- MM-REQ-012 (progress tracking)

## Related Requirements
- MM-REQ-008 (avoiding duplication)
- MM-REQ-009 (directory structure)
- MM-DATA-002 (directory naming convention)
