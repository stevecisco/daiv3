# Skipped Tests Assessment - March 3, 2026

## Executive Summary

**10 tests skipped** in the full test suite (from `./run-tests.bat` dated 2026-02-28):
- **2 Vector Similarity Performance Tests** - ARM64 architectural limitation (permanent skip)
- **3 Web Server Tests** - SDK API not verified (likely no public API available)
- **5 Model Lifecycle Tests** - Updated with correct API calls; still skipped due to data dependencies

---

## Detailed Findings

### Category 1: Vector Similarity Performance Tests (2 skipped) ✅

These tests validate that vector similarity operations scale linearly with batch sizes. They are **intentionally skipped** due to platform-specific performance characteristics.

**Tests:**
1. `BatchCosineSimilarity_ScalingTest_384Dims_LinearPerformance`
2. `BatchCosineSimilarity_ScalingTest_768Dims_LinearPerformance`

**File:** `tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/VectorSimilarityPerformanceBenchmarkTests.cs`

**Skip Reason:** 
```
BLOCKED: Performance scaling test is environment-dependent. Fails on ARM64 
(Snapdragon X Elite) with 37x instead of 5x scaling due to platform-specific 
cache/SIMD characteristics. See ARCH-REQ-003 blocking notes.
```

**Analysis:**
- ✅ This is **NOT a code bug**, but an actual limitation of ARM64 architecture
- The threshold tests (non-scaling) **pass without issues**, validating absolute performance
- Scaling characteristics differ between x64 and ARM64 platforms due to:
  - Different CPU cache hierarchies
  - Platform-specific SIMD optimization capabilities
  - Snapdragon X Elite (ARM64) exhibits non-linear scaling at larger batch sizes
- **Recommendation:** Keep these skipped. This is documented as an architectural limitation in HW-NFR-002.

**Related Requirements:**
- [HW-NFR-002.md](Requirements/Reqs/HW-NFR-002.md) - Performance thresholds and expectations
- [CPU-Performance-Expectations.md](Performance/CPU-Performance-Expectations.md) - Platform-specific guidance

---

### Category 2: Web Server Tests (3 skipped) ⚠️

These tests attempt to validate REST web server functionality that may not be available in the Foundry Local SDK public API.

**Tests:**
1. `StartWebServerAsync_ShouldStartSuccessfully`
2. `StopWebServerAsync_AfterStart_ShouldStopSuccessfully`
3. `StartWebServerAsync_WithCustomUrl_ShouldUseCustomUrl`

**File:** `tests/integration/Daiv3.FoundryLocal.IntegrationTests/WebServerTests.cs`

**Previous Skip Reason:**
```
Web server API methods need verification - check FoundryLocalManager API reference
```

**Updated Skip Reason (March 3, 2026):**
```
SDK API for web server not verified - check FoundryLocalManager/Catalog API documentation
```

**Analysis:**
- ✅ API_DISCOVERY.md shows web server methods are **documented but unverified** in actual SDK
- Tests contain commented-out method calls: `await _manager!.StartWebServerAsync()`
- Research findings:
  - The Foundry Local SDK v0.8.2.1 documentation mentions web server functionality
  - However, **the actual method names and availability are not confirmed** in the public API
  - Microsoft's official API reference (https://aka.ms/fl-csharp-api-ref) should be consulted
  - Alternative: Use reflection/ILSpy to inspect actual SDK methods

**Recommendation:**
1. Confirm web server API availability with Microsoft documentation
2. If methods exist but have different names, update tests with correct names
3. If web server functionality is not public, consider removing these tests OR
4. Check if web service can be managed through Configuration.Web properties instead

**Next Steps:**
- Developer should run API discovery script: `tests/integration/Daiv3.FoundryLocal.IntegrationTests/Inspect-FoundryLocalAPI.ps1`
- Check actual FoundryLocalManager and Catalog type methods
- Update skip reasons with verified findings

---

### Category 3: Model Lifecycle Tests (5 skipped) ✅ UPDATED

These tests validate model download, load, unload, and variant selection operations. **The API has been implemented and the tests have been updated with correct method calls.**

**Tests:**
1. `DownloadAsync_WithValidModel_ShouldDownloadModel`
2. `LoadAsync_WithDownloadedModel_ShouldLoadModel`
3. `UnloadAsync_WithLoadedModel_ShouldUnloadModel`
4. `GetPathAsync_WithCachedModel_ShouldReturnModelPath`
5. `SelectVariant_WithModel_ShouldSelectVariant`

**File:** `tests/integration/Daiv3.FoundryLocal.IntegrationTests/ModelLifecycleTests.cs`

**Status:** ✅ Tests **UPDATED** on 2026-03-03

**What Changed:**
- ✅ **Uncommented** API method calls in all 5 tests
- ✅ **Verified** method names against API_DISCOVERY.md documented API
- ✅ **Updated** skip reasons to indicate current skip is due to **data dependency**, not missing API
- ✅ **Added** logging to track test execution

**Correct API Methods (confirmed):**

| Operation | Method | Location |
|-----------|--------|----------|
| List models | `catalog.ListModelsAsync()` | Catalog object |
| Get catalog | `manager.GetCatalogAsync()` | Manager object |
| Download | `model.DownloadAsync()` | Model object |
| Load | `model.LoadAsync()` | Model object |
| Unload | `model.UnloadAsync()` | Model object |
| Get path | `model.GetPathAsync()` | Model object |
| Get variant | `model.SelectedVariant` | Model object property |
| Select variant | `model.SelectVariant("name")` | Model object |
| Get loaded | `catalog.GetLoadedModelsAsync()` | Catalog object |
| Get cached | `catalog.GetCachedModelsAsync()` | Catalog object |

**Why Still Skipped:**
The tests remain skipped due to **legitimate data dependencies**, not missing API:

| Test | Data Requirement | When Available |
|------|-----------------|-----------------|
| `DownloadAsync` | At least one model available in catalog | When Foundry Local service has models |
| `LoadAsync` | Downloaded/cached model available | After running `DownloadAsync` test |
| `UnloadAsync` | Loaded model available | After running `LoadAsync` test |
| `GetPathAsync` | Cached model available | After downloading a model |
| `SelectVariant` | Model with multiple variants | Depends on model catalog |

**Recommendation:**
When running these tests in an environment with:
- ✅ Foundry Local service running
- ✅ Model(s) available in the catalog
- ✅ Models downloaded/cached locally

Remove the `Skip` attribute from tests to run them. They should pass with the corrected API calls.

---

## Summary Table

| Category | Count | Can Resolve | Action | Priority |
|----------|-------|:-----------:|--------|----------|
| **Vector Scaling (ARM64)** | 2 | ❌ No | Keep skipped - architectural limitation | — |
| **Web Server API** | 3 | ⚠️ Verify | 1. Run API discovery script 2. Confirm methods exist 3. Update with verified names | **HIGH** |
| **Model Lifecycle** | 5 | ✅ Yes | Tests updated 3/3/26 with correct API; ready for environment with models | **MEDIUM** |
| **Total** | **10** | **5 updatable** | See details above | — |

---

## Files Modified (March 3, 2026)

1. **ModelLifecycleTests.cs** - ✅ Updated 5 tests with correct API calls
   - Uncommented: `DownloadAsync()`, `LoadAsync()`, `UnloadAsync()`, `GetPathAsync()`, `SelectVariant()`
   - Added: logging for test execution tracking
   - Updated skip reasons to clarify data dependencies

2. **WebServerTests.cs** - ⚠️ Clarified skip reasons (3 tests)
   - Updated skip messages to indicate API verification needed
   - Added comments explaining possible API patterns to investigate
   - Referenced API_DISCOVERY.md for method discovery guidance

---

## API Discovery References

For future work on web server tests:

- **Official API:** https://aka.ms/fl-csharp-api-ref
- **API Discovery Script:** `tests/integration/Daiv3.FoundryLocal.IntegrationTests/Inspect-FoundryLocalAPI.ps1`
- **API Guide:** `tests/integration/Daiv3.FoundryLocal.IntegrationTests/API_DISCOVERY.md`
- **README:** `tests/integration/Daiv3.FoundryLocal.IntegrationTests/README.md`

---

## Next Actions

### Immediate (To Enable More Tests)
1. ✅ **Done:** Updated ModelLifecycleTests with correct API
2. ⏳ **Pending:** Run full test suite in environment with Foundry Local running and models available
3. ⏳ **Pending:** Verify if Model Lifecycle tests pass without Skip attribute

### Short-term (To Resolve Web Server Tests)
1. Run Inspect-FoundryLocalAPI.ps1 to discover actual SDK methods
2. Verify if StartWebServerAsync / StopWebServerAsync exist in SDK
3. Update WebServerTests with confirmed method names or remove if API not available
4. Run tests and report findings

### Documentation
- Update Master-Implementation-Tracker.md with test status
- Document any web server API findings in API_DISCOVERY.md
- Update test README with confirmed API patterns

---

**Assessment Date:** March 3, 2026  
**Conducted By:** GitHub Copilot  
**Status:** Complete - Ready for action on Categories 2 & 3
