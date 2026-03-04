# CT-REQ-005

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display indexing progress, last scan time, errors, and provide a visual file/directory browser showing indexed content with per-file status indicators.

## Detailed Scope

### Indexing Progress Display
- **Overall Progress Bar:** Documents processed vs. total discovered
- **Scan Status:** Active/idle/paused/error (color-coded)
- **Last Scan Time:** When indexing last ran, how long it took
- **Error Count:** Failed documents with visual warning badge
- **Scan Schedule:** Next scheduled scan time (if automated)

### Indexed Content Browser
- **Directory Tree View:** File system hierarchy of watched directories
- **Expandable Folders:** Click to expand/collapse
- **Per-File Status Indicators:**
  - ✓ Green: Indexed successfully
  - ⟳ Blue: In progress (currently processing)
  - ! Orange: Warning (partial indexing, format not fully supported)
  - X Red: Error (indexing failed, see tooltip)
  - ○ Gray: Not yet indexed (in queue)
  - 🔒 Lock icon: Marked sensitive/not shareable

### File Details on Selection
- **File Properties:**
  - Name, path, size, type
  - Last indexed time, file modification time
  - Embedding status (Tier 1 topic + Tier 2 chunks)
  - Embedding dimension (384D or 768D)
  - Topic summary preview (first 100 chars)
  - Shareability flag
  - Machine location (if multi-node)

### Indexing Filter & Search
- **Filter by Status:** Show only indexed, only errors, in-progress, not-indexed
- **Filter by Type:** Show only PDFs, documents, code, etc.
- **Filter by Status:** Indexed, error, pending
- **Quick Search:** Find documents by name, path

### Indexing Statistics
- **Total Indexed:** Number of documents, KB/MB indexed
- **Top Formats:** Most common file types
- **Error Rate:** % of files with errors
- **Embedding Breakdown:** X% Tier 1, Y% Tier 2
- **Storage Usage:** KB used for embeddings

## Implementation Plan
- Query from KnowledgeDocumentProcessor service for file list and status
- Data contract: FileIndexStatus (path, status, timestamp, error, embedding dims)
- Directory tree binding via TreeView in MAUI
- Real-time status updates from IFileSystemWatcher
- CLI command: `daiv3 knowledge index status` with hierarchical output
- CLI command: `daiv3 knowledge index list --filter=errors` for detailed error view

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- File browser aligns with "File/Directory Explorer" brainstorming item
- Per-file status indicators provide clear visibility into indexing health
- Shareable metadata flag connects to Security & Privacy Management (Topic Area 3)
- Machine location reference supports distributed multi-node scenarios (Topic Area 1)

## Testing Plan
- Unit tests: FileIndexViewModel with mock file data
- Integration tests: Browser with real FileSystemWatcher and database
- UI tests: Expand/collapse, filtering, sorting
- Performance: Display 10K files with <2 sec load time
- Error simulation: Files in error state, missing files, permission errors

## Usage and Operational Notes
- Browser view refreshes every 2-5 seconds (configurable)
- Right-click on file: "View Details", "Re-index", "Delete Index", "Mark Sensitive"
- Double-click file: Show topic summary and top chunks
- Keyboard shortcut Ctrl+F: Quick search
- Indexing is continuous in background; browser shows live status
- Sensitive files show lock icon but not details without confirmation
- Multi-node machines show source machine location with indicator

## Dependencies
- KLC-REQ-011 (MAUI framework)
- KM-REQ-001 (file system watching)
- KM-REQ-006 (embedding generation)
- KM-REQ-007 (embedding storage)
- KM-REQ-008 (file hash tracking)
- KM-REQ-009 (deletion handling)
- CT-NFR-001 (real-time updates without blocking UI)

## Related Requirements
- CT-REQ-003 (dashboard foundation)
- KM-ACC-001 (adding documents)
- KM-ACC-002 (updating documents)
- KM-ACC-003 (deleting documents)
