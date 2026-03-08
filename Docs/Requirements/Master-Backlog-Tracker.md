# Master Backlog Tracker

**Purpose:** Track deferred requirements that are intentionally out of scope for MVP v0.1 and planned for v0.2+, Phase 7+, or future consideration. This file contains requirements that have been identified but are not currently part of active development planning.

**Master Implementation Tracker:** [Master-Implementation-Tracker.md](Master-Implementation-Tracker.md) tracks all active requirements (Not Started with near-term priority, In Progress, Complete).

---

## Key Documentation

- [Master Implementation Tracker](Master-Implementation-Tracker.md) - Active requirements tracking
- [Glossary](Glossary.md) - Canonical terminology (v1.0)
- [Implementation Plan](Implementation-Plan.md) - Development phases and roadmap
- [Design Document](Daiv3_Design_Document.md) - Full system architecture

---

## Summary Statistics

**Total Backlog Requirements:** 26  
**Status Breakdown:**
- **Not Started:** 25 (96%)
- **Backlog:** 1 (4%)

**Backlog Breakdown by Category:**
- **Embedding Model Management:** 5 requirements (v0.2-v0.3)
- **Session Auto-Summarization:** 6 requirements (v0.2 P6.3/P7.1)
- **Knowledge Management Extensions:** 2 requirements (Phase 7+, HNSW scaling)
- **Future Considerations:** 9 requirements (Future exploration)
- **Advanced Features:** 4 requirements (Distributed systems, accessibility, voice)

---

## Future Scalability & Performance Enhancements

Requirements for advanced scalability and performance optimization beyond MVP.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 57 | [KM-NFR-002](Reqs/KM-NFR-002.md) | [4. Knowledge Management & Indexing](Specs/04-Knowledge-Management-Indexing.md) | The system SHOULD be able to scale to HNSW indexing later. | KM-REQ-012 | Not Started | 0% | Future scalability path - HNSW approximate nearest neighbor for large corpora (10K+ documents) |

---

## Embedding Model Management (v0.2-v0.3)

Deferred requirements for configurable embedding model discovery, selection, and plugin system.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 40.1 | [KM-EMB-MODEL-001](Reqs/KM-EMB-MODEL-001.md) | [4. Embedding Model Management](Specs/04-Embedding-Model-Management.md) | Registry of embedding models with metadata and tokenizer alignment. | KM-REQ-013 | Not Started | 0% | Embedding model registry (v0.2) - Foundation for model management |
| 40.2 | [KM-EMB-MODEL-002](Reqs/KM-EMB-MODEL-002.md) | [4. Embedding Model Management](Specs/04-Embedding-Model-Management.md) | Discover local embedding models and download from Azure Blob Storage on first initialization. | KM-EMB-MODEL-001 | Not Started | 0% | Model discovery and download (v0.1: Azure, v0.2+: Hugging Face) |
| 40.3 | [KM-EMB-MODEL-003](Reqs/KM-EMB-MODEL-003.md) | [4. Embedding Model Management](Specs/04-Embedding-Model-Management.md) | Select active embedding models for Tier 1 and Tier 2 with settings persistence. | KM-EMB-MODEL-001, KM-EMB-MODEL-002 | Not Started | 0% | Model selection and settings (v0.2) |
| 40.5 | [KM-EMB-MODEL-CATALOG](Reqs/KM-EMB-MODEL-CATALOG.md) | [4. Embedding Model Management](Specs/04-Embedding-Model-Management.md) | Catalog-driven model discovery with metadata, versions, and download URLs. Extends KM-EMB-MODEL-002. | KM-EMB-MODEL-002 | Backlog | 0% | Plugin Foundation: JSON catalog, model discovery, caching (v0.2) |
| 40.6 | [KM-EMB-MODEL-PLUGIN-SYSTEM](Reqs/KM-EMB-MODEL-PLUGIN-SYSTEM.md) | [4. Embedding Model Management](Specs/04-Embedding-Model-Management.md) | Dynamic DLL-based tokenizer plugin loading system. Extends KM-EMB-MODEL-CATALOG and KM-EMB-MODEL-TOKENIZER. | KM-EMB-MODEL-CATALOG, KM-EMB-MODEL-TOKENIZER | Not Started | 0% | Full Plugin System: DLL loading, plugin discovery, version management (v0.3) |

---

## Session Auto-Summarization & Knowledge Integration (v0.2+)

**Purpose:** Automated session summarization, key knowledge extraction, learning automation, and dashboard visibility enhancements deferred to v0.2+ for Phase 6.3/7.1 completion.

**Rationale:** Foundation requirements (AST-REQ-001, LM-REQ-001, CT-REQ-003) are complete. These advanced automation and UI polish features are deferred to focus on core MVP functionality.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 212.1 | [BACKLOG-01: Session Auto-Summarization Trigger](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [9. Learning Memory](Specs/09-Learning-Memory.md) | Automatically generate structured summary after agent execution capturing milestones, learnings, errors, decisions, and success criteria results. | AST-REQ-001✅, LM-REQ-001✅ | Not Started | 0% | Unblocks: BACKLOG-03 (session key facts), dashboard session context. Effort: 5-8 pts. Integration: `AgentManager.ExecuteTaskAsync()` completion → `ISessionSummarizationService.SummarizeExecutionAsync()` → `sessions.key_knowledge_json` population (v0.2 P6.3) |
| 212.2 | [BACKLOG-02: Dashboard Promotion History Visibility](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [11. Configuration & User Transparency](Specs/11-Configuration-Transparency.md) | Add MAUI dashboard view for promotion visibility (CLI commands already complete via KBP-ACC-002). NOW UNBLOCKED: CT-REQ-003 (dashboard foundation) is complete. | KBP-ACC-002✅, CT-REQ-003✅ | Not Started | 0% | Priority: Medium-High (dashboard infrastructure ready). Effort: 3-5 pts. Simple dashboard widget - add PromotionHistoryViewModel + summary card to DashboardPage.xaml (v0.2 P6.2) |
| 212.3 | [BACKLOG-03: Session Key Knowledge Auto-Population](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [4. Knowledge Management & Indexing](Specs/04-Knowledge-Management-Indexing.md) | Auto-populate `sessions.key_knowledge_json` with structured facts: milestones, learnings, error patterns, resource usage, external data sources. | BACKLOG-01 | Not Started | 0% | Effort: 4-6 pts. Requires BACKLOG-01 for summary context. Enables rich session inspection and dashboard summarization. Integration: Call `ISessionKnowledgeExtractor.ExtractKeyKnowledgeAsync()` after execution completes (v0.2 P6.3) |
| 212.4 | [BACKLOG-04: Auto-Learning Trigger from Agent Failures](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [9. Learning Memory](Specs/09-Learning-Memory.md) | Auto-detect self-correction patterns (step N fails → step N+1 succeeds) and create learnings via LM-REQ-001 SelfCorrectionTriggerContext without manual user action. | LM-REQ-001✅, AST-REQ-001✅ | Not Started | 0% | Priority: Medium (automation feature). Effort: 6-8 pts. Advanced learning automation; deferred for Phase 7 (v0.2 P7.1) |
| 212.5 | [BACKLOG-05: Learning Injection Breadcrumb Logging](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [11. Configuration & User Transparency](Specs/11-Configuration-Transparency.md) | Track and display why learnings were/weren't injected: candidate count, similarity scores, filter decisions, injection reasons per step. | LM-REQ-005✅, CT-REQ-006✅ | Not Started | 0% | Priority: Medium (observability enhancement). Effort: 3-4 pts. CLI: `daiv3 agent inspect <execution_id> --learning-decisions` (v0.2 P6.2) |
| 212.6 | [BACKLOG-06: Agent Learning History UI](Reqs/BACKLOG-SESSION-AUTO-SUMMARIZATION.md) | [11. Configuration & User Transparency](Specs/11-Configuration-Transparency.md) | MAUI dashboard view: per-agent learning reuse metrics (count, effectiveness score), most-used learnings, learning source badges, injection history details. | BACKLOG-01, BACKLOG-02, BACKLOG-05 | Not Started | 0% | Priority: Low (polish feature). Effort: 8-10 pts. Deferred to Phase 7+. Provides exec reflection story: which learnings drove better outcomes? Integrates with breadcrumb traces + effectiveness scoring (v0.2 P6.3/P7.1) |

---

## Knowledge Management Enhancements (Phase 7+)

Advanced knowledge management features requiring mature search infrastructure and Phase 6 completion.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 210 | [CT-REQ-015](Reqs/CT-REQ-015.md) | [11. Configuration & User Transparency](Specs/11-Configuration-Transparency.md) | The system SHALL provide Knowledge Graph Visualization with mind map and interactive graph views for knowledge exploration. | CT-REQ-003, KM-REQ-012, KM-NFR-002 | Not Started | 0% | Complex Feature - Deferred: Requires mature Tier 2 search implementation. Static mind map MVP available if prioritized for Phase 6. Estimated effort: 13-21 story points. High business value for knowledge discovery UX (Phase 7+) |

---

## Future Considerations (v0.3+ / Long-term)

Requirements identified for future exploration after MVP and v0.2 stabilization.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 204 | [FUT-REQ-001](Reqs/FUT-REQ-001.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Add image understanding with local vision models. | None | Not Started | 0% | Deferred: Vision capability - Multimodal AI integration |
| 205 | [FUT-REQ-002](Reqs/FUT-REQ-002.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Add knowledge graph to supplement vector search. | KM-REQ-012 | Not Started | 0% | Deferred: Graph indexing - Semantic knowledge relationships |
| 206 | [FUT-REQ-003](Reqs/FUT-REQ-003.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Implement skill marketplace with versioning, review, trust model. | AST-REQ-007 | Not Started | 0% | Deferred: Marketplace - Community skill sharing |
| 207 | [FUT-REQ-004](Reqs/FUT-REQ-004.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Support multi-user and organizational knowledge hierarchies. | LM-REQ-003 | Not Started | 0% | Deferred: Multi-user - Enterprise deployment model |
| 208 | [FUT-REQ-005](Reqs/FUT-REQ-005.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Add HNSW approximate nearest neighbor indexing for large corpora. | KM-NFR-002 | Not Started | 0% | Deferred: HNSW scaling - Performance optimization for 10K+ documents |
| 209 | [FUT-REQ-006](Reqs/FUT-REQ-006.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Provide voice interface with local speech-to-text/text-to-speech. | KLC-REQ-011 | Not Started | 0% | Deferred: Voice interface - Speech interaction |
| 210 | [FUT-REQ-007](Reqs/FUT-REQ-007.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Future: Implement mobile sync for partial knowledge base access. | KM-REQ-007 | Not Started | 0% | Deferred: Mobile sync - Cross-device knowledge access |
| 216 | [FUT-ACC-001](Reqs/FUT-ACC-001.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Each deferred item has placeholder interface or extension point identified. | ARCH-NFR-002 | Not Started | 0% | Future extensibility design - Architecture preparedness |
| 216 | [FUT-NFR-001](Reqs/FUT-NFR-001.md) | [13. Open Items & Future Considerations](Specs/13-Open-Items-Future.md) | Deferred features SHOULD integrate without breaking existing interfaces. | ARCH-NFR-002 | Not Started | 0% | Future compatibility - Backward compatibility guarantee |

---

## Advanced Features (Distributed, Accessibility, Enhanced Interaction)

Long-term advanced features requiring substantial infrastructure investment.

| Seq | Requirement | Spec Document | Description | Predecessors | Status | Progress % | Notes |
|-----|-------------|---------------|-------------|--------------|--------|------------|-------|
| 211 | [BACKLOG-DDS-001](Reqs/BACKLOG-DISTRIBUTED-DELTA-SCHEMA-IMPLEMENTATION.md) | [Architecture Decisions](Architecture/decisions/DISTRIBUTED-STATE-ARCHITECTURE.md) | Future (v0.2+): Implement distributed delta schema, canonical document pointers, permission-gated replication, cache lifecycle, and repository support for multi-node synchronization. | KLC-REQ-004, KM-REQ-007, KM-REQ-008, ARCH-REQ-006 | Not Started | 0% | High Priority Backlog - Schema migrations (`documents` extensions, `change_log`, `applied_deltas`, `conflict_log`), C# models/repositories, delta sync foundation; enables cloud knowledge sharing; estimated 8-13 story points |
| 212 | BACKLOG-TTS-001 | [Ideas: § 17 Accessibility](../Ideas-Organized-By-Topic.md) | Future: Text-to-Speech reading of text, summaries, knowledge articles, and system responses on desktop (MAUI) and mobile applications using platform-native TTS APIs with controls for pause/resume, speed, voice selection, and reading queue management. | KLC-REQ-011 | Not Started | 0% | Accessibility Feature - Windows SAPI for desktop, platform-native mobile TTS; reading progress tracking, background reading, multiple voice profiles; estimated 5-8 story points |
| 213 | BACKLOG-VC-001 | [Ideas: § 17 Accessibility](../Ideas-Organized-By-Topic.md) | Future: Voice Commands & Control - Speech-to-Text input for natural language commands, voice-driven navigation, dictation mode, command confirmation, and offline basic voice recognition with cloud fallback for accessibility and hands-free operation. | KLC-REQ-011 | Not Started | 0% | Accessibility Feature - Wake word or push-to-talk modes, command disambiguation, full keyboard-free navigation for vision-impaired users; estimated 8-13 story points |
| 214 | BACKLOG-IVR-001 | [Ideas: § 17 Accessibility](../Ideas-Organized-By-Topic.md) | Future: Smart Notifications & Interactive Voice Response (IVR) - Context-aware notifications with voice prompts, meeting detection, touch-tone style voice menus for quick responses, priority-based interruption, and Do Not Disturb integration. | KLC-REQ-011, BACKLOG-TTS-001 | Not Started | 0% | Accessibility Feature - IVR menu options ("Press 1 for approve"), voice response templates, notification categories (task completion, daily briefings, calendar appointments, knowledge updates), emergency override; estimated 8-13 story points |
| 215 | BACKLOG-VKM-001 | [Ideas: § 5 Knowledge Management](../Ideas-Organized-By-Topic.md) | Future: Version-Aware Knowledge & Temporal Tracking - Track original published dates, technology/standard version tags, applies-to version ranges, content validity/expiration dates, per-source freshness policies, staleness detection, and version-specific retrieval for technology documentation and API references. | WFC-DATA-001, KM-REQ-001 | Not Started | 0% | Knowledge Freshness Feature - Schema: temporal metadata (published_date, valid_from/until, last_verified), version applicability (applies_to_versions, min/max_version, deprecated_in_version), freshness policies (refresh_policy, staleness_threshold, stale_impact_score); staleness dashboard, version mismatch warnings, CLI refresh commands; estimated 13-21 story points |

---

## Status Definitions

- **Not Started:** Requirement identified but not yet prioritized for implementation
- **Backlog:** Requirement explicitly deferred to future version (v0.2+)

---

**Last Updated:** 2026-03-08  
**Document Version:** 1.0  
**Master Implementation Tracker:** [Master-Implementation-Tracker.md](Master-Implementation-Tracker.md)
