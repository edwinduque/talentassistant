using TalentAssistant.Application.Assistant;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// Deterministic tool-loop tests over a scripted fake LLM transport and the
/// real use cases backed by the synthetic fixtures: no network, no key
/// (SPEC-009 S1, S4; AC-030).
/// </summary>
public sealed class ChatOrchestratorTests
{
    [Fact]
    public async Task HappyPathExecutesTheToolAndReturnsTheFinalAnswer()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "search_employees", """{"name":"Juan"}""")
            ]));
        model.Enqueue(new AssistantCompletion(
            "Juan Pérez trabaja en la oficina de Madrid.",
            []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("¿Dónde trabaja Juan?", History: [], Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal("Juan Pérez trabaja en la oficina de Madrid.", result.Answer);
        Assert.Equal(["search_employees"], result.ToolsUsed);
        Assert.Equal(result.ToolsUsed, result.Sources);

        Assert.Equal(2, model.Requests.Count);

        // The second request carries the tool result with real fixture data.
        var followUp = model.Requests[1];
        var toolMessage = Assert.Single(
            followUp.Messages,
            message => message.Role == "tool");
        Assert.Contains("Juan Pérez", toolMessage.Content);
        Assert.Equal("call-1", toolMessage.ToolCallId);
    }

    [Fact]
    public async Task ToolResultsUseTheRequestedLanguage()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "search_employees", """{"name":"Synthetic"}""")
            ]));
        model.Enqueue(new AssistantCompletion("Those are the synthetic employees.", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("Who are the synthetic employees?", History: [], Language: "en"));

        Assert.True(result.Completed);

        var firstRequest = model.Requests[0];
        Assert.Contains(
            firstRequest.Messages,
            message => message.Role == "system" && message.Content.Contains("English"));

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        Assert.Contains("Synthetic employee", toolMessage.Content);
    }

    [Fact]
    public async Task UnknownToolSurvivesAndPassesAnErrorPayload()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "bogus_tool", "{}")
            ]));
        model.Enqueue(new AssistantCompletion("No dispongo de esa herramienta.", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("Hola", History: [], Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal("No dispongo de esa herramienta.", result.Answer);
        Assert.Equal(["bogus_tool"], result.ToolsUsed);

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        Assert.Contains("error", toolMessage.Content);
        Assert.Contains("Unknown tool 'bogus_tool'", toolMessage.Content);
    }

    [Fact]
    public async Task RoundsExhaustionReturnsNotCompletedAfterMaxToolRounds()
    {
        var options = new AssistantOrchestratorOptions(MaxToolRounds: 2);
        var model = new ScriptedLanguageModel();

        // The model only ever asks for tools; the script holds exactly
        // MaxToolRounds completions, so an extra round would throw.
        for (var round = 0; round < options.MaxToolRounds; round++)
        {
            model.Enqueue(new AssistantCompletion(
                Content: null,
                ToolCalls:
                [
                    new AssistantToolCall(
                        $"call-{round}",
                        "search_employees",
                        """{"name":"Juan"}""")
                ]));
        }

        var orchestrator = CreateOrchestrator(model, options);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("Bucle infinito", History: [], Language: "es"));

        Assert.False(result.Completed);
        Assert.Equal(string.Empty, result.Answer);
        Assert.Equal(options.MaxToolRounds, model.Requests.Count);
    }

    [Fact]
    public async Task HistoryIsTrimmedToMaxHistoryMessagesKeepingTheMostRecent()
    {
        var options = new AssistantOrchestratorOptions(MaxHistoryMessages: 3);
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion("ok", []));
        var orchestrator = CreateOrchestrator(model, options);

        IReadOnlyList<ChatMessage> history =
        [
            new("user", "h1"),
            new("assistant", "h2"),
            new("user", "h3"),
            new("assistant", "h4"),
            new("user", "h5")
        ];

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("latest question", history, Language: "es"));

        Assert.True(result.Completed);

        var messages = model.Requests[0].Messages;
        Assert.Equal(1 + options.MaxHistoryMessages + 1, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[^1].Role);
        Assert.Equal("latest question", messages[^1].Content);

        var trimmedHistory = messages
            .Skip(1)
            .Take(options.MaxHistoryMessages)
            .Select(message => message.Content)
            .ToArray();
        Assert.Equal(["h3", "h4", "h5"], trimmedHistory);
    }

    private static ChatOrchestrator CreateOrchestrator(
        ScriptedLanguageModel model,
        AssistantOrchestratorOptions? options = null)
    {
        var workforce = new SyntheticWorkforceSource();
        return new ChatOrchestrator(
            model,
            new SearchEmployees(workforce),
            new GetEmployeePresence(workforce),
            new GetEmployeeTimeOff(workforce),
            new GetEmployeeTasks(workforce),
            new GetAttendanceInsights(new SyntheticHcmAttendanceSource()),
            options ?? new AssistantOrchestratorOptions());
    }

    /// <summary>
    /// Scripted LLM transport: records every request it receives and replays a
    /// fixed queue of completions, throwing once the script is exhausted.
    /// </summary>
    private sealed class ScriptedLanguageModel : IAssistantLanguageModel
    {
        private readonly Queue<AssistantCompletion> _script = new();

        public List<AssistantRequest> Requests { get; } = [];

        public void Enqueue(AssistantCompletion completion) => _script.Enqueue(completion);

        public Task<AssistantCompletion> CompleteAsync(
            AssistantRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_script.Count == 0)
            {
                throw new InvalidOperationException(
                    "The scripted model received more requests than scripted.");
            }

            return Task.FromResult(_script.Dequeue());
        }
    }
}
