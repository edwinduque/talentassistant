namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// In-memory circuit breaker per provider (SPEC-022 §3.4). Keeps the number of
/// consecutive provider failures and a cooldown deadline; no state is shared
/// across processes or persisted.
/// </summary>
public sealed class AssistantProviderHealth
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ProviderState> _states =
        new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly int _failureThreshold;
    private readonly TimeSpan _cooldown;

    public AssistantProviderHealth(
        TimeProvider timeProvider,
        int failureThreshold,
        TimeSpan cooldown)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (failureThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(failureThreshold));
        }

        if (cooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        }

        _timeProvider = timeProvider;
        _failureThreshold = failureThreshold;
        _cooldown = cooldown;
    }

    public bool IsAvailable(string providerName)
    {
        lock (_gate)
        {
            return !_states.TryGetValue(providerName, out var state)
                || state.UnavailableUntil is null
                || _timeProvider.GetUtcNow() >= state.UnavailableUntil;
        }
    }

    public void RecordSuccess(string providerName)
    {
        lock (_gate)
        {
            _states[providerName] = new ProviderState();
        }
    }

    public void RecordFailure(string providerName)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(providerName, out var state))
            {
                state = new ProviderState();
                _states[providerName] = state;
            }

            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures >= _failureThreshold)
            {
                // A failed probe after the cooldown re-opens the breaker: the
                // failure count is intentionally not reset here.
                state.UnavailableUntil = _timeProvider.GetUtcNow() + _cooldown;
            }
        }
    }

    private sealed class ProviderState
    {
        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset? UnavailableUntil { get; set; }
    }
}
