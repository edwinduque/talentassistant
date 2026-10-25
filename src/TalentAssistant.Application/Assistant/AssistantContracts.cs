namespace TalentAssistant.Application.Assistant;

/// <summary>
/// A single chat message in the OpenAI-compatible wire shape. Valid roles are
/// "system", "user", "assistant" and "tool". Tool-role messages carry the id of
/// the tool call they answer; assistant messages may carry the tool calls they
/// requested (SPEC-009 §8).
/// </summary>
public sealed record ChatMessage(
    string Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<AssistantToolCall>? ToolCalls = null);

/// <summary>
/// A tool the model may request. ParametersJson is a raw JSON Schema string
/// that the provider client embeds verbatim in the request body.
/// </summary>
public sealed record AssistantToolDefinition(
    string Name,
    string Description,
    string ParametersJson);

/// <summary>A tool call requested by the model; ArgumentsJson is the raw JSON argument string.</summary>
public sealed record AssistantToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

/// <summary>One model completion: either final content, or one or more tool calls.</summary>
public sealed record AssistantCompletion(
    string? Content,
    IReadOnlyList<AssistantToolCall> ToolCalls);

/// <summary>A completion request in the OpenAI-compatible shape the port abstracts.</summary>
public sealed record AssistantRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<AssistantToolDefinition> Tools,
    int MaxTokens,
    string? ConversationId = null);

/// <summary>
/// Language-model port (SPEC-009 S2). Infrastructure implements it over an
/// OpenAI-compatible HTTP endpoint; the orchestrator never talks to a provider.
/// </summary>
public interface IAssistantLanguageModel
{
    Task<AssistantCompletion> CompleteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default);
}
