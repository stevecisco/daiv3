# BACKLOG: Distributed Delta Schema and Repository Implementation (v0.2+)

**Status:** Backlog (v0.2+)  
**Last Updated:** March 7, 2026  
**Priority:** High  
**Effort:** 8-13 story points

---

## Overview

This backlog item captures implementation work deferred from architecture design:

1. Add SQLite schema support for distributed document pointers, replication policy flags, delta tracking, and cache lifecycle metadata.
2. Implement C# model, repository, migration, and service wiring to operationalize those schema changes.

This requirement follows decisions documented in:
- `Docs/Requirements/Architecture/decisions/DISTRIBUTED-STATE-ARCHITECTURE.md`

---

## Scope

### In Scope

- SQL migrations for new columns and tables
- Domain/entity model updates
- Repository updates for read/write/query patterns
- Delta export/import repository support
- Cache metadata update paths
- Conflict and idempotency persistence
- Unit and integration tests

### Out of Scope

- Full UI implementation for cache/conflict dashboards
- Cloud coordinator service implementation
- Enterprise sharding and distributed query routing

---

## Requirement

### BACKLOG-DDS-001: Schema and Repository Foundation for Distributed Delta Sync

**References:** KLC-REQ-004, KM-REQ-007, KM-REQ-008, ARCH-REQ-006  
**Depends On:** Existing SQLite persistence baseline (Complete)  
**Priority:** High

#### Description

Implement the schema and repository contract needed for:

- Canonical document identity across nodes (`source_root_id`, `relative_path`, `cloud_object_key`)
- Permission-gated replication (`allow_cloud_replicate_*`, `allow_cross_node_fetch`)
- Local cache lifecycle (`last_accessed_utc`, `last_hydrated_utc`, `cache_state`, eviction metadata)
- Delta synchronization persistence (`change_log`, `applied_deltas`)
- Conflict auditing (`conflict_log`)

#### Acceptance Criteria

1. SQLite migration adds required `documents` columns:
   - `source_root_id`, `relative_path`, `cloud_object_key`, `local_cache_path`
   - `allow_cloud_replicate_document`, `allow_cloud_replicate_summary`, `allow_cloud_replicate_embeddings`, `allow_cross_node_fetch`
   - `last_accessed_utc`, `last_hydrated_utc`, `access_count_30d`, `cache_state`, `eviction_eligible`
2. SQLite migration creates tables:
   - `change_log`
   - `applied_deltas`
   - `conflict_log`
3. SQLite indexes added for query performance:
   - `documents(source_root_id, relative_path)`
   - `documents(cloud_object_key)`
   - `documents(last_accessed_utc)`
   - `change_log(node_id, sequence_number)`
4. `Document` entity/model updated with new pointer/policy/cache fields.
5. Repository methods added/updated for:
   - document upsert by canonical pointer
   - cache metadata updates (`last_accessed`, `cache_state`)
   - policy flag retrieval and update
6. New repositories/interfaces for:
   - `IChangeLogRepository`
   - `IAppliedDeltaRepository`
   - `IConflictLogRepository`
7. Delta idempotency enforced by repository logic + unique constraints.
8. Migration is backward-compatible with existing databases.
9. Unit tests pass for model/repository behavior.
10. Integration tests pass for migration + CRUD + idempotent delta apply markers.

---

## Suggested Implementation Tasks

1. Add migration script to persistence migrations pipeline.
2. Extend domain entities and persistence DTO mappings.
3. Implement repository interfaces and concrete SQLite repositories.
4. Add transaction boundaries for multi-table delta writes.
5. Add tests:
   - unit: repository methods, mapping, edge cases
   - integration: migration verification on existing DB + new DB
6. Add diagnostics via `ILogger<T>` in repository write paths.

---

## Data Contract Notes

### Canonical Document Identity

- Primary runtime identity remains `doc_id`.
- Canonical cross-node pointer is `(source_root_id, relative_path)`.
- `local_cache_path` is node-local and non-authoritative.

### Replication Policy Defaults

- `allow_cloud_replicate_document = 0`
- `allow_cloud_replicate_summary = 1`
- `allow_cloud_replicate_embeddings = 1`
- `allow_cross_node_fetch = 1`

### Cache State Enum (string)

Allowed values:
- `NotCached`
- `CachedHot`
- `CachedWarm`
- `Evicted`
- `Pinned`

---

## CLI Follow-Up (Future)

Recommended commands after repository foundation is complete:

- `daiv3 knowledge export-delta`
- `daiv3 knowledge apply-delta --path <delta.jsonl>`
- `daiv3 knowledge cache list`
- `daiv3 knowledge cache evict --doc-id <id>`
- `daiv3 knowledge conflicts list`

---

## Risks and Mitigations

1. **Risk:** Migration regressions on existing local DBs.
   - **Mitigation:** Integration tests against seeded pre-migration DB snapshots.
2. **Risk:** Cache-state drift due to missing update calls.
   - **Mitigation:** Centralize cache metadata writes in repository methods.
3. **Risk:** Duplicate delta apply.
   - **Mitigation:** `applied_deltas` unique constraint + repository guard checks.

---

## Definition of Done

- Migration applies cleanly on new and existing DBs.
- Repositories compile and pass project-scoped tests.
- No net-new build errors.
- Requirement doc updated with implementation notes and test evidence when executed.
