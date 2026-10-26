using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TalentAssistant.Application.Assistant;

namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>One configured provider and its routing weight (SPEC-022 §3.1).</summary>
public sealed record AssistantProviderSlot(
    string Name,
    int Weight,
    IAssistantLanguageModel Model,
    string ModelId = "unknown");

/// <summary>
/// Weighted router over the configured providers (SPEC-022 §3). The provider is
/// chosen once per conversation from a stable hash of the optional conversation
/// id, kept for the rest of the turn, and replaced by the other provider when a
/// completion fails. State is scoped to one API request; nothing is persisted.
/// </summary>
public sealed class RoutingLanguageModel : IAssistantLanguageModel
{
    private readonly IReadOnlyList<AssistantProviderSlot> _providers;
    private readonly AssistantProviderHealth _health;
    private readonly ILogger<RoutingLanguageModel> _logger;
    private AssistantProviderSlot? _active;
    private static long _roundRobin;

    public RoutingLanguageModel(
        IReadOnlyList<AssistantProviderSlot> providers,
        AssistantProviderHealth health,
        ILogger<RoutingLanguageModel> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(logger);

        _providers = providers;
        _health = health;
        _logger = logger;
    }

    public async Task<AssistantCompletion> CompleteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_providers.Count == 0)
        {
            throw new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.NotConfigured,
                "No assistant provider is configured.");
        }

        if (_providers.Count == 1)
        {
            var only = _providers[0];
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var completion = await only.Model.CompleteAsync(request, cancellationToken);
                _health.RecordSuccess(only.Name);
                LogSuccess(only, stopwatch);
                return completion;
            }
            catch (AssistantProviderUnavailableException exception)
            {
                _health.RecordFailure(only.Name);
                _logger.LogWarning(
                    "Assistant provider {Provider} ({Model}) failed with {Reason} after {ElapsedMs} ms.",
                    only.Name,
                    only.ModelId,
                    exception.Reason,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        var first = ResolveFirstProvider(request.ConversationId);
        var firstStopwatch = Stopwatch.StartNew();
        try
        {
            var completion = await first.Model.CompleteAsync(request, cancellationToken);
            _health.RecordSuccess(first.Name);
            _active = first;
            LogSuccess(first, firstStopwatch);
            return completion;
        }
        catch (AssistantProviderUnavailableException firstFailure)
        {
            _health.RecordFailure(first.Name);
            _logger.LogWarning(
                "Assistant provider {Provider} ({Model}) failed with {Reason} after {ElapsedMs} ms; trying failover.",
                first.Name,
                first.ModelId,
                firstFailure.Reason,
                firstStopwatch.ElapsedMilliseconds);

            var second = ResolveFailoverProvider(first);
            if (second is null)
            {
                throw;
            }

            var secondStopwatch = Stopwatch.StartNew();
            try
            {
                var completion = await second.Model.CompleteAsync(request, cancellationToken);
                _health.RecordSuccess(second.Name);
                _active = second;
                LogSuccess(second, secondStopwatch);
                _logger.LogInformation(
                    "Assistant failover from {FirstProvider} to {SecondProvider} succeeded.",
                    first.Name,
                    second.Name);
                return completion;
            }
            catch (AssistantProviderUnavailableException secondFailure)
            {
                _health.RecordFailure(second.Name);
                _logger.LogWarning(
                    "Assistant provider {Provider} ({Model}) failed with {Reason} after {ElapsedMs} ms after failover.",
                    second.Name,
                    second.ModelId,
                    secondFailure.Reason,
                    secondStopwatch.ElapsedMilliseconds);

                var finalFailure = secondFailure;
                if (secondFailure.Reason == AssistantProviderFailureReason.InvalidResponse
                    && HasToolTranscript(request.Messages))
                {
                    // The fallback rejected the first provider's tool_calls
                    // transcript. Retry once with a text-only transcript; the
                    // tools are read-only, so re-asking is safe.
                    var sanitizedRequest = request with
                    {
                        Messages = SanitizeTranscript(request.Messages)
                    };
                    _logger.LogWarning(
                        "Retrying {Provider} with a sanitized transcript after a rejected foreign tool transcript.",
                        second.Name);

                    var sanitizedStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var completion = await second.Model.CompleteAsync(
                            sanitizedRequest,
                            cancellationToken);
                        _health.RecordSuccess(second.Name);
                        _active = second;
                        LogSuccess(second, sanitizedStopwatch);
                        _logger.LogInformation(
                            "Assistant failover from {FirstProvider} to {SecondProvider} succeeded with a sanitized transcript.",
                            first.Name,
                            second.Name);
                        return completion;
                    }
                    catch (AssistantProviderUnavailableException sanitizedFailure)
                    {
                        _health.RecordFailure(second.Name);
                        finalFailure = sanitizedFailure;
                        _logger.LogWarning(
                            "Assistant provider {Provider} ({Model}) failed with {Reason} after {ElapsedMs} ms with a sanitized transcript.",
                            second.Name,
                            second.ModelId,
                            sanitizedFailure.Reason,
                            sanitizedStopwatch.ElapsedMilliseconds);
                    }
                }

                // Both were rate limited -> the user-facing "service busy"
                // message; otherwise report the last observed reason.
                var reason = firstFailure.Reason == AssistantProviderFailureReason.RateLimited
                    && finalFailure.Reason == AssistantProviderFailureReason.RateLimited
                        ? AssistantProviderFailureReason.RateLimited
                        : finalFailure.Reason;
                throw new AssistantProviderUnavailableException(
                    reason,
                    "Both assistant providers failed.",
                    finalFailure);
            }
        }
    }

    private void LogSuccess(AssistantProviderSlot slot, Stopwatch stopwatch) =>
        _logger.LogInformation(
            "Assistant provider {Provider} ({Model}) completed in {ElapsedMs} ms.",
            slot.Name,
            slot.ModelId,
            stopwatch.ElapsedMilliseconds);

    private static bool HasToolTranscript(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(message =>
            message.ToolCalls is { Count: > 0 } || message.ToolCallId is not null);

    private static IReadOnlyList<ChatMessage> SanitizeTranscript(
        IReadOnlyList<ChatMessage> messages) =>
        messages
            .Where(message =>
                message.ToolCalls is not { Count: > 0 }
                && message.ToolCallId is null
                && !(message.Role == "assistant" && string.IsNullOrWhiteSpace(message.Content)))
            .ToArray();

    private AssistantProviderSlot ResolveFirstProvider(string? conversationId)
    {
        var preferred = _active ?? SelectPreferred(conversationId);
        if (_health.IsAvailable(preferred.Name))
        {
            return preferred;
        }

        var available = _providers.FirstOrDefault(provider =>
            !string.Equals(provider.Name, preferred.Name, StringComparison.Ordinal)
            && _health.IsAvailable(provider.Name));

        return available ?? throw new AssistantProviderUnavailableException(
            AssistantProviderFailureReason.RateLimited,
            "Every assistant provider is cooling down.");
    }

    private AssistantProviderSlot? ResolveFailoverProvider(AssistantProviderSlot first)
    {
        var candidate = _providers.FirstOrDefault(provider =>
            !string.Equals(provider.Name, first.Name, StringComparison.Ordinal));

        return candidate is not null && _health.IsAvailable(candidate.Name)
            ? candidate
            : null;
    }

    private AssistantProviderSlot SelectPreferred(string? conversationId)
    {
        // A zero-weight provider is never selected by the split, but it remains
        // available as a failover target.
        var weighted = _providers.Where(provider => provider.Weight > 0).ToArray();
        if (weighted.Length == 0)
        {
            weighted = _providers.ToArray();
        }

        var total = weighted.Sum(provider => Math.Max(provider.Weight, 1));
        var index = string.IsNullOrWhiteSpace(conversationId)
            ? (uint)(Interlocked.Increment(ref _roundRobin) % total)
            : StableIndex(conversationId, total);

        var cursor = 0;
        foreach (var provider in weighted)
        {
            cursor += Math.Max(provider.Weight, 1);
            if (index < cursor)
            {
                return provider;
            }
        }

        return weighted[^1];
    }

    private static uint StableIndex(string conversationId, int total)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conversationId));
        var value = BinaryPrimitives.ReadUInt32BigEndian(hash);
        return value % (uint)total;
    }
}
