# Daiv3

Intelligent Local AI Assistant

System Architecture & Design Document

Version 0.1 --- Draft

February 2026

# 1. Executive Summary

Daiv3 is a locally-first, privacy-respecting AI assistant designed to run efficiently on Copilot+ PCs equipped with NPUs, such as the Asus Vivobook S (Snapdragon X Elite) and Microsoft Surface devices (Intel NPU). The system is designed to operate fully offline under normal conditions, falling back to online AI providers only when explicitly needed or configured, with strict token budget controls. Daiv3 is built on .NET 10.

The core design principles are:

-   Local-first: All primary processing runs on-device using local SLMs via Microsoft Foundry Local and in-process ONNX Runtime models.

-   Self-contained: The .NET 10 application bundles its own vector search, embedding generation, and knowledge management without requiring external servers or Docker containers.

-   Transparent: The user can see exactly what the system is doing at all times --- indexing status, model usage, queue state, token budgets, and agent activity.

-   Extensible: Skills, agents, MCP tool servers, and external API integrations can be added and shared without rebuilding the core system.

-   Intelligent resource management: A smart model queue avoids unnecessary model thrashing in Foundry Local, batching tasks by model affinity while respecting priority.

-   Adaptive learning: Agents and skills accumulate structured learnings over time --- corrections, discovered patterns, and user feedback --- stored as inspectable, referenceable memory that improves future behavior without retraining the underlying model.

# 2. Target Hardware & Runtime Environment

## 2.1 Primary Hardware Targets

  ------------------------- --------------------------------------------------------------------------------------
  **Asus Vivobook S**       Snapdragon X Elite NPU --- 45 TOPS. ARM64 architecture. Windows 11 Copilot+.

  **Microsoft Surface**     Intel Core Ultra NPU (series 2) --- 40+ TOPS. x64 architecture. Windows 11 Copilot+.

  **General Copilot+ PC**   Any Windows 11 Copilot+ device with NPU. GPU and CPU fallback supported.
  ------------------------- --------------------------------------------------------------------------------------

## 2.2 Execution Provider Priority

All compute-intensive operations follow a hardware priority chain managed by ONNX Runtime DirectML:

-   NPU --- Preferred for embedding generation and vector batch operations. Low power, high throughput for matrix operations.

-   GPU --- Fallback for devices without NPU or for tasks exceeding NPU capacity.

-   CPU --- Final fallback. SIMD acceleration via .NET 10 TensorPrimitives ensures reasonable CPU performance.

ONNX Runtime with DirectML execution provider handles the hardware selection automatically. No code changes are needed when deploying across different hardware configurations.

# 3. System Architecture Overview

Daiv3 is structured as a layered .NET application. Each layer has clear responsibilities and communicates through well-defined interfaces.

+-------------------------------------------------------------------------------------------+
| **PRESENTATION LAYER**                                                                    |
|                                                                                           |
| UI (MAUI / CLI) --- Chat, Status Dashboard, Project Manager, Settings                     |
+-------------------------------------------------------------------------------------------+
| **ORCHESTRATION LAYER**                                                                   |
|                                                                                           |
| Task Orchestrator --- Intent Resolution --- Agent Manager --- Skill Registry              |
+-------------------------------------------------------------------------------------------+
| **MODEL EXECUTION LAYER**                                                                 |
|                                                                                           |
| Model Queue --- Foundry Local Bridge --- Online Provider Router --- ONNX Embedding Engine |
+-------------------------------------------------------------------------------------------+
| **KNOWLEDGE LAYER**                                                                       |
|                                                                                           |
| Two-Tier Index --- SQLite Vector Store --- Document Processor --- Knowledge Graph         |
+-------------------------------------------------------------------------------------------+
| **PERSISTENCE LAYER**                                                                     |
|                                                                                           |
| SQLite (vectors + metadata) --- File System (source documents) --- Project Store          |
+-------------------------------------------------------------------------------------------+

# 4. Knowledge Management & Indexing

## 4.1 Document Ingestion Pipeline

When a file is added, modified, or detected as new during a directory scan, it passes through the following pipeline:

  ---------- -------------------- ---------------------------------------------------------------------------------------------------------------------------------------------
  **Step**   **Stage**            **Description**

  1          Format Detection     Identify file type (PDF, DOCX, HTML, MD, TXT, image, etc.)

  2          Content Extraction   Extract plain text or markdown. HTML is converted to Markdown to strip styling. DOCX/PDF text extracted via available libraries.

  3          Chunking             Split into overlapping chunks of \~400 tokens with \~50 token overlap to preserve context at boundaries.

  4          Topic Summary        A local SLM (via Foundry) generates a 2-3 sentence summary of the full document for the Tier 1 topic index. This runs as a background task.

  5          Embedding            ONNX Runtime (in-process, NPU/GPU/CPU) generates embeddings for each chunk AND for the topic summary.

  6          Storage              All embeddings, text, metadata, and source pointers written to SQLite.

  7          Change Detection     File hash stored. On re-scan, hash compared --- unchanged files skipped, changed files re-indexed, deleted files removed.
  ---------- -------------------- ---------------------------------------------------------------------------------------------------------------------------------------------

## 4.2 Two-Tier Index Architecture

Search operates across two tiers to balance speed and accuracy while keeping the primary index small.

  ------------------------- -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Tier 1: Topic Index**   One vector per document. Stores the embedding of the AI-generated summary. Used for fast coarse search across all documents. Very small footprint --- hundreds to low thousands of vectors even for large knowledge bases. Searched first on every query.

  **Tier 2: Chunk Index**   Many vectors per document (one per chunk). Only searched for documents that scored highly in Tier 1. Full semantic detail. Supports fine-grained passage retrieval.
  ------------------------- -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

## 4.3 Embedding Model

Embeddings are generated entirely in-process using ONNX Runtime --- no external service required.

-   Model: nomic-embed-text or all-MiniLM-L6-v2 (quantized INT8 ONNX format, sourced from HuggingFace once at setup)

-   Dimensions: 768 for Tier 2 chunk index, 384 for Tier 1 topic index (smaller model for faster coarse search)

-   Tokenization: Microsoft.ML.Tokenizers NuGet package, in-process

-   Hardware: ONNX Runtime DirectML --- NPU preferred, GPU fallback, CPU with TensorPrimitives SIMD as final fallback

## 4.4 SQLite Vector Store Schema

Everything is stored in a single SQLite database file. No external vector DB server required.

  ------------------ -------------------------------------------------------------------------------------------- ------------------------------------------
  **Table**          **Key Columns**                                                                              **Purpose**

  topic_index        doc_id, summary_text, embedding_blob, source_path, file_hash, ingested_at, metadata_json     Tier 1 --- one row per document

  chunk_index        chunk_id, doc_id, chunk_text, embedding_blob, chunk_order, topic_tags                        Tier 2 --- many rows per document

  documents          doc_id, source_path, file_hash, format, size_bytes, last_modified, status                    Document registry and change tracking

  projects           project_id, name, description, root_paths, created_at, config_json                           Project definitions and scoped knowledge

  tasks              task_id, project_id, title, status, priority, scheduled_at, dependencies_json, result_json   Tasks, subtasks, scheduling

  sessions           session_id, started_at, summary, key_knowledge_json                                          Session memory for back-propagation

  model_queue        request_id, model_id, priority, status, payload_json, created_at, completed_at               Model request queue state
  ------------------ -------------------------------------------------------------------------------------------- ------------------------------------------

## 4.5 Vector Search Implementation

Cosine similarity search is implemented as a .NET in-process library --- no Chroma, Qdrant, or other vector DB server. On startup, all topic-index embeddings are loaded into a contiguous float\[\] in memory for fast batch matrix operations. Chunk embeddings for candidate documents are loaded on demand.

-   Batch similarity: query vector vs. all topic-index vectors computed as a tensor operation via ONNX Runtime (NPU/GPU) or TensorPrimitives (CPU)

-   Result: top-K document candidates from Tier 1, then top-K chunks from Tier 2 for those documents only

-   At 10,000 topic vectors (768-dim): \~30MB RAM, sub-10ms search on CPU, faster on NPU

-   Scale path: HNSW approximate nearest neighbor can be added later without changing the interface if corpus grows very large

# 5. Model Execution & Queue Management

## 5.1 Foundry Local Constraint

Microsoft Foundry Local can only run one model in memory at a time. Model switching has a meaningful time cost (loading weights from disk). The queue system is designed to minimize model thrashing by intelligently batching requests.

## 5.2 Request Priority Levels

  --------------------- --------------------------------------------------------------------------------------------------------------------------------
  **Priority (P0)**     Immediate user-facing interactions. Interrupts. Urgent responses. Processed before anything else regardless of model affinity.

  **Normal (P1)**       Active chat sessions, foreground application requests, user-initiated tasks.

  **Background (P2)**   Autonomous agents, document indexing, scheduled tasks, reasoning pipelines, search operations not blocking a user.
  --------------------- --------------------------------------------------------------------------------------------------------------------------------

## 5.3 Queue Scheduling Algorithm

The queue manager maintains awareness of the currently loaded model and applies the following logic when selecting the next request to execute:

-   If a P0 request exists: execute immediately regardless of model. Switch model if necessary.

-   If a P1 request exists for the current model: execute it.

-   If P2 requests exist for the current model: execute them to drain the same-model queue before switching.

-   If no requests exist for the current model: evaluate P1 queue. Pick the model with the most pending P1 work and switch to it.

-   Model switch: unload current model, load target model, then begin executing its queue.

-   All requests in the queue are independent. Task dependencies are resolved by the orchestrator layer before tasks reach the queue.

## 5.4 Intent Resolution

Before a user request is placed in the queue, it passes through an intent resolver that determines:

-   What type of task is this? (chat, search, summarize, code, math, agent action, scheduled job, etc.)

-   Which model is best suited? (local general SLM, local coding model, online specialist, etc.)

-   What priority should it have?

-   Are any skills or tools required?

-   Does this require online access? If yes, can it be queued for later or must it block?

Intent resolution itself runs on a small, fast local model to avoid adding latency. The result is a structured request object placed in the appropriate queue.

## 5.5 Online Provider Routing

When a task requires an online model, the system routes to configured providers (OpenAI, Azure OpenAI, Anthropic, etc.) based on:

-   Task type --- user-configured model-to-task mappings

-   Token budget --- per-provider input and output token budgets (daily/monthly/total)

-   Availability --- if offline, tasks are queued with a 'pending online' status

-   User confirmation --- configurable: always confirm, confirm above X tokens, or auto-run within budget

Only the minimal required context is sent to online models: the retrieved document chunks, the instruction, and the specific question. Raw documents are never sent in full unless the user explicitly overrides this.

## 5.6 Online Task Parallelism

Unlike Foundry Local, online providers support parallel requests. Tasks destined for different online providers can be executed concurrently. Tasks to the same provider are rate-limited per provider configuration.

# 6. Knowledge Back-Propagation

ARIA supports surfacing learned knowledge upward through a hierarchy of scopes, from the narrowest context to the widest:

  --------------------- ---------------------------------------------------------------------------
  **Level**             **Description**

  Context               Active conversation. Ephemeral unless promoted.

  Sub-task              Knowledge from a specific sub-task execution.

  Task                  Aggregated knowledge from a completed task.

  Sub-topic             Distilled knowledge under a personal topic area.

  Topic                 High-level personal knowledge domain.

  Project               Project-scoped knowledge base.

  Organization (opt.)   Shared organizational knowledge (future).

  Internet              Published article, blog post, or shared resource (user-initiated export).
  --------------------- ---------------------------------------------------------------------------

Back-propagation is triggered explicitly by the user or by an agent when a task is marked complete. The system generates a summary of new knowledge and asks the user which levels to promote it to. At the Internet level, the system can draft a blog post, article, or other shareable artifact for the user to review and publish.

# 7. Projects, Tasks & Scheduling

## 7.1 Projects

A project is a scoped unit of work with its own knowledge base, tasks, configuration, and associated files. Projects can span multiple watched directories and can reference shared knowledge from the global knowledge base.

-   Name, description, status, created/modified dates

-   Root paths for scoped document indexing

-   Associated tasks and sub-tasks with dependency graph

-   Project-level instructions (system prompts, constraints) stored as knowledge

-   Project-level model preferences and token budgets

## 7.2 Tasks & Sub-tasks

Tasks are structured units of work that can be executed manually or by agents. The orchestrator resolves dependencies before any task enters the model queue, ensuring the queue only ever receives independent work items.

  --------------------- -----------------------------------------------------------------------------------------------------------------------
  **Fields**            Title, description, status, priority, project, assigned agent/skill, dependencies, scheduled time, recurrence, result

  **Status flow**       Pending → Queued → In Progress → Complete / Failed / Blocked

  **Dependencies**      A task can depend on other tasks. Orchestrator blocks execution until dependencies are satisfied.

  **Recurrence**        Tasks can be scheduled to run once, on a cron schedule, or triggered by events (file change, time, agent signal).
  --------------------- -----------------------------------------------------------------------------------------------------------------------

## 7.3 Scheduler

A background scheduler service evaluates pending scheduled tasks at regular intervals and submits them to the appropriate queue. Users can view, pause, and modify scheduled jobs via the transparency dashboard.

# 8. Agents, Skills & Tools

## 8.1 Agent Model

Agents are autonomous processes that can execute multi-step tasks, use tools, call skills, and iterate until a task is complete or a stopping condition is met. Agents run primarily on local models (Background priority) and are allocated token budgets to prevent runaway cost.

-   Agents are defined declaratively (JSON/YAML): name, goal, available skills, tools, model preference, iteration limit, output format.

-   The system can define new agents dynamically as needed for new task types.

-   Agents can communicate with other agents via a message bus within the orchestration layer.

-   Agents support self-correction: output is evaluated against success criteria; if failed, the agent retries with context from the failure.

## 8.2 Skills

Skills are modular capabilities that can be attached to agents or invoked directly. Skills can be built-in, user-defined, or imported from a skill marketplace.

  ---------------------- --------------------------------------------------------------------------------
  **Skill Category**     **Examples**

  Reasoning & Analysis   Legal analysis, financial modeling, math, brainstorming, argument mapping

  Code                   Code generation, review, debugging, test writing, architecture review

  Document               Generate DOCX/PDF/MD reports, format conversion, summarization, translation

  Data & Visualization   Chart generation, graph analysis, data transformation, statistics

  Web & Research         URL fetch, web crawl, content extraction to MD, source summarization

  Project Management     Task breakdown, scheduling, dependency analysis, status reporting

  Communication          Draft email, Slack message, meeting summary, action item extraction

## 8.3 Tool Backend Strategy (Direct, CLI, MCP)

Daiv3 supports three tool backends, each optimized for different use cases:

### 8.3.1 Direct C# Services (Preferred for Local Operations)
Built-in services with zero context overhead and lowest latency. Examples: knowledge search, model queue, scheduling, document processing, embedding generation. These tools are invoked directly via C# interfaces without serialization or RPC overhead.

### 8.3.2 CLI Execution (Fallback for External Utilities)
Shell command invocation for offline external tools, local utilities, and system integration. Examples: file operations (if not using direct services), Windows native tools, local external binaries. CLI tools add minimal context overhead (only when requested) and avoid network latency.

### 8.3.3 MCP (Optional for Remote Services)
The Model Context Protocol provides standardized integration with persistent remote services and third-party APIs. Examples: GitHub, AWS, external SaaS platforms, remote REST integrations. MCP tools are discoverable and shareable, but incur context token overhead that must be managed given Daiv3's token budget constraints.

### 8.3.4 Tool Routing & Backend Selection

Agents intelligently route tool calls to the most efficient backend:

1. **Direct C# services** are always preferred when available (zero overhead)
2. **CLI tools** are preferred over MCP for equivalent local operations
3. **MCP tools** are used only for remote services where direct/CLI backends are unavailable

**Design rationale**: Daiv3 explicitly tracks token budgets (daily/monthly limits on online providers) and is designed as a local-first system. Having all tools serialize through MCP would add unavoidable context overhead on every task. Instead, the system uses MCP as a **delegation mechanism** for external integrations while keeping internal operations overhead-free.

## 8.4 MCP Tool Integration for Remote Services

Any MCP-compatible tool server can be registered and made available to agents and skills. This enables integration with external services and APIs without custom per-integration development. MCP tool registration is optional; the system functions fully without any MCP servers.

**When to use MCP**:
- External APIs and SaaS platforms (GitHub, AWS, REST services)
- Shared tool servers with discoverable schemas
- Scenarios where context token overhead is justified by functionality

**When NOT to use MCP**:
- Local operations (use direct C# or CLI instead)
- High-frequency calls or time-critical operations
- Knowledge search, document processing, or indexing (use direct C#)
- File operations where CLI or direct approach is available

## 8.5 External Application Integration

Agents can interact with external applications via REST APIs where available, or via UI automation (Windows accessibility APIs, UIAutomation) where APIs are not available. Automation scripts are defined as skills and can be shared.

# 9. Learning Memory

Daiv3 agents and skills accumulate structured learnings over time. When an agent discovers it was wrong, receives corrective feedback, encounters a compilation error and resolves it, or identifies a better approach to a problem, it stores that learning in a persistent memory store. These learnings are inspectable and editable by the user, and are automatically retrieved and injected into future agent and skill executions to prevent repeating the same mistakes.

## 9.1 What Triggers a Learning

A learning is created when any of the following occurs:

-   User feedback: the user explicitly corrects an agent output, marks a result as wrong, or provides a better answer.

-   Self-correction: an agent evaluates its own output against success criteria, determines it was incorrect, and arrives at a better solution through iteration.

-   Compilation or runtime error: a code-generating agent encounters an error, resolves it, and records what caused the failure and what fixed it.

-   Tool or API failure: an agent calls a tool incorrectly, receives an error, and learns the correct invocation pattern.

-   Contradicting knowledge: a new document or result contradicts a prior belief stored in the knowledge base, triggering a reconciliation learning.

-   Explicit agent learning call: an agent or skill can programmatically record a learning at any point when it determines something worth remembering.

## 9.2 Learning Structure

Each learning is a structured record with enough context to be retrieved semantically and applied correctly in future situations.

  ------------------------- -------------------------------------------------------------------------------------------------
  **Field**                 **Description**

  learning_id               Unique identifier.

  title                     Short human-readable summary of what was learned.

  description               Full explanation: what happened, what was wrong, what the correct approach is.

  trigger_type              Enum: UserFeedback, SelfCorrection, CompilationError, ToolFailure, KnowledgeConflict, Explicit.

  scope                     Where this applies: Global, Agent, Skill, Project, or Domain (e.g. coding, legal).

  source_agent              The agent or skill that generated this learning.

  source_task_id            The task or session in which the learning occurred (for traceability).

  embedding_blob            Vector embedding of the description for semantic retrieval.

  tags                      Comma-separated tags for filtering (e.g. \'csharp\', \'file-io\', \'prompt-format\').

  confidence                Float 0-1. High confidence = injected automatically. Low confidence = injected as suggestion.

  status                    Active, Suppressed (user ignored), Superseded (replaced by newer), Archived.

  times_applied             Retrieval count. Surfaces high-value learnings and identifies stale ones.

  created_at / updated_at   Creation and last modification timestamps.

  created_by                Agent ID or \'user\' if manually entered.
  ------------------------- -------------------------------------------------------------------------------------------------

## 9.3 How Learnings Are Retrieved & Applied

Before any agent or skill begins execution, the system performs a semantic search of the learning memory using the task description and context as the query. Relevant learnings are ranked by cosine similarity and filtered by scope (global learnings always included; agent/skill/domain learnings only if they match the current context). The top learnings are injected into the agent\'s system prompt as a \'Lessons Learned\' block, for example:

+---------------------------------------------------------------------------------------------------------------+
| **\## Lessons Learned**                                                                                       |
|                                                                                                               |
| Apply these learnings to the current task:                                                                    |
|                                                                                                               |
| 1\. \[C# File IO\] Always use FileStream with \'using\' blocks. Leaving streams open caused file lock errors. |
|                                                                                                               |
| 2\. \[Prompt Format\] This user prefers bullet-point summaries. Prior feedback: \'too wordy\'.                |
+---------------------------------------------------------------------------------------------------------------+

This improves agent behavior without any model fine-tuning. Learnings are knowledge injected at inference time via the same RAG mechanism used for document retrieval.

## 9.4 User Visibility & Control

All learnings are fully visible and editable by the user via the Transparency Dashboard. The user can:

-   Browse all learnings, filtered by agent, skill, project, domain, trigger type, or date.

-   View the full context: what task triggered it, before/after state, and how many times it has been applied.

-   Edit a learning\'s description or title to correct or refine it.

-   Suppress a learning so it is no longer injected (without deleting it).

-   Promote a learning\'s scope (e.g. from Agent-specific to Global) if it is broadly applicable.

-   Manually create a learning to encode knowledge the system has not discovered on its own.

-   Mark a learning as superseded when a better approach has been found, linking old and new entries.

## 9.5 Learning Back-Propagation

Learnings participate in the same back-propagation hierarchy as other knowledge (Section 6). A learning can be promoted from agent-scope to project-scope, domain-scope, or global. At global scope, learnings become part of the default instruction set for all agents. High-confidence, frequently-applied global learnings may optionally be exported as skill instructions or shared via the skill marketplace.

## 9.6 Storage

Learnings are stored in a dedicated learnings table in the main SQLite database. The embedding_blob column enables semantic retrieval using the same in-process cosine similarity pipeline used for document chunks. Learnings are searched as a distinct scope alongside document Tier 1 and Tier 2 searches, keeping them fast and isolated from general document retrieval unless explicitly cross-queried.

# 10. Web Fetch, Crawl & Content Ingestion

ARIA can fetch content from the web and process it into local knowledge. This capability is designed to build a local, offline-accessible copy of relevant external knowledge.

-   Fetch a single URL: retrieve content, convert HTML to Markdown (stripping all styling, navigation, ads), extract meaningful text.

-   Crawl mode: follow links up to a configurable depth within a domain. Respects robots.txt. Rate-limited to be polite.

-   Content is saved as Markdown files in a configurable local directory, making it part of the watched knowledge base.

-   AI summary is generated and added to the Tier 1 topic index. Chunks are added to Tier 2.

-   Source URL and fetch date are stored as metadata for freshness evaluation.

-   Refetch scheduling: pages can be scheduled to re-fetch at intervals to keep local copies fresh.

Format conversion (HTML to Markdown) is handled in-process using a .NET HTML parsing library (HtmlAgilityPack or AngleSharp) with custom conversion logic. This ensures the vector index captures semantic content rather than structural markup.

# 11. Configuration & User Transparency

## 11.0 User Interfaces

Daiv3 provides two primary user interfaces to accommodate different usage patterns and deployment scenarios:

### 11.0.1 MAUI Application (Daiv3.App.Maui)

A cross-platform .NET MAUI application provides a rich graphical interface for:

-   Interactive chat sessions with context-aware responses
-   Project management and configuration
-   Real-time transparency dashboard showing system status
-   Settings management with visual controls
-   File and directory selection for knowledge base indexing
-   Agent and skill configuration
-   Knowledge back-propagation review and approval

The MAUI app has full filesystem access, enabling it to browse, select, and monitor directories outside the application sandbox. This is essential for document indexing and project management across the user's files.

Target platforms: Windows (primary), with potential for macOS and Linux support.

### 11.0.2 CLI Application (Daiv3.App.Cli)

A command-line interface provides scriptable, headless operation for:

-   Automated knowledge ingestion and indexing
-   CI/CD pipeline integration
-   Server and headless deployments
-   Power user workflows and scripting
-   Remote administration
-   Non-interactive batch operations

The CLI supports both interactive mode (conversational prompts) and non-interactive mode (single command execution with flags).

Examples:
```bash
# Interactive chat session
daiv3 chat

# Non-interactive query
daiv3 query "What is the status of project Alpha?"

# Index a directory
daiv3 index add --path /documents/project-x --recursive

# Check system status
daiv3 status --json

# Export project knowledge
daiv3 export project --name "Project Alpha" --output ./export.md
```

Both interfaces share the same core orchestration layer and operate on the same SQLite database, ensuring consistency regardless of which interface is used.

## 11.1 Settings

All settings are stored locally and managed through a settings UI. Key configurable areas:

  ------------------------- -----------------------------------------------------------------------------------------------------------------------
  **Watched Directories**   Paths to index, include/exclude patterns, sub-directory depth, file type filters.

  **Models**                Foundry Local model preferences per task type, online provider API URLs and keys, model-to-task mappings.

  **Token Budgets**         Per-provider daily/monthly input token budget, output token budget, alert thresholds, hard stop or user-confirm mode.

  **Online Access**         When to allow online calls: never, ask each time, auto within budget, or per task type.

  **Agents & Skills**       Enable/disable individual skills and agents, set iteration limits, allocate local token budgets.

  **Scheduling**            View, add, edit, pause, or delete scheduled tasks and recurring jobs.

  **Knowledge Paths**       Map knowledge categories to file system directories for back-propagation targets.

  **Skill Marketplace**     Browse, import, and share skills. Configure local or remote skill repositories.
  ------------------------- -----------------------------------------------------------------------------------------------------------------------

## 11.2 Transparency Dashboard

A real-time status view is always accessible to the user, showing:

-   Model queue: current model loaded, pending requests by priority, estimated wait times.

-   Indexing status: files being processed, indexing progress, last scan time, any errors.

-   Agent activity: which agents are running, what they are doing, how many iterations, token usage so far.

-   Online usage: tokens used vs budget per provider this session, day, and month.

-   Scheduled jobs: next run times, last run results.

-   Knowledge back-propagation: pending promotions awaiting user review.

# 12. Key .NET Libraries & Components

  ---------------------- --------------------------------------------- ----------------------------------------------
  **Component**          **Library / Package**                         **Notes**

  Embedding generation   Microsoft.ML.OnnxRuntime.DirectML             In-process, NPU/GPU/CPU. No external server.

  Tokenization           Microsoft.ML.Tokenizers                       Microsoft official. BPE & WordPiece support.

  Vector math (CPU)      System.Numerics.TensorPrimitives (.NET 10)    SIMD cosine similarity. CPU fallback path.

  Persistence            Microsoft.Data.Sqlite                         Embedded SQLite. Single file, no server.

  Foundry Local bridge   Microsoft.Extensions.AI + Foundry Local SDK   Chat completion interface to local SLMs.

  Online AI providers    Microsoft.Extensions.AI abstractions          Unified interface; swap providers easily.

  HTML parsing           AngleSharp or HtmlAgilityPack                 HTML-to-Markdown conversion for web content.

  MCP tool support       ModelContextProtocol .NET SDK                 Connect to any MCP-compatible tool server.

  Document extraction    PdfPig (PDF), Open XML SDK (DOCX)             In-process text extraction.

  Scheduling             Quartz.NET or custom hosted service           Job scheduling with cron and event triggers.

  UI (MAUI)              .NET MAUI / Windows App SDK                   Cross-platform graphical UI for Windows.
  
  UI (CLI)               System.CommandLine                            Command-line interface for scripting.
  ---------------------- --------------------------------------------- ----------------------------------------------

# 13. Open Items & Future Considerations

  --------------------------- ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Image understanding**     Index images using a local vision model (e.g., Phi-3-vision or similar via ONNX). Generate text descriptions for the vector index. Currently deferred pending model availability in Foundry Local.

  **Knowledge graph**         Supplement vector search with an explicit knowledge graph (entity-relationship) for more structured reasoning. Consider LiteGraph or a simple custom implementation over SQLite.

  **Skill marketplace**       Design for sharing, versioning, and reviewing community skills. Trust model and sandboxing for imported skills to be defined.

  **Multi-user / org mode**   Shared knowledge bases, per-user permissions, organizational knowledge hierarchies. Deferred to a future version.

  **HNSW scaling**            If topic index exceeds \~100K vectors, approximate nearest neighbor (HNSW) indexing should be added. Interface design should not change.

  **Voice interface**         Local speech-to-text (Whisper via ONNX) and text-to-speech for hands-free interaction. NPU-friendly.

  **Mobile / sync**           Sync a subset of the knowledge base to a mobile device for offline access on the go.
  --------------------------- ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

# 14. Glossary

**Canonical Glossary:** See [Glossary.md](Glossary.md) for the complete, versioned glossary with definitions, related terms, and canonical usage rules.

The table below provides a quick reference to key terminology used throughout this document:

  ------------------ -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Term**           **Definition**

  Chunk              A segment of a document (typically 400 tokens) that forms the unit of embedding and retrieval.

  Embedding          A fixed-length float array encoding the semantic meaning of a piece of text, produced by an embedding model.

  Foundry Local      Microsoft\'s local SLM runtime. Runs one model at a time on the local device.

  MCP                Model Context Protocol. Standard interface for connecting AI models to external tools and data sources.

  NPU                Neural Processing Unit. Dedicated hardware for matrix math in AI workloads. Present in Copilot+ PCs.

  ONNX Runtime       Microsoft's cross-platform inference engine for running ML models locally.

  Learning Memory    A persistent store of structured learnings accumulated by agents and skills. Injected into future agent prompts via semantic retrieval to prevent repeating mistakes.

  RAG                Retrieval Augmented Generation. The pattern of retrieving relevant text from a knowledge base and injecting it into an LLM prompt.

  SLM                Small Language Model. A compact LLM suitable for running on consumer hardware, e.g. Phi-4.

  Tier 1 / Tier 2    The two levels of the vector index. Tier 1 is the fast coarse topic index; Tier 2 is the detailed chunk index.

  TensorPrimitives   .NET 10 API for SIMD-accelerated tensor math operations. Used for CPU-side vector similarity.
  ------------------ -----------------------------------------------------------------------------------------------------------------------------------------------------------------------
