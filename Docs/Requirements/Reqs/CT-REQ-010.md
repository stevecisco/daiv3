# CT-REQ-010

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The system SHALL provide a System Admin Dashboard displaying real-time infrastructure metrics including CPU, GPU, NPU utilization, queue status, storage capacity, and agent workload.

## Detailed Scope

### Real-Time Infrastructure Metrics

#### CPU Monitoring
- **Overall CPU Usage:** % (0-100%) with colour gradient (green < 50%, yellow 50-75%, red > 75%)
- **Per-Core Breakdown:** Visual gauge for each CPU core showing % utilization
- **Process-Level Breakdown:**
  - MAUI app, background service, agent processes
  - Show top 5 CPU consumers
  - Percentage breakdown in list format
- **Thermal Status:** Current CPU temperature (if available), throttling indicator
- **Processor Info:** Core count, base/max frequency

#### GPU/NPU Monitoring
- **GPU Status:**
  - GPU % utilization (0-100%)
  - GPU memory used / total available
  - Dedicated GPU process list (which agents/services using GPU)
  - Memory allocation per process
  - GPU temperature (if available)
- **NPU Status:**
  - NPU availability (present/not present)
  - NPU % utilization (0-100%)
  - NPU memory used / available (if available)
  - Active execution provider indicator
- **Execution Provider Active:**
  - Visual indicator showing which provider is active (NPU | GPU | CPU)
  - Why indicator: automatic selection reason and override option

#### Memory Monitoring
- **System Memory:**
  - Physical RAM used / total (% utilized)
  - Available memory
  - Virtual memory usage
  - Threshold warning at >80% usage
- **Process Memory:**
  - MAUI app: RSS memory
  - Background service: RSS memory
  - Agent processes: RSS per agent
  - Memory trend (growing, stable, shrinking)

#### Storage Monitoring
- **Knowledge Base Storage:**
  - Embeddings database size
  - Documents storage size
  - Total knowledge base size
  - Available space on drive
- **Model Cache Storage:**
  - ONNX model directory size
  - Per-model breakdown
  - Last access time per model
- **Disk Alerts:**
  - Warning at <1GB free
  - Critical at <500MB free
  - Quick action: cleanup old models

#### Queue Status Summary
- **Current Queue Depth:** Total items by priority (critical/high/normal/low)
- **Current Processing:** Model + item being processed, elapsed time
- **Queue Age:** Oldest item in queue (how long waiting)
- **Throughput:** Items processed per minute (trend)
- **Average Wait Time:** By priority level
- **Bottleneck Indicator:** If queue growing over time

#### Agent Workload
- **Active Agents:** Count of currently running agents
- **Agents by State:**
  - Running (count + names)
  - Idle (count)
  - Blocked (count + reasons)
  - Error (count + error brief)
- **Agent Resource Usage:**
  - Memory per agent
  - CPU per agent
  - Thread count per agent
- **Task Assignment:** Which agents are assigned which projects/tasks

### Dashboard Layout Options

#### Single-View Layout (Recommended for MVP)
- **Top Row:** CPU, GPU/NPU, Memory (3-column gauge section)
- **Middle Section:** Storage breakdown + Queue status (2-column tile)
- **Bottom Section:** Agent workload list + Process breakdown (2-column)
- **Alerts Container:** Floating toast notifications for thresholds

#### Tabbed Layout (Enhanced Version)
- **Tab 1: CPU & Thermal** - Detailed CPU monitoring
- **Tab 2: GPU/NPU** - Graphics and NPU metrics
- **Tab 3: Memory** - Process-level memory breakdown
- **Tab 4: Storage** - Knowledge base + model cache
- **Tab 5: Queue & Agents** - Queue + workload overview

### Refresh Intervals & Performance
- **Metric Refresh Rate:** 2-5 second intervals (configurable, default 3 sec)
- **No UI Blocking:** All data fetching on background thread (CT-NFR-001)
- **Cache Strategy:** Show last known good state if data source slow
- **Stale Indicator:** Timestamp of last update; mark as stale after 30 sec

### Alerts & Thresholds (Configurable)
- **CPU Alert:** >85% sustained for >30 seconds
- **GPU Alert:** >90% utilization
- **Memory Alert:** >80% physical RAM used
- **Disk Alert:** <1GB free space
- **Queue Alert:** >100 items pending for >5 minutes
- **Thermal Alert:** CPU/GPU thermal throttling detected

### Interactive Features
- **Drill-Down:** Click metric to see trend chart (24h rolling window)
- **Process Sorting:** Sort by CPU%, memory%, elapsed time
- **Agent Details:** Click agent name to view current task, token usage, iterations
- **Quick Actions:** Kill process (confirmation), clear model cache, reprioritize queue
- **Export:** Export metrics snapshot as CSV or JSON for analysis

## Implementation Plan

### Data Collection
- **CPU/Memory:** Windows Performance Counters via `System.Diagnostics`
- **GPU/NPU:** NVIDIA CUDA queries (if GPU present) + DirectML provider status
- **Storage:** File system API for directory sizes
- **Queue:** ModelQueue service async queries
- **Agents:** Orchestration layer agent manager queries
- **Polling Service:** Background service with CancellationToken and timeout (5 sec)

### Data Contracts
```csharp
public record SystemMetrics(
    CpuMetrics Cpu,
    GpuMetrics Gpu,
    NpuMetrics Npu,
    MemoryMetrics Memory,
    StorageMetrics Storage,
    QueueMetrics Queue,
    List<AgentMetrics> Agents,
    DateTime LastUpdated
);

public record CpuMetrics(
    double OverallUtilization,
    List<double> PerCoreUtilization,
    double Temperature,
    bool ThermalThrottling
);
// Similar for other metrics...
```

### MAUI Implementation
- **AdminDashboard Page:** New page in App Shell
- **SystemMetricsViewModel:** Binds to UI, handles polling
- **ISystemMetricsService:** Interface for data collection
- **Charts:** Use MAUI.Controls default chart or 3rd-party (deferred if not available)
- **Gauges:** Circular progress indicators for CPU%, memory%, etc.

### CLI Implementation
- `daiv3 dashboard admin` - Display current metrics in tabular format
- `daiv3 dashboard admin --json` - Output raw metrics JSON
- `daiv3 dashboard admin --watch` - Continuous output with refresh every 3 seconds
- `daiv3 dashboard admin --history` - Show 24-hour trend data

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "System Admin Dashboard" brainstorming concept
- Real-time infrastructure visibility supports distributed multi-agent orchestration (Topic Area 1)
- Queue + agent workload tracking enables resource optimization (Topic Area 7)
- Complements CT-REQ-006 (agent-focused) with system-wide view

## Testing Plan
- Unit tests: Mock ISystemMetricsService; verify data binding
- Integration tests: Real Performance Counters access; verify accuracy vs. Task Manager
- Performance test: Polling 5+ metrics <1 sec refresh, no UI blocking
- Stress test: Dashboard open 24h, verify no memory leaks
- Accuracy test: Compare CPU%, memory% against official sources (Task Manager)
- Alert test: Simulate threshold conditions, verify toast notifications
- UI responsiveness: Rapid click/scroll while metrics updating

## Usage and Operational Notes
- Dashboard starts on app launch if enabled in settings
- Available for system administrators and developers
- Multiple metric views prevent single point of failure
- Responsive design works on desktop and tablet sizes
- Offline mode: Shows last known metrics, marked stale if >30 sec
- Archived metrics for trend analysis (24h rolling window, configurable)
- Settings: Configure refresh interval, alert thresholds, visible metrics

## Configuration

### Settings Schema (appsettings.json)
```json
{
  "Dashboard": {
    "AdminMetrics": {
      "Enabled": true,
      "RefreshIntervalSeconds": 3,
      "HistoryRetentionHours": 24,
      "Alerts": {
        "CpuThresholdPercent": 85,
        "MemoryThresholdPercent": 80,
        "DiskFreeThresholdMB": 1024,
        "QueueDepthThreshold": 100
      }
    }
  }
}
```

## Dependencies
- KLC-REQ-011 (MAUI framework for UI)
- HW-NFR-002 (performance metrics infrastructure)
- MQ-REQ-001 (model queue for queue status)
- AST-REQ-001 (agent manager for agent workload)
- CT-NFR-001 (async/dispatch patterns for real-time updates)

## Related Requirements
- CT-REQ-006 (agent activity + system metrics; CT-REQ-010 focuses on infrastructure)
- CT-REQ-004 (queue display; CT-REQ-010 provides system-wide queue context)
- CT-REQ-002 (settings for alert thresholds)
- CT-NFR-001 (performance requirements)
