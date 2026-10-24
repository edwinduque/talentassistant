using System.Text.Json;
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
    public async Task EmployeeSearchToolReturnsOnlyApprovedDirectoryFields()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "search_employees", """{"name":"Mauricio"}""")
            ]));
        model.Enqueue(new AssistantCompletion("Mauricio Cardenas es Técnico de nómina.", []));
        var orchestrator = CreateOrchestrator(model);

        await orchestrator.ExecuteAsync(
            new ChatQuery("¿Qué cargo tiene Mauricio?", History: [], Language: "es"));

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        using var payload = JsonDocument.Parse(toolMessage.Content);
        var employee = Assert.Single(payload.RootElement.EnumerateArray().ToArray());
        var fields = employee.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["DisplayName", "HireDate", "Id", "JobTitle", "LastName", "OfficeIds"],
            fields);
        Assert.Equal("Técnico de nómina", employee.GetProperty("JobTitle").GetString());
        Assert.Equal("Cardenas", employee.GetProperty("LastName").GetString());
        Assert.Equal("2018-09-17", employee.GetProperty("HireDate").GetString());
        Assert.DoesNotContain("salary", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bankAccount", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iban", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SurnameSearchToolFindsEmployeesBySurnameFragment()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "search_employees", """{"surname_contains":"z"}""")
            ]));
        model.Enqueue(new AssistantCompletion("Juan Pérez tiene una z en el apellido.", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery(
                "hay alguien con apellido que tenga z",
                History: [],
                Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal(["search_employees"], result.ToolsUsed);

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        using var payload = JsonDocument.Parse(toolMessage.Content);
        var employee = Assert.Single(payload.RootElement.EnumerateArray().ToArray());
        Assert.Equal("Juan Pérez", employee.GetProperty("DisplayName").GetString());
        Assert.Equal("Pérez", employee.GetProperty("LastName").GetString());
    }

    [Fact]
    public async Task UpcomingBirthdayToolReturnsOnlyTheNextBirthdayAndEmployeeIdentity()
    {
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "upcoming_birthday", "{}")
            ]));
        model.Enqueue(new AssistantCompletion("El resultado se recibió de la fuente HCM.", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery(
                "Quien es la proxima persona que cumple años",
                History: [],
                Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal(["upcoming_birthday"], result.ToolsUsed);

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        using var payload = JsonDocument.Parse(toolMessage.Content);
        var employees = payload.RootElement.EnumerateArray().ToArray();
        var employee = Assert.Single(employees);
        var fields = employee.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expected = new[]
            {
                (DisplayName: "Juan Pérez", Month: 4, Day: 12),
                (DisplayName: "Pedro Rojas", Month: 7, Day: 23),
                (DisplayName: "Mauricio Cardenas", Month: 11, Day: 30),
                (DisplayName: "Juan Herrera", Month: 2, Day: 18),
                (DisplayName: "Empleado sintético 001", Month: 0, Day: 0),
                (DisplayName: "Empleado sintético 005", Month: 5, Day: 9),
                (DisplayName: "Empleado sintético 004", Month: 9, Day: 27),
                (DisplayName: "Empleado sintético 002", Month: 12, Day: 2),
                (DisplayName: "Empleado sintético 003", Month: 3, Day: 14)
            }
            .Where(birthday => birthday.Month > 0)
            .Select(birthday =>
            {
                var nextBirthday = new DateOnly(today.Year, birthday.Month, birthday.Day);
                if (nextBirthday < today)
                {
                    nextBirthday = nextBirthday.AddYears(1);
                }

                return (birthday.DisplayName, NextBirthdayOn: nextBirthday);
            })
            .OrderBy(birthday => birthday.NextBirthdayOn)
            .First();

        Assert.Equal(["DisplayName", "EmployeeId", "NextBirthdayOn"], fields);
        Assert.Equal(expected.DisplayName, employee.GetProperty("DisplayName").GetString());
        Assert.Equal(
            expected.NextBirthdayOn.ToString("yyyy-MM-dd"),
            employee.GetProperty("NextBirthdayOn").GetString());
        Assert.DoesNotContain("BirthdayDate", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salary", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bankAccount", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NullPresenceDateIsNormalizedToTodayInsteadOfAFailedTool()
    {
        // Observed against the live provider: the model expresses "defaults to
        // today" as {"date":null}. The schema must accept null (see catalog
        // test) and the executor must treat it as absent, so the follow-up
        // tool message carries today's real snapshot instead of a tool error.
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(
            Content: null,
            ToolCalls:
            [
                new AssistantToolCall("call-1", "presence", """{"date":null}""")
            ]));
        model.Enqueue(new AssistantCompletion("Hoy hay empleados presentes.", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("¿Quién está presente hoy?", History: [], Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal(["presence"], result.ToolsUsed);

        var toolMessage = Assert.Single(
            model.Requests[1].Messages,
            message => message.Role == "tool");
        Assert.DoesNotContain("error", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Juan Pérez", toolMessage.Content);
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
    public async Task SystemPromptInstructsAskingWhichEmployeeWhenTheNameMatchesSeveral()
    {
        // SPEC-009 §3/§8 as amended 2026-10-18 (AC-032): the system prompt must
        // carry the disambiguation rule so the model lists the candidates and
        // asks which person it means before answering.
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion("¿Cuál de los dos quieres decir?", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery(
                "Dime cuantos dias de vacaciones tiene Juan.",
                History: [],
                Language: "es"));

        Assert.True(result.Completed);
        var system = model.Requests[0].Messages[0];
        Assert.Equal("system", system.Role);
        Assert.Contains("ask which one", system.Content);

        // The candidate lookup must be mandatory and FIRST for person
        // questions: only search_employees makes the ambiguity visible to the
        // model (timeoff resolves ids internally and hides it), live-observed
        // when a direct timeoff call answered "Juan" without asking (2026-10-18).
        Assert.Contains("first call search_employees", system.Content);
    }

    [Fact]
    public async Task SystemPromptBindsTheAssistantToTheHrmScopeAndAntiHallucinationRules()
    {
        // SPEC-010 S1: scope bound as the operative guardrail. The live
        // adversarial proofs run in T2 (scripts/verify-assistant-guardrails.ps1);
        // these pins fail first if the instruction is dropped or reworded away
        // from the agreed contract (AC-033/AC-034).
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion("ok", []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery("Hola", History: [], Language: "es"));

        Assert.True(result.Completed);
        var system = model.Requests[0].Messages[0];
        Assert.Equal("system", system.Role);
        Assert.Contains("HR/HCM", system.Content);
        Assert.Contains("outside HR/HCM", system.Content);
        Assert.Contains("decline politely", system.Content);
        Assert.Contains("never invent employees, dates or facts", system.Content);
        Assert.Contains("information is not available", system.Content);
        Assert.Contains("salary", system.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bank account", system.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hire-date questions", system.Content);
        Assert.Contains("surname_contains", system.Content);
    }

    [Fact]
    public async Task JobOfferDraftingUsesOnlyUserProvidedTermsAndStaysInChat()
    {
        const string draft = "Oferta de vigilante para Bogotá, con turnos rotativos y salario de 2.500.000 COP.";
        var model = new ScriptedLanguageModel();
        model.Enqueue(new AssistantCompletion(draft, []));
        var orchestrator = CreateOrchestrator(model);

        var result = await orchestrator.ExecuteAsync(
            new ChatQuery(
                "Quiero crear una oferta de vigilante para Bogotá, horarios rotativos, salario 2.500.000 pesos colombianos.",
                History: [],
                Language: "es"));

        Assert.True(result.Completed);
        Assert.Equal(draft, result.Answer);
        Assert.Empty(result.ToolsUsed);

        var system = model.Requests[0].Messages[0].Content;
        Assert.Contains("job-offer drafting", system, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only user-provided requirements", system);
        Assert.Contains("role, location, shift, salary, benefits, and work modality only when supplied", system);
        Assert.Contains("ask one concise follow-up", system);
        Assert.Contains("omit it rather than inventing it", system);
        Assert.Contains("about 500 characters", system);
        Assert.Contains("never save or publish", system);
        Assert.Contains("employee salaries", system, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offer term", system);
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
            new GetUpcomingBirthdays(workforce),
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
