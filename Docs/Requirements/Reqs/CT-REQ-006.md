# CT-REQ-006

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display agent activity, iterations, token usage, and real-time system resource metrics (CPU, GPU, NPU utilization, memory, storage).

## Detailed Scope

### Agent Activity Display
- **Active Agents:** List of currently running agents with:
  - Agent name/ID
  - Current task/request
  - Status (idle, running, blocked, error)
  - Start time, elapsed time
  - Iterations completed on current task

### Agent Metrics per Agent
- **Token Usage:** Total tokens used in current task, daily total
- **Iteration Count:** Number of agent iterations (LM operations) in current task
- **Success Rate:** % of tasks completed successfully in session
- **Error Count:** Failed tasks, recoverable vs. fatal
- **Learning Captures:** Number of learnings captured this session

### System Resource Metrics (Real-Time)
- **CPU Utilization:**
  - Overall % (0-100%)
  - Per-core breakdown (visual gauge for each core)
  - Process-level breakdown by agent/service
  - Thermal status indicator

- **GPU/NPU Utilization:**
  - GPU % (0-100%)
  - GPU Memory used / available
  - Dedicated GPU process list
  - NPU availability status and utilization (if present)
  - NPU memory (if available)
  - Execution provider active (NPU, GPU, CPU)

- **Memory:**
  - System (physical RAM used / total)
  - Process breakdown (MAUI app, background service, agents)
  - Virtual memory (if applicable)

- **Storage:**
  - Knowledge base size (embeddings DB, documents)
  - Available disk space
  - Model cache storage
  - Threshold warning if <1GB free

### Resource Alerts
- **High CPU Alert:** Trigger at >85% sustained
- **High Memory Alert:** Trigger at >80% usage
- **Low Disk Alert:** Trigger at <1GB free
- **Thermal Alert:** If CPU/GPU thermal throttling detected

### Dual Dashboard Layout
- **Agent-Focused View:** Primary focus on agent iterations and token usage (role: orchestrator/developer)
- **System-Focused View:** Primary focus on resource metrics (role: admin/ops)
- **Tabbed or Side-by-Side:** User toggles between views or sees both

## Implementation Plan
- Query from orchestration layer for agent activity
- Query from Windows Performance Counters (or equivalent) for system metrics
- Data contract: AgentActivity + SystemMetrics
- Real-time updates via background polling or pub/sub
- MAUI dashboard with grid/chart visualization
- CLI commands:
  - `daiv3 dashboard agents` - agent activity
  - `daiv3 dashboard resources` - system metrics
  - `daiv3 dashboard summary` - both combined

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Agent + resource metrics together provide "System Admin Dashboard" brainstorming concept
- Real-time CPU/GPU/NPU metrics support distributed task orchestration (Topic Area 1)
- Token usage per agent enables financial tracking and profitability analysis (Topic Area 7)
- Thermal/resource alerting supports resource optimization and capacity planning

## Testing Plan
- Unit tests: AgentActivityViewModel, ResourceMetricsViewModel with mock data
- Integration tests: Dashboard with live agent execution and performance counter polling
- Performance: Metric refresh <1 sec, no UI blocking
- Stress test: Resource display with 10+ agents running
- Accuracy: Verify CPU%, memory% against Windows Task Manager
- Alert trigger testing: Simulate high utilization scenarios

## Usage and Operational Notes
- Agent view refreshes every 1 second (configurable)
- Resource metrics refresh every 2 seconds (configurable)
- Clicking agent: Show full task context, recent iterations, token breakdown
- Clicking metric: Show historical trend (last 24h or configurable)
- Right-click agent: "View Logs", "Pause", "Cancel", "Boost Priority"
- Resource alerts show toast notification in corner (dismissible)
- Offline mode: Shows cached metrics, marked as stale
- Data retained for trend analysis (24h rolling window)

## Dependencies
- KLC-REQ-011 (MAUI framework)
- ARCH-REQ-003 (agent manager, task orchestrator)
- HW-NFR-002 (performance metrics infrastructure)
- CT-NFR-001 (real-time updates without blocking UI)
- AST-REQ-001 (agent execution model)

## Related Requirements
- CT-REQ-004 (queue status; agents pull from queue)
- CT-REQ-007 (token budget; combined with usage here)
- CT-REQ-010 (system admin dashboard; resource metrics component)
- None
