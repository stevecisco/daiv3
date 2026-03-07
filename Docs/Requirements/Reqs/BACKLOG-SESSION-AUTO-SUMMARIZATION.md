# BACKLOG: Session Auto-Summarization & Knowledge Integration (v0.2+)

**Status:** Backlog (v0.2+)  
**Last Updated:** March 7, 2026  
**Priority:** High (enables learning loop automation and knowledge propagation)

---

## Overview

DAIv3 has complete infrastructure for capturing learnings from agent execution and promoting them across scopes (AST-REQ-001, LM-REQ-001, KBP-REQ-002). However, **automatic session summarization and integration into the knowledge backpropagation pipeline is deferred** to enable MVP v0.1 completion.

This backlog tracks the future work needed to automatically:
1. Summarize execution sessions into structured knowledge artifacts
2. Populate `sessions.key_knowledge_json` with important facts learned
3. Trigger learning creation from implicit self-correction patterns
4. Display agent learning context breadcrumbs in UI/logs

---

## Related Completed Requirements

| Requirement | Status | Provides |
|-------------|--------|----------|
| [AST-REQ-001](AST-REQ-001.md) | ✅ 100% | Agent execution with `ExecutionId`, step history, termination reason; foundation for summarization |
| [LM-REQ-001](LM-REQ-001.md) | ✅ 100% | Learning creation from 6 trigger types; SelfCorrectionTriggerContext already defined |
| [LM-REQ-005](LM-REQ-005.md) | ✅ 100% | Learning retrieval and injection into prompts; learnings already in agent context |
| [KBP-REQ-004](KBP-REQ-004.md) | ✅ 100% | Template-based knowledge summarization; facade ready for integration |
| [CT-REQ-003](CT-REQ-003.md) | ✅ 100% | Dashboard infrastructure; unblocks dashboard promotion visibility |

---

## Future Requirements (v0.2+)

### BACKLOG-01: Session Auto-Summarization Trigger

**Reference:** AST-REQ-001  
**Depends On:** AST-REQ-001 (Complete), LM-REQ-001 (Complete)  
**Priority:** High  
**Effort:** 5-8 story points

#### Description
After each agent execution (ExecutionId completes), automatically generate a structured summary capturing:
- Top 3 key milestones achieved in the execution
- Learnings triggered (if any via LM-REQ-001)
- Success criteria evaluation result
- Critical decisions made (captured in plan/execute steps)
- Error patterns encountered

#### Acceptance Criteria
1. `ISessionSummarizationService` interface created with `SummarizeExecutionAsync(ExecutionId)` method
2. Returns `SessionSummary` with: title, milestones, learnings_triggered, success_eval, errors, duration
3. Summary is automatically triggered after each `AgentManager.ExecuteTaskAsync()` completion
4. Integration with `LM-REQ-001` to list learnings created during execution
5. CLI command: `daiv3 session summarize <execution-id>` displays summary
6. Dashboard: Session view displays auto-generated summary
7. Configurable via `OrchestrationOptions.EnableAutoSessionSummarization` (default: true)

#### Implementation Notes
- **Data Source:** `AgentExecutionResult` from AST-REQ-001 (steps, iterations, tokens, termination)
- **Learning List:** `LearningService.GetLearningsBySourceTaskAsync(execution_id)`
- **Summary Template:** Leverage existing `IKnowledgeSummaryService` from KBP-REQ-004
- **Storage:** `sessions.key_knowledge_json` (JSON blob with milestone/learning/error arrays)
- **Observability:** `ISessionObserver` event pattern (parallel with LM-NFR-002)

---

### BACKLOG-02: Dashboard Promotion History Visibility (Now Unblocked)

**Reference:** KBP-ACC-002  
**Depends On:** KBP-ACC-002 (Complete), CT-REQ-003 (✅ Now Complete)  
**Priority:** Medium  
**Effort:** 3-5 story points  
**Status:** ⏸️ Was blocked by CT-REQ-003; now **READY TO IMPLEMENT**

#### Description
KBP-ACC-002 completed CLI visibility (`promotion-history` commands). This backlog item adds MAUI dashboard view for promotion visibility, now that `CT-REQ-003` (Dashboard infrastructure) is complete.

#### Acceptance Criteria
1. **MAUI Dashboard Page:** `KnowledgePromotionPage.xaml` displays:
   - Recent promotions (last 50, paginated)
   - Filters: by-scope, by-source-task, by-learner, date range
   - Visualization: timeline of promotions with promotion path per learning
   - Summary stats: total promotions, by-scope distribution, by-trigger distribution
2. **ViewModel:** `KnowledgePromotionViewModel` with:
   - `RecentPromotions` ObservableCollection
   - Filter commands: `FilterByScopeCommand`, `FilterByForceCommand`, etc.
   - Pagination logic
3. **Data Integration:** Leverage existing `PromotionRepository` queries from KBP-DATA-001
4. **Navigation:** Add to `Shell.xaml` under Dashboard section
5. **Metrics:** Track promotion view engagement via `IPromotionObserver` (new)

#### Implementation Notes
- **Reuse existing CLI commands:** `PromotionHistoryCliModels` can be shared with MAUI
- **Timeline Visualization:** Use StackLayout with datetime grouping (daily/weekly rollups)
- **Performance:** Load incrementally; 50 per page with date range query limits
- **Reference:** Parallel pattern from CT-REQ-004 (queue dashboard) and CT-REQ-008 (scheduled jobs)

---

### BACKLOG-03: Session Key Knowledge Auto-Population

**Reference:** KM-DATA-001  
**Depends On:** BACKLOG-01 (Session Auto-Summarization)  
**Priority:** High  
**Effort:** 4-6 story points

#### Description
Populate `sessions.key_knowledge_json` field automatically with important structured facts from execution:
- Milestones completed
- Learnings created (title + confidence + trigger type)
- Error patterns (category, frequency, resolution)
- Model/tool usage statistics
- External resources accessed (web fetches, API calls)

#### Acceptance Criteria
1. `ISessionKnowledgeExtractor` interface with `ExtractKeyKnowledgeAsync(ExecutionId)` method
2. Returns JSON object: `{ milestones: [], learnings: [], errors: [], resources: [] }`
3. Automatically called after execution completes via `AgentManager` integration
4. Stored in `sessions.key_knowledge_json` BLOB
5. CLI: `daiv3 session inspect <session-id> --key-facts` displays extracted facts
6. Dashboard: Session detail view shows key facts with icons/badges
7. Backward compatible: null field allowed for sessions without population

#### Implementation Notes
- **Extractor Sources:**
  - Milestones: Parse `AgentExecutionStep` descriptions (NLP or regex templates)
  - Learnings: Query `LearningService.GetLearningsBySourceTaskAsync()`
  - Errors: Parse `AgentExecutionStep.Output` for patterns (regex templates per error type)
  - Resources: Query `WebFetchRepository.GetBySourceTaskAsync()`, `ModelQueueRepository.GetByTaskAsync()`
- **Storage Schema:** JSON with typed arrays for queryability
- **Observability:** Log extraction success/failure per execution

---

### BACKLOG-04: Auto-Learning Trigger from Agent Failures

**Reference:** LM-REQ-001, AST-REQ-001  
**Depends On:** LM-REQ-001 (Complete), AST-REQ-001 (Complete)  
**Priority:** Medium  
**Effort:** 6-8 story points

#### Description
Currently, **SelfCorrectionTriggerContext** is defined in LM-REQ-001 but not automatically triggered when an agent fails and retries internally (AST-REQ-001 supports `EnableSelfCorrection` by default).

This backlog item automates learning capture from self-correction patterns:
- When `AgentExecutionStep[N]` fails and `AgentExecutionStep[N+1]` succeeds (iteration corrects prior failure)
- Capture: failed action, error message, corrective action taken, resolution
- Create learning via `LearningService.CreateSelfCorrectionLearningAsync()`

#### Acceptance Criteria
1. `ISelfCorrectionLearningDetector` interface analyzes `AgentExecutionResult` for retry patterns
2. Implements pattern matching: step N fails → step N+1 (correcting step type) succeeds
3. Automatically called after execution completes (configurable via `OrchestrationOptions`)
4. Creates learning with:
   - Title: "Self-corrected {error_category} in {task_context}"
   - Trigger: SelfCorrection (confidence 0.8 per LM-REQ-001)
   - Source: execution_id + step numbers (N, N+1)
5. CLI: `daiv3 learning list --trigger self-correction` shows auto-detected learnings
6. Dashboard: Agent learning history shows auto-triggered learnings with "Self-correction" badge
7. Configurable pattern matching: customizable error patterns and resolution templates

#### Implementation Notes
- **Pattern Detection:** Step state machine: [Failed] → [Planning/Retry] → [Success]
- **Error Categories:** Parse error messages against known patterns (compilation, tool failure, timeout, etc.)
- **Confidence Adjustment:** Vary based on error complexity (simple fixes: 0.85, complex patterns: 0.6)
- **Deduplication:** Check if identical error+resolution already learned (via embedding similarity)
- **Performance:** Light-weight pattern matching; skip on ExecutionResult.TerminationReason != "Success" to ignore incomplete executions

---

### BACKLOG-05: Learning Injection Breadcrumb Logging

**Reference:** LM-REQ-005, CT-REQ-006  
**Depends On:** LM-REQ-005 (Complete), CT-REQ-006 (Complete)  
**Priority:** Medium  
**Effort:** 3-4 story points

#### Description
LM-REQ-005 injects retrieved learnings into agent prompts. However, **why** learnings were injected/excluded/scored is not currently visible to end users.

This backlog item adds breadcrumb logging to show:
- Which learnings were candidates (count, confidence scores)
- Which learnings passed filters (similarity, confidence thresholds)
- Which learnings were injected into final prompt (count, order, injection reason)
- Why learnings were excluded (below threshold, wrong scope, etc.)

#### Acceptance Criteria
1. `ILearningInjectionBreadcrumb` interface captures injection decision details
2. Populated during `LearningRetrievalService.RetrieveAsync()` and `AgentManager.RetrieveAndFormatLearningsAsync()`
3. Stored as JSON in `AgentExecutionStep.Output` or separate breadcrumb field
4. CLI: `daiv3 agent inspect <execution_id> --learning-trace` shows injection decisions
5. Dashboard: Agent activity view shows learning source badge on each step
6. Breadcrumb includes: learning_id, similarity_score, confidence_score, passed_filters, injection_reason
7. Configurable via `OrchestrationOptions.EnableLearningBreadcrumbs` (default: true in debug, false in release)

#### Implementation Notes
- **Minimal Overhead:** Breadcrumbs only populated if enabled (gate on debug/config)
- **Storage:** Side-car JSON file per execution: `{LOCALAPPDATA}\Daiv3\traces\{execution_id}-learning-breadcrumb.json`
- **Dashboard Access:** Load on-demand when user clicks "Show Learning Trace" link
- **Observability Hook:** Integrate with `ILearningObserver` from LM-NFR-002

---

### BACKLOG-06: Agent Learning History UI

**Reference:** AST-REQ-001, LM-REQ-005, CT-REQ-006  
**Depends On:** BACKLOG-01, BACKLOG-02, BACKLOG-05  
**Priority:** Low (nice-to-have for Phase 6.3)  
**Effort:** 8-10 story points

#### Description
Provide MAUI dashboard view showing what learnings an agent has used across all executions:
- Learning reuse count (how many times injected)
- Learning effectiveness score (based on whether step succeeded after injection)
- Learning source (auto-triggered vs user-promoted)
- Learning history per agent

#### Acceptance Criteria
1. **MAUI Dashboard Page:** `AgentLearningHistoryPage.xaml` displays:
   - Stats: total learnings used, average effectiveness, most-used learning
   - Table: learnings with reuse count, effectiveness score, creation date, source badge
   - Filters: by-trigger-type, by-scope, by-date-range
   - Detail: Click learning to show injection history (which executions used it)
2. **ViewModel:** `AgentLearningHistoryViewModel` with:
   - `AgentId` property (selected agent)
   - `UsedLearnings` ObservableCollection
   - Filter/sort commands
3. **Data Integration:**
   - Query: `LearningRepository.GetByAgentAndScopeAsync(agent_id, scope)`
   - Metrics: `LearningMetricsCollector.GetMetricsByAgentAsync(agent_id)`
4. **Navigation:** Add to Dashboard → Agent Activity section
5. **Breadcrumb:** Show which execution used each learning via learning_breadcrumb side-car files

#### Implementation Notes
- **Effectiveness Score:** `(successful_subsequent_steps / total_injections) * injected_learning.confidence`
- **Lazy Load Details:** Load full injection history on-demand (pagination for large datasets)
- **Reuse Visualization:** Sparkline chart showing usage over time
- **Reference Architecture:** Similar to Kubernetes resource usage dashboards (familiar pattern)

---

## Backlog Work Order

**Recommended Implementation Sequence (v0.2 Roadmap):**

1. **Phase 6.3 Sprint 1:** BACKLOG-01 (Session Auto-Summarization)
   - Unblocks BACKLOG-03, improves dashboard context
   - Estimated: 1 sprint (5-8 pts)

2. **Phase 6.3 Sprint 1-2:** BACKLOG-02 (Dashboard Promotion History) + BACKLOG-05 (Breadcrumb Logging)
   - Low-hanging fruit; parallel work; unblocked now
   - Estimated: 1.5 sprints (6-9 pts combined)

3. **Phase 6.3 Sprint 2:** BACKLOG-03 (Session Key Knowledge Auto-Population)
   - Depends on BACKLOG-01; enables rich session inspection
   - Estimated: 1 sprint (4-6 pts)

4. **Phase 7 Sprint 1:** BACKLOG-04 (Auto-Learning Trigger from Failures)
   - Advanced learning automation; lower priority for MVP
   - Estimated: 1.5 sprints (6-8 pts)

5. **Phase 7 Sprint 2+:** BACKLOG-06 (Agent Learning History UI)
   - Polish/nice-to-have; completes learning reflection story
   - Estimated: 2 sprints (8-10 pts)

---

## Integration Points

All backlog items integrate with existing systems:

| System | Integration Point | Requirement |
|--------|------------------|-------------|
| **Agent Execution** | `AgentManager.ExecuteTaskAsync()` return path | AST-REQ-001 |
| **Learning Creation** | `LearningService.CreateAsync()` family | LM-REQ-001 |
| **Learning Retrieval** | `LearningRetrievalService.RetrieveAsync()` | LM-REQ-005 |
| **Dashboard** | `IDashboardService.CollectAsync()` + ViewModel binding | CT-REQ-003 |
| **Persistence** | `sessions` + `learnings` + `promotions` tables | KM-DATA-001, LM-DATA-001, KBP-DATA-001 |
| **Observability** | Observer pattern (ISessionObserver, IPromotionObserver) | LM-NFR-002 |

---

## Architectural Decisions

### Auto-Summarization Scope
- **In Scope:** Milestone/learning/error extraction from logs; JSON storage
- **Out of Scope (Phase 6.3+):** SLM-based abstractive summarization (blocked on Foundry Local); narrative generation

### Learning Trigger Automation
- **Design:** Non-blocking, opt-in via configuration
- **Default:** Enabled in debug; configurable for production
- **Failure Mode:** Errors in auto-learning don't prevent execution completion (gate with try-catch + telemetry)

### Dashboard Visibility
- **Phase 6.2 Priority:** KBP-ACC-002 dashboard (high immediately)
- **Phase 6.3 Priority:** Learning history + breadcrumb traces (polish)
- **Performance:** Implemented with pagination/lazy-loading to avoid UI jank

---

## Future Expansion Hooks

These backlog items are designed to enable future capabilities:

1. **Knowledge Graph Integration (FUT-REQ-002):** Session summaries + learning provenance → graph construction
2. **Multi-User Organizational Learning (FUT-REQ-004):** Session summaries + promotion history → team dashboards
3. **Learning Effectiveness Analytics (v0.3+):** Breadcrumb traces → identify which learnings drive better outcomes
4. **Skill Marketplace Metadata (FUT-REQ-003):** Session auto-summaries → curated skill descriptions

---

## References

- [AST-REQ-001: Agent Execution](AST-REQ-001.md)
- [LM-REQ-001: Learning Creation](LM-REQ-001.md)
- [LM-REQ-005: Learning Retrieval & Injection](LM-REQ-005.md)
- [KBP-REQ-004: Knowledge Summarization](KBP-REQ-004.md)
- [KBP-ACC-002: Promotion Visibility](KBP-ACC-002.md)
- [CT-REQ-003: Dashboard Foundation](CT-REQ-003.md)

