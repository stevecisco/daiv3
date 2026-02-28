# MQ-REQ-009

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL select a model based on task type and user preferences.

## Implementation Plan
### ✅ COMPLETE

**Component:** `Daiv3.ModelExecution`

**Interfaces:**
- `IModelSelector` - Model selection service interface

**Models:**
- `ModelSelectionPreferences` - User preferences for model selection

**Implementation:**
- `ModelSelector` - Task-type based model selection with fallback logic
- `ModelSelectorOptions` - Configuration with task-to-model mappings

**Features:**
- Task type to model mapping
- User preference override support
- Local vs online model preference
- Automatic fallback (preferred → task-mapped → default → any available)
- Model availability checking
- Token budget consideration via preferences

**Default Mappings:** All task types default to "phi-4" (Foundry Local default model)

**DI Registration:** `ModelExecutionServiceExtensions.AddModelExecutionServices()`

## Testing Plan
### ✅ COMPLETE - 33/33 Tests Passing

**Unit Tests:** `ModelSelectorTests.cs`
- Default task type mappings (6 test cases)
- User preference override
- Custom mappings
- Availability checking
- Fallback logic (multiple levels)
- Local vs online preference
- Error conditions (no models available)
- Case-insensitive model IDs

**Test Coverage:**
- All selection paths validated
- Configuration options tested
- Error handling verified
- Preference precedence confirmed

## Usage and Operational Notes

**Configuration (appsettings.json):**
```json
{
  "ModelSelector": {
    "TaskTypeModelMappings": {
      "Chat": "phi-4",
      "Code": "phi-4"
    },
    "DefaultFallbackModel": "phi-4",
    "AvailableLocalModels": ["phi-4", "llama-3"],
    "AvailableOnlineModels": ["openai:gpt-4", "azure:gpt-35-turbo"],
    "PreferLocalModels": true,
    "AllowOnlineFallback": true
  }
}
```

**Usage:**
```csharp
var selector = serviceProvider.GetRequiredService<IModelSelector>();
var preferences = new ModelSelectionPreferences 
{ 
    PreferredModelId = "custom-model",
    AllowOnlineFallback = false 
};
var modelId = selector.SelectModel(TaskType.Chat, preferences);
```

**Selection Priority:**
1. User-provided PreferredModelId (if available)
2. Task type mapping
3. Default fallback model
4. Any available local model (if PreferLocalModels)
5. Any available online model (if AllowOnlineFallback)
6. Exception if no models available

**Operational Notes:**
- Model IDs are case-insensitive
- Online model format: "provider:model-id"
- Both global and per-request preferences supported
- Respects token budgets via MaxOnlineTokens in preferences

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None
