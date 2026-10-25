using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TalentAssistant.Application.Assistant;

namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>One configured provider and its routing weight (SPEC-022 §3.1).</summary>
public sealed record AssistantProviderSlot(
    string Name,
    int Weight,
    IAssistantLanguageModel Model);

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
            try
            {
                var completion = await only.Model.CompleteAsync(request, cancellationToken);
                _health.RecordSuccess(only.Name);
                return completion;
            }
            catch (AssistantProviderUnavailableException exception)
            {
                _health.RecordFailure(only.Name);
                _logger.LogWarning(
                    "Assistant provider {Provider} failed with {Reason}.",
                    only.Name,
                    exception.Reason);
                throw;
            }
        }

        var first = ResolveFirstProvider(request.ConversationId);
        try
        {
            var completion = await first.Model.CompleteAsync(request, cancellationToken);
            _health.RecordSuccess(first.Name);
            _active = first;
            return completion;
        }
        catch (AssistantProviderUnavailableException firstFailure)
        {
            _health.RecordFailure(first.Name);
            _logger.LogWarning(
                "Assistant provider {Provider} failed with {Reason}; trying failover.",
                first.Name,
                firstFailure.Reason);

            var second = ResolveFailoverProvider(first);
            if (second is null)
            {
                throw;
            }

            try
            {
                var completion = await second.Model.CompleteAsync(request, cancellationToken);
                _health.RecordSuccess(second.Name);
                _active = second;
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
                    "Assistant provider {Provider} failed with {Reason} after failover.",
                    second.Name,
                    secondFailure.Reason);

                // Both were rate limited -> the user-facing "service busy"
                // message; otherwise report the last observed reason.
                var reason = firstFailure.Reason == AssistantProviderFailureReason.RateLimited
                    && secondFailure.Reason == AssistantProviderFailureReason.RateLimited
                        ? AssistantProviderFailureReason.RateLimited
                        : secondFailure.Reason;
                throw new AssistantProviderUnavailableException(
                    reason,
                    "Both assistant providers failed.",
                    secondFailure);
            }
        }
    }

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
