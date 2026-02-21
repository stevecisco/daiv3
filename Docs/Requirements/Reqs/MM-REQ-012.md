# MM-REQ-012

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
The system SHALL track download progress and report completion percentage.

## Rationale
Long downloads can take minutes to hours. Users need real-time feedback to understand download status and estimate completion time.

## Implementation Plan
- Subscribe to HTTP response stream during download.
- Parse "Total ... %" style messages from Foundry service response.
- Report progress as percentage (0-100).
- Update at least once per second.
- On completion (100%), trigger post-download steps.
- On error, report percentage achieved and error details.

## Testing Plan
- Unit test to parse progress messages.
- Integration test with various file sizes.
- Network interruption simulation.
- Progress accuracy validation.

## Usage and Operational Notes
- User-visible: "Downloading: 45.2%"
- Should not block UI during download.
- Each update should show percentage.
- Cancellation should preserve partial download for resume.

## Dependencies
- MM-REQ-007 (download implementation)
- Foundry service with progress-reporting HTTP response.

## Related Requirements
- MM-REQ-013 (cancellation support)
- MM-ACC-005 (progress visibility)
