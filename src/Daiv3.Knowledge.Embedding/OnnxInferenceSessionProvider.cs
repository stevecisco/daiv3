using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace Daiv3.Knowledge.Embedding;

public sealed class OnnxInferenceSessionProvider : IOnnxInferenceSessionProvider
{
    private readonly ILogger<OnnxInferenceSessionProvider> _logger;
    private readonly EmbeddingOnnxOptions _options;
    private readonly IOnnxSessionOptionsFactory _sessionOptionsFactory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private InferenceSession? _session;
    private OnnxExecutionProvider? _selectedProvider;

    public OnnxExecutionProvider? SelectedProvider => _selectedProvider;

    public OnnxInferenceSessionProvider(
        ILogger<OnnxInferenceSessionProvider> logger,
        IOptions<EmbeddingOnnxOptions> options,
        IOnnxSessionOptionsFactory sessionOptionsFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sessionOptionsFactory = sessionOptionsFactory ?? throw new ArgumentNullException(nameof(sessionOptionsFactory));
    }

    public async Task<InferenceSession> GetSessionAsync(CancellationToken ct = default)
    {
        if (_session != null)
        {
            return _session;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_session != null)
            {
                return _session;
            }

            _options.Validate();
            var modelPath = _options.GetExpandedModelPath();

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("ONNX model file not found.", modelPath);
            }

            var sessionOptions = _sessionOptionsFactory.Create(out var provider);
            _selectedProvider = provider;

            _session = new InferenceSession(modelPath, sessionOptions);
            _logger.LogInformation("ONNX inference session created using {Provider}.", provider);

            return _session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ONNX inference session.");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _selectedProvider = null;
        _initLock?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
