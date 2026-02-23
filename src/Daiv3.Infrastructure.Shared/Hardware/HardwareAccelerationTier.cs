namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Represents the hardware acceleration tier for a given operation.
/// Orders from best to worst: NPU > GPU > CPU.
/// </summary>
public enum HardwareAccelerationTier
{
    /// <summary>
    /// Unknown or unavailable tier (fallback).
    /// </summary>
    None = 0,

    /// <summary>
    /// Neural Processing Unit (NPU) - primary acceleration target for Copilot+ devices.
    /// Primary hardware accelerator in Windows 11 Copilot+ PCs with Snapdragon X, Intel Core Ultra, etc.
    /// </summary>
    Npu = 1,

    /// <summary>
    /// Graphics Processing Unit (GPU) - secondary acceleration via DirectML on NVIDIA/AMD GPUs.
    /// Fallback when NPU is unavailable.
    /// </summary>
    Gpu = 2,

    /// <summary>
    /// Central Processing Unit (CPU) - tertiary fallback with SIMD optimizations via TensorPrimitives.
    /// Universal fallback available on all .NET 10 platforms.
    /// </summary>
    Cpu = 3,
}
