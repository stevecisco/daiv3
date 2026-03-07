# Distributed State Architecture for DAIv3 Multi-Node Deployments

**Status:** Architecture Decision Document (ADD-007)  
**Date:** March 7, 2026  
**Scope:** Multi-node deployment, cloud knowledge sharing, state replication  
**Predecessors:** ARCH-REQ-001, KLC-REQ-004, KM-REQ-001-KM-REQ-019

---

## Problem Statement

DAIv3 uses SQLite for local persistence. That is excellent for single-node operation, but multi-node deployments need a safe sharing model for:

1. Knowledge and embedding replication across nodes.
2. New node bootstrap from cluster state.
3. Conflict handling when multiple nodes change data.
4. Cloud sharing controls (document vs summary vs embeddings).
5. On-demand document retrieval with local cache lifecycle.

---

## Decision Summary

1. **Primary sync model:** Delta-based replication (append-only change sets).
2. **Snapshot role:** Baseline bootstrap and recovery only (not day-to-day sync).
3. **Identity model:** Canonical document pointers (`source_root_id + relative_path + doc_id`), not node-local absolute paths.
4. **Security/privacy model:** Permission-gated replication per document and per artifact type.
5. **Retrieval model:** On-demand hydration from shared storage when raw content is missing locally.
6. **Storage model:** Local SQLite remains system of execution on each node; shared storage holds deltas, optional baselines, and allowed artifacts.

---

## Architecture Model

### Layer 1: Node-Local State (SQLite + local cache)

This data stays local and is not directly shared as a live DB:

- `documents`, `topic_index`, `chunk_index`, `sessions`, `tasks`, `token_usage`
- In-memory Tier 1 index cache
- Node-local operational logs
- Local document cache files

### Layer 2: Shared Storage Artifacts (Blob/File)

This data is exchanged between nodes:

- Delta files: `deltas/<node_id>/<node_id>-delta-<start>-<end>.jsonl`
- Optional baseline snapshots: `baselines/baseline-YYYY-MM-DD.db.zip`
- Summary artifacts (when allowed)
- Embedding exports (when allowed)
- Document binaries (when allowed)

### Layer 3: Optional Coordinator Services

Optional services for enterprise operations:

- Node registry and health
- Conflict telemetry aggregation
- Learning/event aggregation
- Monitoring dashboards

---

## Replication Strategy

### Pattern A: Delta-Based Synchronization (Primary)

Each node writes a local change log and periodically exports unexported changes.

High-level cycle:

1. Node exports local unexported changes as a delta file.
2. Node discovers deltas from other nodes that were not yet applied.
3. Node applies discovered deltas idempotently.
4. Node records applied delta ranges to prevent duplicate apply.
5. Node refreshes in-memory Tier 1 cache when relevant changes were applied.

Benefits:

- Prevents data loss from multi-writer overlap.
- Small network payloads (changes only).
- Provides auditability per node and sequence.

### Pattern B: Baseline Snapshot (Bootstrap/Recovery Only)

Snapshots are not used as the primary synchronization method.

Use cases:

- New node bootstrapping.
- Disaster recovery.
- Periodic re-baseline to reduce long delta replay windows.

Suggested cadence:

- Delta sync: every 5-15 minutes.
- Baseline refresh: weekly (or environment-dependent).

---

## Document Pointer and Path Model

### Canonical Identity

Do not rely on absolute local paths for identity.

Use:

- `doc_id` (stable GUID)
- `source_root_id` (logical root, for example `knowledge-shared`, `project-root`)
- `relative_path` (for example `Docs/Architecture/doc1.md`)
- `cloud_object_key` (for replicated artifacts)

Rules:

1. Identity is `(doc_id)` and `(source_root_id, relative_path)`.
2. Absolute paths are runtime-local cache details only.
3. Each node maps `source_root_id` to a local base path via configuration.

---

## Permission-Gated Replication

Replication is explicit and policy-driven per document.

Required flags:

- `allow_cloud_replicate_document`
- `allow_cloud_replicate_summary`
- `allow_cloud_replicate_embeddings`
- `allow_cross_node_fetch`

Behavior:

1. Summary upload only if `allow_cloud_replicate_summary = true`.
2. Raw document upload only if `allow_cloud_replicate_document = true`.
3. Embedding export only if `allow_cloud_replicate_embeddings = true`.
4. Remote node hydration of raw content only if `allow_cross_node_fetch = true`.

---

## On-Demand Hydration Flow

Scenario: Node B needs details from Doc1 but does not have Doc1 locally.

1. Node B resolves metadata by `doc_id` and canonical pointer.
2. If local file exists, use local file.
3. If missing and policy allows, fetch from `cloud_object_key`.
4. Store into local cache path derived from `source_root_id + relative_path`.
5. Update cache/access metadata.
6. Continue local enrichment/indexing and export allowed deltas.

Result:

- Nodes can collaborate on the same corpus without forcing permanent local copies.
- Raw file storage is demand-driven and cache-controlled.

---

## Cache Lifecycle and Eviction

Track local cache health and usage:

- `last_accessed_utc`
- `last_hydrated_utc`
- `access_count_30d`
- `cache_size_bytes`
- `cache_state` (`NotCached`, `CachedHot`, `CachedWarm`, `Evicted`, `Pinned`)
- `eviction_eligible`

Recommended policy:

1. LRU plus max cache size threshold.
2. Never evict `Pinned` items.
3. Eviction deletes only local cached binary, not metadata/summaries/embeddings.
4. User confirmation for sensitive eviction scenarios.
5. Evicted items remain rehydratable via canonical pointer if policy allows.

---

## Conflict Resolution

### Different Documents on Different Nodes

No conflict. Both deltas apply and state converges.

### Same Document Modified on Multiple Nodes

Conflict detection inputs:

- `doc_id`
- `content_hash`
- `updated_at_utc`

Default strategy:

- Last-write-wins by timestamp.
- Log every conflict decision for audit.

Alternative strategies (future):

- Manual resolution workflow.
- Dual-version retention.
- Content-aware merge.

---

## Bootstrap Flow for New Node

1. Node starts and loads schema.
2. Node downloads latest baseline snapshot if available.
3. Node applies all deltas newer than baseline in timestamp/sequence order.
4. Node initializes Tier 1 in-memory cache.
5. Node begins normal sync loop (export local deltas + import remote deltas).

If no baseline exists:

- Initialize empty DB.
- Join cluster and ingest deltas going forward.
- Optionally produce first baseline once enough state exists.

---

## SQLite Schema Extensions (Planned)

### `documents` table additions

- `source_root_id TEXT NOT NULL DEFAULT 'knowledge-shared'`
- `relative_path TEXT NOT NULL`
- `cloud_object_key TEXT NULL`
- `local_cache_path TEXT NULL`
- `allow_cloud_replicate_document INTEGER NOT NULL DEFAULT 0`
- `allow_cloud_replicate_summary INTEGER NOT NULL DEFAULT 1`
- `allow_cloud_replicate_embeddings INTEGER NOT NULL DEFAULT 1`
- `allow_cross_node_fetch INTEGER NOT NULL DEFAULT 1`
- `last_accessed_utc TEXT NULL`
- `last_hydrated_utc TEXT NULL`
- `access_count_30d INTEGER NOT NULL DEFAULT 0`
- `cache_state TEXT NOT NULL DEFAULT 'NotCached'`
- `eviction_eligible INTEGER NOT NULL DEFAULT 1`

### `change_log`

- Tracks exported and applied changes per node sequence.

### `applied_deltas`

- Idempotency table to prevent duplicate delta application.

### `conflict_log`

- Persistent audit of conflict decisions.

### Suggested indexes

- `(source_root_id, relative_path)`
- `(cloud_object_key)`
- `(last_accessed_utc)`
- `(node_id, sequence_number)` on `change_log`

---

## Operational Guidance

For current DAIv3 status:

- Keep single-node local mode as default.
- Implement change tracking early (low overhead, high future value).
- Enable cloud replication features behind config flags.
- Roll out multi-node sync when environment requires it.

---

## Final Position on the User Scenario

Yes, the intended model is:

1. Node A ingests Doc1 with canonical pointer metadata.
2. Node B ingests Doc2 with canonical pointer metadata.
3. Summaries/documents/embeddings replicate only when allowed by policy flags.
4. If Node B later needs Doc1 raw content, it fetches on demand from shared storage if allowed.
5. Node B caches locally, updates access metadata, and may later evict safely.
6. Evicted files are rehydratable on this or other nodes from canonical shared pointers.

This enables collaborative knowledge enhancement across nodes without requiring all raw files to be permanently present on every node.

---

## References

- `Docs/Requirements/Reqs/ARCH-REQ-001.md`
- `Docs/Requirements/Reqs/KLC-REQ-004.md`
- `Docs/Requirements/Reqs/KM-REQ-001.md` through `Docs/Requirements/Reqs/KM-REQ-019.md`
