# 4. Knowledge Management & Indexing - Requirements

## Overview
This document specifies requirements derived from Section 4 of the design document. It covers ingestion, indexing, embedding generation, and vector search.

## Goals
- Provide a local ingestion pipeline with automatic change detection.
- Use a two-tier index for efficient retrieval.
- Generate embeddings in-process.

## Functional Requirements
- KM-REQ-001: The system SHALL detect new or changed files in watched directories.
- KM-REQ-002: The system SHALL extract text from supported formats: PDF, DOCX, HTML, MD, TXT, and images (future).
- KM-REQ-003: The system SHALL convert HTML to Markdown for indexing.
- KM-REQ-004: The system SHALL chunk documents into ~400 token segments with ~50 token overlap.
- KM-REQ-005: The system SHALL generate a 2-3 sentence topic summary for each document using a local SLM.
- KM-REQ-006: The system SHALL generate embeddings for each chunk and for the topic summary.
- KM-REQ-007: The system SHALL store embeddings and metadata in SQLite.
- KM-REQ-008: The system SHALL store and compare file hashes to detect changes.
- KM-REQ-009: The system SHALL delete index entries when source files are removed.

## Two-Tier Index Requirements
- KM-REQ-010: The system SHALL maintain a Tier 1 topic index with one vector per document.
- KM-REQ-011: The system SHALL maintain a Tier 2 chunk index with multiple vectors per document.
- KM-REQ-012: The system SHALL query Tier 1 first, then query Tier 2 only for top candidates.

## Embedding Requirements
- KM-REQ-013: Embeddings SHALL be generated using ONNX Runtime in-process.
- KM-REQ-014: The system SHALL support nomic-embed-text or all-MiniLM-L6-v2 models.
- KM-EMB-MODEL-001: The system SHALL maintain a registry of supported embedding models with metadata required for selection, validation, and tokenizer alignment.
- KM-EMB-MODEL-002: The system SHALL discover embedding models in the local embeddings directory and optionally download approved models from Hugging Face.
- KM-EMB-MODEL-003: The system SHALL allow selection of the active embedding model for Tier 1 and Tier 2 embeddings and persist the selection in settings.
- KM-REQ-015: Tier 1 embeddings SHALL use a smaller dimension model (e.g., 384 dims) for speed.
- KM-REQ-016: Tier 2 embeddings SHALL use 768 dimensions for higher fidelity.

## Vector Search Requirements
- KM-REQ-017: The system SHALL compute cosine similarity in batch for Tier 1 queries.
- KM-REQ-018: Topic embeddings SHOULD be loaded into memory at startup.
- KM-REQ-019: Chunk embeddings SHOULD be loaded on demand.

## Non-Functional Requirements
- KM-NFR-001: Tier 1 search SHOULD complete in <10ms on CPU for ~10,000 vectors.
- KM-NFR-002: The system SHOULD be able to scale to HNSW indexing later.

## Data Requirements (SQLite)
- KM-DATA-001: The database SHALL include topic_index, chunk_index, documents, projects, tasks, sessions, and model_queue tables.

## Dependencies
- Microsoft.ML.OnnxRuntime.DirectML.
- Microsoft.ML.Tokenizers.
- SQLite via Microsoft.Data.Sqlite.

## Acceptance Criteria
- KM-ACC-001: Adding a document results in topic and chunk embeddings in SQLite.
- KM-ACC-002: Updating a document triggers re-indexing only for that document.
- KM-ACC-003: Deleting a document removes its topic and chunk entries.

## Out of Scope
- Knowledge graph implementation (deferred).

## Risks and Open Questions
- Confirm exact tokenization model and chunk size for v0.1.
- Define supported file types for v0.1 scope.
