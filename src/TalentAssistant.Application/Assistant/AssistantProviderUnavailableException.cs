namespace TalentAssistant.Application.Assistant;

/// <summary>
/// Closed set of provider failure reasons the API maps to controlled degraded
/// responses (SPEC-009 S2, AC-031).
/// </summary>
public enum AssistantProviderFailureReason
{
    NotConfigured,
    Unreachable,
    Timeout,
    RateLimited,
    InvalidResponse
}

/// <summary>
/// Raised when the assistant provider cannot produce a completion. The API
/// maps it to a controlled response (429 for RateLimited, 503 otherwise) and
/// the UI localizes the message; no stack trace or secret ever leaks (SPEC-009 §8).
/// </summary>
public sealed class AssistantProviderUnavailableException : Exception
{
    public AssistantProviderUnavailableException(
        AssistantProviderFailureReason reason,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? $"The assistant provider is unavailable ({reason}).", innerException)
    {
        Reason = reason;
    }

    public AssistantProviderFailureReason Reason { get; }
}
