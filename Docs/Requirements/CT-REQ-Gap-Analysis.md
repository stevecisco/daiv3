# Phase 6 UI Requirements - Gap Analysis vs. Ideas-Organized-By-Topic

**Date:** March 4, 2026  
**Purpose:** Analyze brainstorming UI ideas against existing Phase 6 requirements and recommend updates

---

## Executive Summary

Current Phase 6 has 9 UI-related requirements (CT-REQ-001 through CT-REQ-009) covering:
- Settings storage and configuration UI
- Real-time transparency dashboard (generic)
- 6 specific dashboard views (queue, indexing, agent, budget, jobs, promotions)

Your brainstorming in Ideas-Organized-By-Topic Section 8 introduces **14 distinct UI components**, of which:
- **5 are partially covered** by existing requirements (need enhancement)
- **8 are not covered** and need new requirements
- **1 is a technical implementation detail** that should be elevated to NFR

---

## Detailed Gap Analysis

### ✅ COVERED (with possible enhancements)

#### 1. Queue/Priority Views
**Brainstorming Idea:** Top 3 items overall and by project, priority-sorted  
**Existing Coverage:** CT-REQ-004 (dashboard displays queue, current model, pending requests)  
**Gap:** Current requirement doesn't specify:
- "Top 3" prominent highlighting
- Per-project views
- Priority sorting (implied but not explicit)  
**Recommendation:** Enhance CT-REQ-004 with explicit "Top 3 queue view" and "per-project queue priorities"

#### 2. Performance Reporting / Token Usage  
**Brainstorming Idea:** Different formats and views for different stakeholders  
**Existing Coverage:** CT-REQ-006 (agent iterations, token usage), CT-REQ-007 (online token usage and budget)  
**Gap:** Current requirements don't specify:
- Multiple report formats (daily, weekly, summary)
- Role-specific views (user vs. admin)  
**Recommendation:** Enhance CT-REQ-006 and CT-REQ-007 with explicit requirement for "role-specific views" and add multi-format reporting guidance

#### 3. Indexing Progress  
**Brainstorming Idea:** File/Directory Explorer with indexed content, shareable status, machine location reference  
**Existing Coverage:** CT-REQ-005 (dashboard displays indexing progress, last scan time, errors)  
**Gap:** Current requirement doesn't specify:
- Visual file/directory browser interface
- Per-file status (indexed, pending, error)
- Shareable metadata display  
**Recommendation:** Enhance CT-REQ-005 with "indexed file browser" and file-level status indicators

#### 4. Knowledge Presentations  
**Brainstorming Idea:** Notebook-style Interface (OneNote/Notion-like) for organizing knowledge  
**Existing Coverage:** Loosely related to CT-REQ-005 (indexing) as source  
**Gap:** No explicit UI requirement for knowledge exploration/reading interface  
**Recommendation:** Could enhance CT-REQ-005 or create new CT-REQ-010 for "Knowledge Browser UI"

#### 5. Agent Activity  
**Brainstorming Idea:** Agent workload and system resource utilization (CPU/GPU/NPU)  
**Existing Coverage:** CT-REQ-006 (agent activity, iterations, token usage)  
**Gap:** Current requirement doesn't specify:
- System resource metrics (CPU%, GPU%, NPU%, Memory, Disk I/O)
- Agent workload visualization
- Real-time performance metrics  
**Recommendation:** Enhance CT-REQ-006 with explicit "System Resource Dashboard" section showing hardware utilization

---

### ❌ NOT COVERED (need new requirements)

#### 1. System Admin Dashboard  
**Brainstorming Content:**
- Real-time CPU/GPU/NPU utilization
- Queue status
- Storage sizes
- Agent workload  

**Why Needed:** Distributed system management requires real-time infrastructure visibility  
**Recommendation:** Create **CT-REQ-010: System Admin Dashboard** with:
- Real-time resource metrics (CPU, GPU, NPU utilization per core/device)
- Storage capacity and usage breakdown
- Background service status and health
- Queue depth and latency metrics  
**Dependencies:** HW-NFR-002 (performance metrics), MQ-REQ-001 (queue status), KM-REQ-001 (indexing metrics)  
**MVP Scope:** Real-time metrics updates (0.5-5 sec refresh); foundation for future distributed monitoring

#### 2. Project Master Dashboard  
**Brainstorming Content:**
- Nested project hierarchy with states and progress
- Status indicators per project/sub-project
- Multiple view hierarchies (by project, by priority, by agent)  

**Why Needed:** Your business model treats projects as entities with resource tracking; this is central to orchestration visibility  
**Recommendation:** Create **CT-REQ-011: Project Master Dashboard** with:
- Project tree view (hierarchical)
- State indicators (active, pending, completed, blocked)
- Progress bars and completion %
- Multiple pivot views (project tree, priority queue, agent assignment)
- Filter/search by project name, status, agent  
**Dependencies:** PTS-REQ-001 (project persistence), PTS-REQ-007 (scheduling)  
**MVP Scope:** Read-only dashboard; interactive project management deferred to Phase 7

#### 3. Background Service Inspector  
**Brainstorming Content:**
- View running services/processes
- Task state and progress
- Ability to cancel/stop tasks  

**Why Needed:** Essential for troubleshooting, task management, and resource cleanup; aligns with background processing architecture  
**Recommendation:** Create **CT-REQ-012: Background Service Inspector** with:
- List of running background tasks (name, start time, elapsed time, status)
- Task resource usage (memory, thread count)
- Cancellation capability for long-running tasks
- Process/terminal lifecycle visibility  
**Dependencies:** ARCH-REQ-003 (orchestration), background app architecture  
**MVP Scope:** View-only with basic cancellation; cleanup on completion not yet specified

#### 4. Time Tracking View  
**Brainstorming Content:**
- Per-agent, per-project rollups
- Time spent hierarchy (Project > Task > SubTask > Agent)
- Opportunity cost calculation
- Profitability metrics  

**Why Needed:** Your business model requires billing and resource allocation tracking; essential for financial dashboards  
**Recommendation:** Create **CT-REQ-013: Time Tracking & Profitability Dashboard** with:
- Hierarchical time view (Project > Task > Agent)
- Total time per project, task, agent
- Cost breakdown (API calls, compute hours, agent time)
- Profitability per project (cost vs. value/output)
- Trend analysis over time  
**Dependencies:** PTS-REQ-001 (projects), LM-REQ-006 (learning metrics), MQ-REQ-012 (budget tracking)  
**MVP Scope:** Time tracking only; cost attribution and profitability metrics in Phase 7

#### 5. Calendar / Reminders View  
**Brainstorming Content:**
- Upcoming deadlines
- Scheduled tasks
- Follow-up reminders
- Task dependencies  

**Why Needed:** Essential for task management and deadline awareness; particularly important for distributed agents working in background  
**Recommendation:** Create **CT-REQ-014: Calendar & Reminders Dashboard** with:
- Calendar view of upcoming deadlines
- Reminder alerts (visual, in-app)
- Deadline-driven task prioritization
- Dependency visualization (task A blocks task B)  
**Dependencies:** PTS-REQ-007 (scheduling), LM-REQ-009 (learning from patterns)  
**MVP Scope:** Basic calendar with deadline display; advanced scheduling and dependency graphs deferred

#### 6. Mind Map / Knowledge Relationship View  
**Brainstorming Content:**
- Related information visualization
- Concept mapping
- Knowledge interconnection display  

**Why Needed:** Bridges gap between raw search results and human understanding; useful for exploring knowledge domains  
**Recommendation:** Create **CT-REQ-015: Knowledge Graph Visualization** with:
- Mind map view of related documents/topics (based on embedding similarity)
- Interactive concept exploration
- Tag/category clustering
- Related topic suggestions  
**Dependencies:** KM-REQ-012 (semantic search), ARCH-NFR-002 (knowledge graph extensibility)  
**MVP Scope:** Static mind map from Tier 2 search results; interactive graph deferred to Phase 7+

#### 7. Document Generation & Export  
**Brainstorming Content:**
- PowerPoint generation
- Excel generation
- Office document creation  

**Why Needed:** Enterprise feature for sharing knowledge and reports; supports knowledge distribution and learning promotion  
**Recommendation:** Defer to **Phase 7+ or FUT-REQ-008** (not critical for MVP)  
**Alternative:** Add as enhancement to "export search results" (CT-REQ-005 enhancement) for PDF/CSV first

#### 8. Image Display & Annotation  
**Brainstorming Content:**
- Screenshot/image display
- Visual annotation/markup
- Evidence attachment  

**Why Needed:** Essential for visual work (design, UI testing, screenshots); supports rich content knowledge storage  
**Recommendation:** Defer to **Phase 7+ or FUT-REQ-009** (requires image processing and storage)  
**Alternative:** Add as enhancement for embedding image metadata in knowledge documents (Phase 6.1)

---

## Technical Implementation Details (NFRs)

### 1. Async/Dispatch Patterns  
**Brainstorming Content:** Keep UI responsive with proper threading and cancellation  
**Current Coverage:** CT-NFR-001 (dashboard should update in near real-time without blocking UI)  
**Recommendation:** Enhance **CT-NFR-001** with explicit guidance:
- Dashboard updates on background thread (don't block UI)
- CancellationToken support for all long-running operations
- Debouncing for high-frequency metrics (e.g., don't update every 100ms, use 500-1000ms)
- Connection pooling for data sources to avoid connection exhaustion  

### 2. Responsive UI Patterns  
**Brainstorming Content:** Proper threading and cancellation for UI responsiveness  
**Recommendation:** Create **CT-NFR-003: UI Responsiveness** with:
- All dashboard data fetching on background thread
- Result marshaling to UI thread via Dispatcher
- Timeout for data fetches (default 5 sec, configurable)
- Graceful degradation if data source is slow (show cached/stale data)  

---

## Summary of Recommendations

### Enhance Existing CT Requirements
1. **CT-REQ-004:** Add explicit "Top 3 queued items" view and per-project queue filtering
2. **CT-REQ-005:** Add visual file browser with per-file status indicators
3. **CT-REQ-006:** Add system resource metrics (CPU, GPU, NPU, memory) to agent activity dashboard
4. **CT-REQ-007:** Unchanged (already covers budget transparency well)
5. **CT-NFR-001:** Enhance with debouncing, thread safety, and timeout guidance

### Create New CT Requirements
| ID | Title | Scope | MVP | Phase |
|----|-------|-------|-----|-------|
| CT-REQ-010 | System Admin Dashboard | Infrastructure metrics, resource utilization | Yes | 6 |
| CT-REQ-011 | Project Master Dashboard | Project hierarchy, states, progress | Yes | 6 |
| CT-REQ-012 | Background Service Inspector | Task list, cancellation, lifecycle | Yes | 6 |
| CT-REQ-013 | Time Tracking Dashboard | Project/task/agent time rollups | Partial | 6/7 |
| CT-REQ-014 | Calendar & Reminders | Deadlines, task schedules | Partial | 6/7 |
| CT-REQ-015 | Knowledge Graph Visualization | Related documents, concept maps | No | 7+ |
| (Future) | Document Generation | PDF/PowerPoint/Excel export | No | 7+ |
| (Future) | Image Annotation | Screenshots, visual markup | No | 7+ |

### Create/Enhance NFRs
1. **Enhance CT-NFR-001** with debouncing, thread safety, timeouts
2. **Create CT-NFR-003:** UI Responsiveness and thread marshaling patterns

---

## Implementation Priority (MVP for Phase 6)

### Must Have (P1)
1. **CT-REQ-003** - Dashboard foundation (already exists, needs expansion)
2. **CT-REQ-004** - Queue view with top-3 highlighting
3. **CT-REQ-005** - Indexing view with file browser
4. **CT-REQ-006** - Agent activity with system metrics
5. **CT-REQ-010** (New) - System Admin Dashboard
6. **CT-REQ-011** (New) - Project Master Dashboard

### Should Have (P2)
7. **CT-REQ-012** (New) - Service Inspector
8. **CT-REQ-013** (New) - Time Tracking (time tracking only, no cost attribution)
9. **CT-REQ-014** (New) - Calendar basics

### Nice to Have (P3)
10. **CT-REQ-015** (New) - Knowledge Graph (static, read-only)
11. Document/Image features → Phase 7+

---

## Next Steps

1. **Review & Approve** this gap analysis
2. **Create new CT-REQ requirements** (CT-REQ-010 through CT-REQ-015) with full implementation details
3. **Update existing CT-REQ files** (CT-REQ-004, CT-REQ-005, CT-REQ-006) with enhancements
4. **Update CT-NFR-001** with technical guidance
5. **Update Master-Implementation-Tracker** to reflect new requirements and dependencies
6. **Revise Phase 6 specification** (Specs/11-Configuration-Transparency.md) to incorporate new dashboard components

