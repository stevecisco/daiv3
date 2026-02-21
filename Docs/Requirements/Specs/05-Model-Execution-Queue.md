# 5. Model Execution & Queue Management - Requirements

## Overview
This document specifies requirements derived from Section 5 of the design document. It covers model constraints, queue priorities, scheduling, intent resolution, and online routing.

## Goals
- Minimize model thrashing under Foundry Local constraints.
- Prioritize user-facing tasks.
- Provide deterministic queue behavior.

## Functional Requirements
- MQ-REQ-001: The system SHALL enforce the constraint that only one Foundry Local model is loaded at a time.
- MQ-REQ-002: The system SHALL provide three priority levels: P0 (Immediate), P1 (Normal), P2 (Background).
- MQ-REQ-003: The queue manager SHALL execute P0 requests immediately, even if a model switch is required.
- MQ-REQ-004: If P1 requests exist for the current model, the queue SHALL execute them before switching.
- MQ-REQ-005: If P2 requests exist for the current model, the queue SHALL drain them before switching.
- MQ-REQ-006: If no requests exist for the current model, the queue SHALL select the model with the most pending P1 work.
- MQ-REQ-007: The model switch SHALL unload the current model and load the target model before execution.

## Intent Resolution Requirements
- MQ-REQ-008: The system SHALL classify each request by task type (chat, search, summarize, code, etc.).
- MQ-REQ-009: The system SHALL select a model based on task type and user preferences.
- MQ-REQ-010: The system SHALL assign a queue priority based on task type and context.
- MQ-REQ-011: The intent resolver SHALL run on a small local model to minimize latency.

## Online Provider Routing Requirements
- MQ-REQ-012: The system SHALL route online tasks based on model-to-task mappings, token budgets, and availability.
- MQ-REQ-013: The system SHALL queue online tasks when offline and mark them as pending.
- MQ-REQ-014: The system SHALL require user confirmation based on configurable rules (always, above X tokens, or auto within budget).
- MQ-REQ-015: The system SHALL send only the minimal required context to online providers.

## Online Parallelism Requirements
- MQ-REQ-016: The system SHALL execute online tasks concurrently across different providers.
- MQ-REQ-017: The system SHALL rate-limit requests per provider.

## Non-Functional Requirements
- MQ-NFR-001: Queue operations SHOULD be deterministic and observable.
- MQ-NFR-002: Model switching SHOULD be minimized under steady workloads.

## Data Requirements
- MQ-DATA-001: The system SHALL persist queue state in a model_queue table.

## Dependencies
- Foundry Local SDK.
- Online provider SDKs (OpenAI, Azure OpenAI, Anthropic).

## Acceptance Criteria
- MQ-ACC-001: P0 requests preempt P1 and P2.
- MQ-ACC-002: Requests for the current model are batched before switching.
- MQ-ACC-003: Online tasks respect token budget rules.

## Out of Scope
- Multi-model concurrent loading for local inference.

## Risks and Open Questions
- Confirm the specific model set and mapping rules for v0.1.
- Define queue observability metrics for the dashboard.
