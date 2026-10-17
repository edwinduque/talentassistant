namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// Provider credentials and model id resolved from the AI configuration
/// section (SPEC-009 S2). The API key only flows from configuration into the
/// Authorization header; it is never logged or embedded in code (NFR-004).
/// </summary>
public sealed record AssistantProviderOptions(
    string ApiKey,
    string Model);
