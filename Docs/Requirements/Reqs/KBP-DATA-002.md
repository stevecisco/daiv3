# KBP-DATA-002

Source Spec: 6. Knowledge Back-Propagation - Requirements

## Requirement
Promotions SHALL store target scope and timestamps.

## Implementation Summary

**Status:** Complete - Implemented together with KBP-DATA-001

This requirement was fully implemented as part of the comprehensive promotion tracking system in KBP-DATA-001. The `promotions` table stores:

- **Target Scope:** `to_scope` column with CHECK constraint (`'Global'`, `'Agent'`, `'Skill'`, `'Project'`, `'Domain'`)
- **Source Scope:** `from_scope` column (for complete audit trail)
- **Timestamp:** `promoted_at` column (Unix timestamp)
- **Indexed Queries:** Efficient querying by target scope and time range

### Key Implementation Details

**Database Schema (Migration005):**
```sql
CREATE TABLE IF NOT EXISTS promotions (
    ...
    to_scope TEXT NOT NULL CHECK(to_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    promoted_at INTEGER NOT NULL,
    ...
);

CREATE INDEX idx_promotions_to_scope ON promotions(to_scope);
CREATE INDEX idx_promotions_promoted_at ON promotions(promoted_at DESC);
```

**Promotion Entity:**
```csharp
public class Promotion
{
    public string ToScope { get; set; } = string.Empty;     // Target scope (KBP-DATA-002)
    public long PromotedAt { get; set; }                    // Unix timestamp (KBP-DATA-002)
    ...
}
```

**Repository Methods:**
- `GetByToScopeAsync(string toScope)` - Query all promotions to a specific scope (e.g., all promotions to Global)
- `GetByTimeRangeAsync(long start, long end)` - Query promotions within a time range

### Testing
- **Unit Tests:** 15/15 passing in `PromotionRepositoryTests.cs`
  - Timestamp tracking and ordering
  - Target scope queries
  - Time range queries
- **Integration Tests:** 6/6 passing in `LearningManagementWorkflowTests.cs`
  - `GetByToScope_ReturnsPromotionsToTargetScope` - Verifies scope-based queries
  - Timestamp persistence across sessions

### Usage Example
```csharp
// Query all promotions to Global scope
var globalPromotions = await promotionRepo.GetByToScopeAsync("Global");

// Query promotions in last 24 hours
var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var recentPromotions = await promotionRepo.GetByTimeRangeAsync(yesterday, now);

// Examine promotion details
foreach (var promotion in globalPromotions)
{
    Console.WriteLine($"Learning {promotion.LearningId} promoted from {promotion.FromScope} to {promotion.ToScope}");
    var timestamp = DateTimeOffset.FromUnixTimeSeconds(promotion.PromotedAt);
    Console.WriteLine($"  Promoted at: {timestamp:yyyy-MM-dd HH:mm:ss UTC}");
}
```

## Dependencies
- ✅ KBP-DATA-001 - Implemented together

## Related Requirements
- **KBP-DATA-001** - Source task/session tracking (implemented together)
- **KBP-NFR-002** - Provenance storage (satisfied)
- **CT-REQ-009** - Future dashboard can use this data

## Status
**Complete (100%)**

See [KBP-DATA-001](KBP-DATA-001.md) for complete implementation details.

