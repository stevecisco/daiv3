using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Daiv3.Knowledge.Embedding;

public sealed class OnnxEmbeddingModelRunner : IOnnxEmbeddingModelRunner
{
    private readonly ILogger<OnnxEmbeddingModelRunner> _logger;
    private readonly EmbeddingOnnxOptions _options;
    private readonly IOnnxInferenceSessionProvider _sessionProvider;

    public OnnxEmbeddingModelRunner(
        ILogger<OnnxEmbeddingModelRunner> logger,
        IOptions<EmbeddingOnnxOptions> options,
        IOnnxInferenceSessionProvider sessionProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
    }

    public async Task<DenseTensor<float>> RunAsync(EmbeddingModelInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var session = await _sessionProvider.GetSessionAsync(ct).ConfigureAwait(false);
        var inputs = new List<NamedOnnxValue>();

        try
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(_options.InputIdsTensorName, input.InputIds));
            inputs.Add(NamedOnnxValue.CreateFromTensor(_options.AttentionMaskTensorName, input.AttentionMask));

            if (!string.IsNullOrWhiteSpace(_options.TokenTypeIdsTensorName) && input.TokenTypeIds != null)
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(_options.TokenTypeIdsTensorName, input.TokenTypeIds));
            }

            using var results = session.Run(inputs);
            var output = SelectOutputTensor(results);

            return CopyTensor(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ONNX embedding inference failed.");
            throw;
        }
    }

    private Tensor<float> SelectOutputTensor(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        if (!string.IsNullOrWhiteSpace(_options.OutputTensorName))
        {
            var named = results.FirstOrDefault(result =>
                string.Equals(result.Name, _options.OutputTensorName, StringComparison.OrdinalIgnoreCase));

            if (named != null)
            {
                return named.AsTensor<float>();
            }
        }

        var fallback = results.FirstOrDefault(result => result.Value is Tensor<float>);
        if (fallback == null)
        {
            throw new InvalidOperationException("ONNX model output did not contain a float tensor.");
        }

        _logger.LogWarning(
            "ONNX output tensor {OutputTensorName} was not found. Falling back to {FallbackName}.",
            _options.OutputTensorName,
            fallback.Name);

        return fallback.AsTensor<float>();
    }

    private static DenseTensor<float> CopyTensor(Tensor<float> tensor)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var data = tensor.ToArray();
        return new DenseTensor<float>(data, dimensions);
    }
}
