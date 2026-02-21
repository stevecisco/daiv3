# MM-ACC-006

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
Model variant selection correctly prioritizes NPU > GPU > CPU when device is not specified.

## Acceptance Scenario
- User requests "download phi-3" without specifying device type.
- The system queries available variants for phi-3: [CPU v1, GPU v1, GPU v2, NPU v2].
- Using priority NPU > GPU > CPU, the system selects: **NPU v2**.
- If the device doesn't have an NPU, the fallback is: **GPU v2** (highest version).
- If no GPU, then: **CPU v1**.
- The system displays the selected variant clearly.
- The user can override with "--device CPU" if desired.

## Verification Steps
1. Query catalog for a multi-variant model (e.g., Phi-3).
2. Test auto-selection on different device types:
   - NPU device: should select NPU variant.
   - GPU-only device: should select GPU variant.
   - CPU-only device: should select CPU variant.
3. Test override: "download phi-3 --device cpu" forces CPU variant.
4. Verify selected variant is shown to user before download.

## Testing Approach
- Unit test on selection algorithm.
- Integration test on actual devices with different hardware.
- Mock device detection for testing.

## Usage Notes
- This prioritization reflects hardware performance: NPU > GPU > CPU.
- Users appreciate sensible defaults without manual selection.
- Override capability is important for power users.
- Selection should be visible in confirmation message.

## Related Requirements
- MM-REQ-011 (auto-selection)
- MM-REQ-027 (explicit variant selection)
- MM-REQ-028 (default algorithm)
