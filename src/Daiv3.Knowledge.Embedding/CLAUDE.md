# Daiv3.Knowledge.Embedding – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

ONNX-based text embedding generation with hardware acceleration. Supports CPU execution (cross-platform) and Windows-native DirectML (NPU/GPU) for maximum throughput on Copilot+ devices. Handles tokenisation, ONNX session lifetime, vector computation, and cosine similarity.

## Project Type

Library — **multi-TFM** (Windows and cross-platform builds)

## Target Framework

```xml
<TargetFramework>net10.0</TargetFramework>
<!-- Windows build automatically uses Windows-specific TFM -->
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

- `net10.0` → CPU execution via `Microsoft.ML.OnnxRuntime`
- `net10.0-windows10.0.26100` → DirectML (NPU/GPU) via `Microsoft.ML.OnnxRuntime.DirectML`

## Key Responsibilities

- `OnnxSessionOptionsFactory` — selects CPU or DirectML execution provider at runtime based on hardware capability
- `EmbeddingService` — tokenises text, runs ONNX inference, returns normalised float vectors
- `HardwareCapabilityDetector` — detects NPU/GPU/CPU tier for provider selection
- Vector similarity helpers (cosine similarity, batch operations)

## Platform / Hardware Notes

- Platform detection and provider selection happen **at library level** — executables do not need to be Windows-targeted to use NPU features
- Always provide CPU fallback for non-Windows or non-NPU scenarios
- Use `#if NET10_0_WINDOWS10_0_26100_OR_GREATER` guards for DirectML-specific code paths

## Test Projects

Integration tests require a real ONNX model file (download-on-first-run):

```powershell
dotnet test tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/Daiv3.Knowledge.Embedding.IntegrationTests.csproj --nologo --verbosity minimal
```

## Large Model File Policy

ONNX model files must **never** be committed to Git (>95 MB threshold). Use the download-on-first-run pattern with a manifest (model name, version, SHA256, URL). See AI-Instructions.md § 4.2.
