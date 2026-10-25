namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// Provider credentials and model id resolved from the AI configuration
/// section (SPEC-009 S2, SPEC-022 S9). The API key only flows from
/// configuration into the Authorization header; it is never logged or embedded
/// in code (NFR-004). MaxAttempts lets the router cap retries per provider.
/// </summary>
public sealed record AssistantProviderOptions(
    string ApiKey,
    string Model,
    string Name = "provider",
    string? ReasoningEffort = null,
    int MaxAttempts = 2,
    bool NormalizeToolSchemas = false);
