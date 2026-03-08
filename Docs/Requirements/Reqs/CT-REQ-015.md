# CT-REQ-015

Source Spec: 11. Configuration & User Transparency - Requirements

## Status: BACKLOG (Phase 7+)

**Moved to backlog** as a deferred Phase 7+ requirement. While this feature provides high value for knowledge exploration UX, it requires mature Tier 2 vector search implementation and knowledge consolidation features that are foundational prerequisites.

## Requirement Summary
The system SHALL provide a Knowledge Graph Visualization displaying related documents, topic connections, and concept mapping through mind map and interactive graph views to enable knowledge exploration and discovery.

## Detailed Scope

### Mind Map View (Static MVP Option)

#### Core Concept
- **Central Node:** Current document or search query at center
- **Radial Layout:** Related documents positioned around center at varying distances
- **Distance = Similarity:** Closer nodes are more similar, farther nodes less related
- **Connection Lines:** Lines between related documents with similarity score label
- **Color Coding:** By document type (code, article, note, learning) or topic category

#### Mind Map Display
- **Node Information per Document:**
  - Title or heading
  - Type indicator (icon)
  - Similarity score (optional)
  - Preview text on hover
  - Click to view full document

- **Connections:**
  - Line thickness = similarity strength
  - Line color = relationship type (topical, citation, dependency)
  - Similarity score on line (e.g., "92% match")

- **Filtering & Zoom:**
  - Show top N related documents (e.g., top 10, top 20)
  - Similarity threshold slider (hide weak connections)
  - Click document to re-center on that document
  - Zoom in/out to explore (pan and pinch)

#### Interactive Features
- **Document Drill-Down:** Click node to view full document
- **Expand Connections:** Double-click to expand that document's connections
- **Search Within:** Find documents by text within visible graph
- **Export:** Save mind map image as PNG or SVG
- **Bookmark Path:** Bookmark interesting exploration paths (sequences of related docs)

### Knowledge Graph View (Phase 7+ Interactive)

#### Graph Components
- **Nodes:** Documents, topics, concepts, entities
- **Edges:** Relationships between nodes (similarity, citation, dependency, categorization)
- **Community Detection:** Visual clustering of related documents

#### Interactive Exploration
- **D3-Style Layout:** Force-directed graph with physics simulation
- **Node Selection:** Click to select, show details in side panel
- **Path Finding:** Click two nodes to highlight path between them
- **Level-of-Detail:** Progressive disclosure (expand on demand)
- **Search:** Find documents/topics within graph
- **Filter:** By document type, topic, date range, confidence

#### Graph Metrics (Phase 7+)
- **Centrality:** Most important documents (by citation/connection count)
- **Clustering Coefficient:** How tightly topics cluster
- **Average Path Length:** Knowledge commonality (short paths = well-connected)
- **Bridges:** Documents connecting disparate topics (interdisciplinary)

### Topic Clustering & Categories

#### Automatic Topic Detection (Phase 7+)
- **Cluster Documents:** Group by semantic similarity (LDA, clustering)
- **Category Labels:** Auto-generate category names from cluster keywords
- **Hierarchy:** Taxonomies with parent/child relationships
- **Custom Categorization:** Users can manually organize into custom categories

#### Tag Cloud & Category Browser
- **Tag Cloud:** Prominent topics displayed by frequency (font size)
- **Category List:** All topics with document count per category
- **Drill Into Category:** See all documents tagged with category
- **Hierarchy View:** Multi-level categorization tree

### Smart Recommendations

#### Related Document Suggestions (Phase 7+)
- **When Reading Document:** Suggest 5-10 related documents
- **Recommendation Basis:** Semantic similarity, same category, cross-citations
- **Relevance Score:** Show confidence score (80% match, etc.)
- **Why Recommended:** Explain reasoning (\"shares 5 topics with this document\")

#### Topic Exploration (Phase 7+)
- **Topic Suggestions:** \"You might be interested in these related topics\"
- **Learning Paths:** Curated sequences of documents to learn a topic
- **Prerequisite Documents:** \"Read these first to understand this document\"

### Responsive Design
- **Desktop:** Full mind map/graph view
- **Tablet:** Responsive graph with bottom detail panel
- **Mobile:** Simplified mind map with zooming, tap for details

## Implementation Plan (Phase 7+)

### Deferred Dependencies
- **Tier 2 Vector Search Enhancement:** More accurate similarity scoring
- **Knowledge Consolidation:** Document clustering and categorization
- **NLP Enhancement:** Entity extraction, topic modeling (LDA or equivalent)
- **Visualization Library:** D3.js integration with MAUI (Skia bridge or web view)

### Data Contracts (Planning Only)
```csharp
// Phase 7+ implementation
public record KnowledgeNode(
    string DocumentId,
    string Title,
    string Type, // Document type: article, code, note, learning
    double[] Embedding,
    List<string> Topics,
    DateTime CreatedDate
);

public record KnowledgeEdge(
    string SourceDocId,
    string TargetDocId,
    double SimilarityScore,
    string RelationshipType // Semantic, citation, dependency
);

public record KnowledgeGraph(
    List<KnowledgeNode> Nodes,
    List<KnowledgeEdge> Edges,
    List<TopicCluster> Clusters
);
```

### MAUI Implementation (Phase 7+)
- **KnowledgeGraphPage:** New page in App Shell (deferred)
- **GraphVisualization:** Custom control using Skia canvas or embedded web view
- **MindMapView:** Hierarchical mind map rendering
- **GraphViewModel:** Query knowledge layer, compute graph layout

### CLI Implementation (Planning)
- `daiv3 knowledge graph --document <doc-id>` - Show related documents
- `daiv3 knowledge graph --explore <topic>` - Explore topic relationships
- `daiv3 knowledge topics` - List all topics and document counts

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "Mind Map Visualization" and "Knowledge Relationship View" brainstorming concepts
- Bridges gap between raw search results and human understanding (Topic Area 5)
- Supports knowledge exploration and discovery (Topic Area 5)
- Aligns with "Master Organizer\" skill concept (Topic Area 4)
- Prerequisite: Strong Tier 2 semantic search implementation (KM-REQ-012)

## Testing Plan (Phase 7+)
- Unit tests: Graph computation and layout algorithms
- Integration tests: Real knowledge graph generation from documents
- UI tests: Graph interaction (zoom, pan, click, filter)
- Performance: Render 1000+ connected documents with <3 sec load
- Accuracy: Verify similarity scores, clustering correctness
- Accessibility: Keyboard navigation, high contrast for color-blind users

## Phase 6 Status
- **Status:** NOT STARTED (Deferred to Phase 7+)
- **Reason:** Requires mature Tier 2 vector search and knowledge consolidation first
- **MVP Alternative:** Static mind map using Tier 1 search results (Phase 6 possible with simpler UX)

## Phase 6 MVP Alternative (Optional)
If approved, a simplified static mind map could be delivered in Phase 6:
- **Limited Scope:** Show top 10 related documents (from Tier 1 similarity)
- **Simple Layout:** Fixed radial layout (no physics simulation)
- **Basic Interactivity:** Click to view, hover for preview
- **No Clustering:** Simple list of related documents with scores
- **Deferred to Phase 7+:** Graph view, community detection, advanced exploration

## Phase 7+ Full Implementation
- ✅ Interactive force-directed graph
- ✅ Topic clustering and categorization
- ✅ Entity extraction and relationship detection
- ✅ Learning paths and recommendations
- ✅ Calendar integration (deadlines visible on graph)
- ✅ Export and sharing
- ✅ Bookmark interesting knowledge paths

## Dependencies
- KLC-REQ-011 (MAUI framework)
- KM-REQ-012 (Tier 2 semantic search; foundation for similarity)
- KM-NFR-002 (HNSW scaling for large graphs)
- Visualization library (D3.js or equivalent, TBD Phase 7)

## Related Requirements
- CT-REQ-005 (indexed content explorer; knowledge graph complements)
- CT-REQ-003 (dashboard foundation)
- KM-REQ-012 (semantic search; provides similarity scores)
- ARCH-NFR-002 (knowledge graph extensibility; already planned)
- FUT-REQ-002 (knowledge graph in open items; aligns with this requirement)

## Deferred Considerations for Future Phases
- **Machine Learning Integration:** Auto-detecting document topics (LDA, BERTopic)
- **Temporal Aspects:** How topics evolve over time
- **Expert Annotation:** Allowing users to manually curate relationships
- **Cross-Silo Learning:** Combining graphs from multiple agents/projects
- **Recommendation Quality:** Improving suggestions through feedback loops
- **Privacy-Preserving Graph:** Sharing knowledge graphs while protecting sensitive documents
