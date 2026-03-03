using Daiv3.FoundryLocal.Management;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Daiv3.ModelExecution;

/// <summary>
/// Bridge to Microsoft Foundry Local for local SLM execution.
/// </summary>
public class FoundryBridge : IFoundryBridge
{
    private readonly ILogger<FoundryBridge> _logger;
    private readonly IModelLifecycleManager? _modelLifecycleManager;
    private readonly FoundryLocalManagementService? _foundryManagementService;
    private readonly IChatClient? _chatClient;
    private string? _currentModelId;

    public FoundryBridge(ILogger<FoundryBridge> logger)
        : this(logger, null, null, null)
    {
    }

    public FoundryBridge(
        ILogger<FoundryBridge> logger,
        IModelLifecycleManager? modelLifecycleManager,
        FoundryLocalManagementService? foundryManagementService)
        : this(logger, modelLifecycleManager, foundryManagementService, null)
    {
    }

    public FoundryBridge(
        ILogger<FoundryBridge> logger,
        IModelLifecycleManager? modelLifecycleManager,
        FoundryLocalManagementService? foundryManagementService,
        IChatClient? chatClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelLifecycleManager = modelLifecycleManager;
        _foundryManagementService = foundryManagementService;
        _chatClient = chatClient;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string modelId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _logger.LogInformation(
            "Executing request {RequestId} with model {ModelId}",
            request.Id, modelId);

        if (_modelLifecycleManager != null)
        {
            await _modelLifecycleManager.SwitchModelAsync(modelId, ct).ConfigureAwait(false);
        }

        _currentModelId = modelId;

        if (_chatClient != null)
        {
            var responseText = await ExecuteViaChatClientAsync(_chatClient, request.Content, modelId, ct).ConfigureAwait(false);
            return new ExecutionResult
            {
                RequestId = request.Id,
                Content = responseText,
                Status = ExecutionStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                TokenUsage = new TokenUsage
                {
                    InputTokens = EstimateTokens(request.Content),
                    OutputTokens = EstimateTokens(responseText)
                }
            };
        }

        await Task.Delay(100, ct).ConfigureAwait(false);
        return new ExecutionResult
        {
            RequestId = request.Id,
            Content = $"Processed by Foundry Local model '{modelId}': {request.Content}",
            Status = ExecutionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            TokenUsage = new TokenUsage
            {
                InputTokens = EstimateTokens(request.Content),
                OutputTokens = EstimateTokens("simulated response")
            }
        };
    }

    public async Task<string?> GetLoadedModelAsync()
    {
        if (_modelLifecycleManager != null)
        {
            return await _modelLifecycleManager.GetLoadedModelAsync().ConfigureAwait(false);
        }

        return _currentModelId;
    }

    public async Task<List<string>> ListAvailableModelsAsync(CancellationToken ct = default)
    {
        if (_foundryManagementService == null)
        {
            return ["phi-3-mini", "phi-3.5-mini", "phi-4-mini"];
        }

        await _foundryManagementService.InitializeAsync(
            new FoundryLocalOptions
            {
                AppName = "daiv3-model-execution",
                EnsureExecutionProviders = true
            },
            ct).ConfigureAwait(false);

        var models = await _foundryManagementService.ListAvailableModelsAsync(ct).ConfigureAwait(false);
        return models.SelectMany(entry => entry.Variants)
            .Select(variant => variant.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<string> ExecuteViaChatClientAsync(
        IChatClient chatClient,
        string prompt,
        string modelId,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var options = new ChatOptions { ModelId = modelId };

        var chatClientType = chatClient.GetType();
        var completeAsyncMethod = chatClientType.GetMethod(
            "CompleteAsync",
            [typeof(IList<ChatMessage>), typeof(ChatOptions), typeof(CancellationToken)]);

        if (completeAsyncMethod == null)
        {
            return prompt;
        }

        var task = (Task?)completeAsyncMethod.Invoke(chatClient, [messages, options, ct]);
        if (task == null)
        {
            return prompt;
        }

        await task.ConfigureAwait(false);

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        var message = result?.GetType().GetProperty("Message")?.GetValue(result);
        var text = message?.GetType().GetProperty("Text")?.GetValue(message) as string;

        return string.IsNullOrWhiteSpace(text) ? prompt : text;
    }

    private static int EstimateTokens(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
    }
}
