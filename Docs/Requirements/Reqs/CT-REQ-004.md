# CT-REQ-004

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display model queue status, current model, and pending requests by priority, with prominent highlighting of the top 3 queued items and per-project queue views.

## Detailed Scope

### Core Queue Display
- **Current Model:** Display the model currently executing (name, type: local/online, start time)
- **Queue Status:** Total pending requests, count by model affinity, count by priority
- **Pending Requests:** List of next N requests with:
  - Request ID
  - Priority level (color-coded: critical/high/normal/low)
  - Model required/preferred
  - Estimated wait time
  - Requester/agent name

### Top 3 Priority Queue View
- **Prominent Display:** Top 3 queued items shall be visually prominent (larger, highlighted)
- **Details per item:**
  - Priority badge (color-coded)
  - Brief description of request
  - Estimated start time
  - Visual progress indicator if not yet processing

### Per-Project Queue Filtering
- **Project Filter:** Users can filter queue view by project
- **Per-Project Queue Stats:** Show queue depth per project
- **Priority Distribution:** Visualization of priority distribution within selected project

### Queue Performance Metrics
- **Average Wait Time:** Per priority level
- **Queue Throughput:** Requests processed per minute
- **Model Utilization:** Percent time each model is busy vs. idle

## Implementation Plan
- Query from ModelQueue service (async polling or pub/sub)
- Data contract: QueueStatus (current model, pending items, metrics)
- Filter logic for project-scoped views
- MAUI dashboard binding to ViewModel
- CLI command: `daiv3 dashboard queue` with tabular output

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Queue display aligns with "Queue/Priority Views" brainstorming item
- Top 3 highlighting supports prioritization and quick decision-making
- Per-project views support multi-project orchestration scenarios
- Async/dispatch patterns (UI responsiveness) covered by CT-NFR-001

## Testing Plan
- Unit tests: QueueViewModel with mock ModelQueue data
- Integration tests: Dashboard with live ModelQueue
- UI tests: Verify top 3 highlighting, filter behavior
- Performance: Queue refresh <1 sec for 1000+ items

## Usage and Operational Notes
- Queue view refreshes every 1-2 seconds (configurable)
- Clicking a queued item shows full request details
- Right-click on queued item: "View Request Details", "Reprioritize", "Cancel" (if applicable)
- Offline mode: Shows cached queue state, marks as stale
- Budget impact: Only display, no API calls required

## Dependencies
- KLC-REQ-011 (MAUI framework)
- MQ-REQ-001 (model queue persistence)
- MQ-REQ-006 (queue batching by model affinity)
- CT-NFR-001 (real-time updates without blocking UI)

## Related Requirements
- CT-REQ-008 (scheduled jobs view)
- MQ-ACC-002 (queue acceptance criteria)
