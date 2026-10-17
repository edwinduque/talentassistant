namespace TalentAssistant.Application.Assistant;

/// <summary>
/// Orchestrator limits resolved from the AI configuration section (SPEC-009 §2).
/// </summary>
public sealed record AssistantOrchestratorOptions(
    int MaxToolRounds = 4,
    int MaxHistoryMessages = 20,
    int MaxTokens = 700);
