# MQ-REQ-008

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL classify each request by task type (chat, search, summarize, code, etc.).

## Implementation Plan
### ✅ COMPLETE

**Component:** `Daiv3.ModelExecution`

**Interfaces:**
- `ITaskTypeClassifier` - Classification service interface

**Models:**
- `TaskType` enum - Known task types (Chat, Search, Summarize, Code, etc.)
- `ExecutionRequest` - Already has TaskType field

**Implementation:**
- `TaskTypeClassifier` - Pattern-based classification using keyword matching
- `TaskTypeClassifierOptions` - Configuration with custom patterns support

**Features:**
- Pattern-based classification (10 task types)
- Case-insensitive matching
- Custom pattern extensibility
- Explicit task type override support
- Confidence threshold configuration

**DI Registration:** `ModelExecutionServiceExtensions.AddModelExecutionServices()`

## Testing Plan
### ✅ COMPLETE - 27/27 Tests Passing

**Unit Tests:** `TaskTypeClassifierTests.cs`
- Pattern matching for all 10 task types (27 test cases)
- Explicit task type override
- Case-insensitive matching
- Custom patterns
- Edge cases (null, empty, invalid inputs)
- Multi-pattern scoring

**Test Coverage:**
- All major task types validated
- Configuration options tested
- Error handling verified
- Default behavior confirmed

## Usage and Operational Notes

**Configuration (appsettings.json):**
```json
{
  "TaskTypeClassifier": {
    "UseExplicitTaskType": true,
    "CaseInsensitiveMatching": true,
    "MinimumConfidence": 0.3,
    "CustomPatterns": {
      "Code": ["custom-keyword"]
    }
  }
}
```

**Usage:**
```csharp
var classifier = serviceProvider.GetRequiredService<ITaskTypeClassifier>();
var taskType = classifier.Classify(request); // or classifier.Classify(content)
```

**Classification Patterns:**
- **Chat**: "chat", "talk", "conversation", "discuss"
- **Search**: "search", "find", "look for", "locate"
- **Summarize**: "summarize", "summary", "brief", "overview"
- **Code**: "code", "function", "class", "implement"
- **QuestionAnswer**: "what", "why", "how", "explain"
- And 5 more task types

**Operational Notes:**
- Classification is synchronous and fast (pattern matching)
- No online dependencies
- Deterministic results
- Extensible via custom patterns

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None
