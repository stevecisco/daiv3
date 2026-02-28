using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Hardware Acceleration Acceptance Tests for HW-ACC-001, HW-ACC-002, HW-ACC-003.
/// Tests that embedding generation uses appropriate hardware (NPU/GPU/CPU) based on availability.
/// </summary>
[Collection("Knowledge Database Collection")]
public class HardwareAccelerationAcceptanceTests : IAsyncLifetime
{
	private readonly KnowledgeDatabaseFixture _fixture;
	private IEmbeddingGenerator? _embeddingGenerator;
	private IOnnxInferenceSessionProvider? _sessionProvider;
	private IHardwareDetectionProvider? _hardwareDetection;
	private ILogger<HardwareAccelerationAcceptanceTests>? _logger;

	public HardwareAccelerationAcceptanceTests(KnowledgeDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	public async Task InitializeAsync()
	{
		_embeddingGenerator = _fixture.ServiceProvider.GetRequiredService<IEmbeddingGenerator>();
		_sessionProvider = _fixture.ServiceProvider.GetRequiredService<IOnnxInferenceSessionProvider>();
		_hardwareDetection = _fixture.ServiceProvider.GetRequiredService<IHardwareDetectionProvider>();
		
		var loggerFactory = _fixture.ServiceProvider.GetRequiredService<ILoggerFactory>();
		_logger = loggerFactory.CreateLogger<HardwareAccelerationAcceptanceTests>();

		await Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await Task.CompletedTask;
	}

	/// <summary>
	/// HW-ACC-001: On an NPU device, embedding generation uses the NPU by default.
	/// This test verifies:
	/// 1. Embedding generation completes successfully
	/// 2. When hardware supports NPU/GPU (DirectML), that provider is selected
	/// 3. Output is valid with expected dimensions
	/// 4. Performance is measured and logged
	/// </summary>
	[Fact]
	public async Task EmbeddingGeneration_OnDefaultConfig_UsesCorrectHardwareProvider()
	{
		// Arrange
		var text = "Hardware acceleration enables efficient embedding generation using NPU or GPU.";
		var stopwatch = Stopwatch.StartNew();

		// Detect available hardware
		var availableTiers = _hardwareDetection!.GetAvailableTiers();
		var primaryTier = availableTiers.FirstOrDefault();
		
		_logger?.LogInformation("Available hardware tiers: {Tiers}", string.Join(", ", availableTiers));
		_logger?.LogInformation("Primary hardware tier: {PrimaryTier}", primaryTier);

		// Act - Generate embedding (this will initialize the session with detected hardware)
		var embedding = await _embeddingGenerator!.GenerateEmbeddingAsync(text);
		stopwatch.Stop();

		// Get the selected provider after session initialization
		var selectedProvider = _sessionProvider!.SelectedProvider;

		// Assert - Basic structure
		Assert.NotNull(embedding);
		Assert.NotEmpty(embedding);
		Assert.True(
			embedding.Length == 384 || embedding.Length == 768,
			$"Expected embedding dimension of 384 or 768, got {embedding.Length}");

		// Assert - All values should be valid
		foreach (var value in embedding)
		{
			Assert.True(
				!float.IsNaN(value) && !float.IsInfinity(value),
				$"Embedding contains invalid value: {value}");
		}

		// Assert - Hardware provider selection
		Assert.NotNull(selectedProvider);
		
		// Log the results
		_logger?.LogInformation(
			"✓ HW-ACC-001: Embedding generated successfully\n" +
			"  Hardware Tier: {HardwareTier}\n" +
			"  ONNX Provider: {OnnxProvider}\n" +
			"  Dimensions: {Dimensions}\n" +
			"  Latency: {LatencyMs:F2} ms",
			primaryTier,
			selectedProvider,
			embedding.Length,
			stopwatch.Elapsed.TotalMilliseconds);

		// Verify provider matches hardware capability
		if (primaryTier == HardwareAccelerationTier.Npu || primaryTier == HardwareAccelerationTier.Gpu)
		{
			// NPU and GPU both use DirectML provider
			Assert.Equal(OnnxExecutionProvider.DirectML, selectedProvider);
			_logger?.LogInformation("✓ HW-ACC-001: DirectML provider selected for {Tier} hardware", primaryTier);
		}
		else if (primaryTier == HardwareAccelerationTier.Cpu)
		{
			// CPU uses CPU provider
			Assert.Equal(OnnxExecutionProvider.Cpu, selectedProvider);
			_logger?.LogInformation("✓ HW-ACC-001: CPU provider selected for CPU-only hardware");
		}
	}

	/// <summary>
	/// HW-ACC-002: On a GPU-only device, embedding generation completes without errors.
	/// This test simulates GPU-only environment by disabling NPU.
	/// </summary>
	[Fact]
	public async Task EmbeddingGeneration_WithNpuDisabled_UsesGpuFallback()
	{
		// Save original environment variable
		var originalDisableNpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_NPU");

		try
		{
			// Arrange - Simulate GPU-only device by disabling NPU
			Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", "1");
			
			// Create a new service provider with the updated environment
			var services = new ServiceCollection();
			services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
			
			// Configure embedding with a valid model path
			var modelPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
			services.AddEmbeddingServices(opts =>
			{
				opts.ModelPath = modelPath;
			});
			
			await using var serviceProvider = services.BuildServiceProvider();
			var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator>();
			var sessionProvider = serviceProvider.GetRequiredService<IOnnxInferenceSessionProvider>();
			var hardwareDetection = serviceProvider.GetRequiredService<IHardwareDetectionProvider>();

			var text = "GPU fallback ensures embedding generation continues without errors.";
			var stopwatch = Stopwatch.StartNew();

			// Detect available hardware with NPU disabled
			var availableTiers = hardwareDetection.GetAvailableTiers();
			_logger?.LogInformation("Available hardware with NPU disabled: {Tiers}", string.Join(", ", availableTiers));
			
			// Verify NPU is not available
			Assert.DoesNotContain(HardwareAccelerationTier.Npu, availableTiers);

			// Act - Generate embedding with GPU or CPU
			var embedding = await embeddingGenerator.GenerateEmbeddingAsync(text);
			stopwatch.Stop();

			var selectedProvider = sessionProvider.SelectedProvider;

			// Assert - Embedding is valid
			Assert.NotNull(embedding);
			Assert.NotEmpty(embedding);
			Assert.True(
				embedding.Length == 384 || embedding.Length == 768,
				$"Expected embedding dimension of 384 or 768, got {embedding.Length}");

			// Log the results
			_logger?.LogInformation(
				"✓ HW-ACC-002: GPU fallback successful\n" +
				"  Available Tiers: {Tiers}\n" +
				"  ONNX Provider: {OnnxProvider}\n" +
				"  Dimensions: {Dimensions}\n" +
				"  Latency: {LatencyMs:F2} ms",
				string.Join(", ", availableTiers),
				selectedProvider,
				embedding.Length,
				stopwatch.Elapsed.TotalMilliseconds);

			// Verify provider is DirectML (GPU) or CPU
			Assert.NotNull(selectedProvider);
			if (availableTiers.Contains(HardwareAccelerationTier.Gpu))
			{
				Assert.Equal(OnnxExecutionProvider.DirectML, selectedProvider);
				_logger?.LogInformation("✓ HW-ACC-002: DirectML provider used for GPU fallback");
			}
			else
			{
				Assert.Equal(OnnxExecutionProvider.Cpu, selectedProvider);
				_logger?.LogInformation("✓ HW-ACC-002: CPU provider used (no GPU available)");
			}
		}
		finally
		{
			// Restore original environment variable
			Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", originalDisableNpu);
		}
	}

	/// <summary>
	/// HW-ACC-003: On a CPU-only device, embedding generation completes within acceptable latency.
	/// This test forces CPU-only execution and measures latency.
	/// </summary>
	[Fact]
	public async Task EmbeddingGeneration_WithCpuOnly_CompletesWithAcceptableLatency()
	{
		// Save original environment variables
		var originalForceCpu = Environment.GetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY");

		try
		{
			// Arrange - Force CPU-only execution
			Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", "true");
			
			// Create a new service provider with the updated environment
			var services = new ServiceCollection();
			services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
			
			// Configure embedding with a valid model path
			var modelPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
			services.AddEmbeddingServices(opts =>
			{
				opts.ModelPath = modelPath;
			});
			
			await using var serviceProvider = services.BuildServiceProvider();
			var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator>();
			var sessionProvider = serviceProvider.GetRequiredService<IOnnxInferenceSessionProvider>();
			var hardwareDetection = serviceProvider.GetRequiredService<IHardwareDetectionProvider>();

			var text = "CPU execution provides acceptable performance for embedding generation through SIMD optimization.";
			var stopwatch = Stopwatch.StartNew();

			// Detect available hardware with CPU-only
			var availableTiers = hardwareDetection.GetAvailableTiers();
			_logger?.LogInformation("Available hardware with CPU-only: {Tiers}", string.Join(", ", availableTiers));
			
			// Verify only CPU is available
			Assert.Single(availableTiers);
			Assert.Equal(HardwareAccelerationTier.Cpu, availableTiers[0]);

			// Act - Generate embedding with CPU
			var embedding = await embeddingGenerator.GenerateEmbeddingAsync(text);
			stopwatch.Stop();

			var selectedProvider = sessionProvider.SelectedProvider;
			var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

			// Assert - Embedding is valid
			Assert.NotNull(embedding);
			Assert.NotEmpty(embedding);
			Assert.True(
				embedding.Length == 384 || embedding.Length == 768,
				$"Expected embedding dimension of 384 or 768, got {embedding.Length}");

			// Assert - CPU provider was used
			Assert.Equal(OnnxExecutionProvider.Cpu, selectedProvider);

			// Assert - Latency is acceptable
			// For small/medium models (384-768 dims), CPU should complete within 1000ms on modern hardware
			// This is a generous threshold to account for CI/CD environments
			const double maxAcceptableLatencyMs = 2000.0;
			Assert.True(
				latencyMs < maxAcceptableLatencyMs,
				$"CPU latency {latencyMs:F2}ms exceeds acceptable threshold {maxAcceptableLatencyMs}ms");

			// Log the results
			_logger?.LogInformation(
				"✓ HW-ACC-003: CPU execution completed successfully\n" +
				"  ONNX Provider: {OnnxProvider}\n" +
				"  Dimensions: {Dimensions}\n" +
				"  Latency: {LatencyMs:F2} ms\n" +
				"  Threshold: {ThresholdMs:F2} ms\n" +
				"  Status: {Status}",
				selectedProvider,
				embedding.Length,
				latencyMs,
				maxAcceptableLatencyMs,
				latencyMs < maxAcceptableLatencyMs ? "PASS" : "FAIL");
		}
		finally
		{
			// Restore original environment variable
			Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", originalForceCpu);
		}
	}

	/// <summary>
	/// Performance baseline test: Measures embedding generation latency across multiple runs.
	/// Provides baseline metrics for hardware acceleration acceptance.
	/// </summary>
	[Fact]
	public async Task EmbeddingGeneration_PerformanceBaseline_LogsDetailedMetrics()
	{
		// Arrange
		var testTexts = new[]
		{
			"Short text for baseline measurement.",
			"Medium length text with more tokens to measure average case performance for embedding generation.",
			"Longer text segment that contains multiple sentences and provides a more comprehensive test case for embedding generation performance measurement. This helps establish baseline metrics across different text lengths."
		};

		var results = new List<(int TextLength, int Tokens, double LatencyMs)>();

		// Act - Run multiple embedding generations
		foreach (var text in testTexts)
		{
			var stopwatch = Stopwatch.StartNew();
			var embedding = await _embeddingGenerator!.GenerateEmbeddingAsync(text);
			stopwatch.Stop();

			results.Add((text.Length, embedding.Length, stopwatch.Elapsed.TotalMilliseconds));
		}

		// Get hardware info
		var availableTiers = _hardwareDetection!.GetAvailableTiers();
		var selectedProvider = _sessionProvider!.SelectedProvider;

		// Assert - All embeddings generated successfully
		Assert.Equal(testTexts.Length, results.Count);

		// Calculate statistics
		var avgLatency = results.Average(r => r.LatencyMs);
		var minLatency = results.Min(r => r.LatencyMs);
		var maxLatency = results.Max(r => r.LatencyMs);

		// Log detailed performance metrics
		_logger?.LogInformation(
			"Performance Baseline:\n" +
			"  Hardware: {Hardware}\n" +
			"  Provider: {Provider}\n" +
			"  Runs: {Runs}\n" +
			"  Avg Latency: {AvgMs:F2} ms\n" +
			"  Min Latency: {MinMs:F2} ms\n" +
			"  Max Latency: {MaxMs:F2} ms",
			string.Join(", ", availableTiers),
			selectedProvider,
			results.Count,
			avgLatency,
			minLatency,
			maxLatency);

		foreach (var (textLen, tokens, latency) in results)
		{
			_logger?.LogInformation(
				"  Text: {TextLen} chars → Latency: {Latency:F2} ms",
				textLen,
				latency);
		}

		// Performance should be consistent (no single run should be 10x slower than average)
		Assert.All(results, r => 
			Assert.True(r.LatencyMs < avgLatency * 10, 
				$"Outlier detected: {r.LatencyMs:F2}ms is more than 10x average {avgLatency:F2}ms"));
	}
}
