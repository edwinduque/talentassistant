using System.Text.Encodings.Web;
using System.Text.Json;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;

namespace TalentAssistant.Application.Assistant;

/// <summary>
/// A chat turn from the authenticated user: the message, the client-held
/// history and the resolved profile language (SPEC-009 §8).
/// </summary>
public sealed record ChatQuery(
    string Message,
    IReadOnlyList<ChatMessage> History,
    string Language);

/// <summary>
/// Chat answer with the ordered tool trace. In v1 Sources is the same list as
/// ToolsUsed: every answer is grounded on exactly the executed tools; a richer
/// source model (per-claim citations) is out of scope until requested.
/// </summary>
public sealed record ChatResult(
    string Answer,
    IReadOnlyList<string> ToolsUsed,
    IReadOnlyList<string> Sources,
    bool Completed);

/// <summary>
/// Fixed tool-loop orchestrator (SPEC-009 S1). The model only requests tools
/// from the closed catalog; every execution delegates to the existing
/// Application use cases with the user's session language, and the model never
/// touches data, network or permissions directly (SPEC-009 §3, ADR-0011).
/// No write tools exist (FR-015).
/// </summary>
public sealed class ChatOrchestrator(
    IAssistantLanguageModel languageModel,
    SearchEmployees searchEmployees,
    GetEmployeePresence getEmployeePresence,
    GetEmployeeTimeOff getEmployeeTimeOff,
    GetEmployeeTasks getEmployeeTasks,
    GetAttendanceInsights getAttendanceInsights,
    AssistantOrchestratorOptions options)
{
    /// <summary>
    /// The closed v1 catalog (SPEC-008 §6): search_employees, presence,
    /// timeoff, tasks and attendance_insights. The model cannot extend it.
    /// </summary>
    public static IReadOnlyList<AssistantToolDefinition> ToolCatalog { get; } =
        Array.AsReadOnly<AssistantToolDefinition>(
        [
            new(
                "search_employees",
                "Search the employee directory by display name and/or office id.",
                """{"type":"object","properties":{"name":{"type":["string","null"]},"office":{"type":["string","null"]}},"required":[]}"""),
            new(
                "presence",
                "Get the presence snapshot for a local date (yyyy-MM-dd, defaults to today).",
                """{"type":"object","properties":{"date":{"type":["string","null"],"description":"Local date as yyyy-MM-dd"}},"required":[]}"""),
            new(
                "timeoff",
                "Get the time off requests of an employee by employee id and/or name.",
                """{"type":"object","properties":{"employee_id":{"type":["string","null"]},"name":{"type":["string","null"]}},"required":[]}"""),
            new(
                "tasks",
                "Get the tasks of an employee by employee id and/or name.",
                """{"type":"object","properties":{"employee_id":{"type":["string","null"]},"name":{"type":["string","null"]}},"required":[]}"""),
            new(
                "attendance_insights",
                "Get attendance insights for a date range (from inclusive, to exclusive, both yyyy-MM-dd).",
                """{"type":"object","properties":{"from":{"type":["string","null"],"description":"Inclusive start date as yyyy-MM-dd"},"to":{"type":["string","null"],"description":"Exclusive end date as yyyy-MM-dd"}},"required":["from","to"]}""")
        ]);

    // Names for the requested language in system-prompt instructions (SPEC-009 S1).
    private static readonly IReadOnlyDictionary<string, string> LanguageNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["es"] = "Spanish",
            ["en"] = "English"
        };

    public async Task<ChatResult> ExecuteAsync(
        ChatQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var messages = new List<ChatMessage>
        {
            new("system", BuildSystemPrompt(query.Language))
        };

        var history = TrimHistory(query.History);
        messages.AddRange(history);
        messages.Add(new ChatMessage("user", query.Message));

        var toolsUsed = new List<string>();

        for (var round = 0; round < options.MaxToolRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var completion = await languageModel.CompleteAsync(
                new AssistantRequest(messages, ToolCatalog, options.MaxTokens),
                cancellationToken);

            if (completion.ToolCalls is not { Count: > 0 })
            {
                return new ChatResult(
                    completion.Content ?? string.Empty,
                    toolsUsed,
                    toolsUsed,
                    Completed: true);
            }

            // Keep the assistant turn with its tool calls in the transcript so
            // tool messages follow the OpenAI protocol the provider expects.
            messages.Add(new ChatMessage(
                "assistant",
                completion.Content ?? string.Empty,
                ToolCalls: completion.ToolCalls));

            foreach (var toolCall in completion.ToolCalls)
            {
                if (!toolsUsed.Contains(toolCall.Name))
                {
                    toolsUsed.Add(toolCall.Name);
                }

                var resultJson = await ExecuteToolAsync(
                    toolCall,
                    query.Language,
                    cancellationToken);
                messages.Add(new ChatMessage(
                    "tool",
                    resultJson,
                    ToolCallId: toolCall.Id));
            }
        }

        // Rounds exhausted without final content: the UI localizes this state (SPEC-009 §2).
        return new ChatResult(
            string.Empty,
            toolsUsed,
            toolsUsed,
            Completed: false);
    }

    private IReadOnlyList<ChatMessage> TrimHistory(IReadOnlyList<ChatMessage>? history)
    {
        if (history is not { Count: > 0 })
        {
            return Array.Empty<ChatMessage>();
        }

        var visible = history
            .Where(message => message is not null
                && !string.IsNullOrWhiteSpace(message.Role)
                && !string.IsNullOrWhiteSpace(message.Content))
            .ToArray();
        return visible.Length <= options.MaxHistoryMessages
            ? visible
            : visible[^options.MaxHistoryMessages..];
    }

    private static string BuildSystemPrompt(string language)
    {
        var languageName = LanguageNames.TryGetValue(language, out var name)
            ? name
            : LanguageNames["es"];
        return string.Join(
            ' ',
            $"You are Talent Assistant, an internal HR/HCM assistant for this company's employees. Always answer in {languageName}.",
            "Answer ONLY about HR/HCM topics and ONLY with data returned by the tools; never invent employees, dates or facts.",
            "When the tools return records that answer the question, present those record values as reported"
            + " (durations, dates, statuses); if the tools return no relevant data, say explicitly that the"
            + " information is not available.",
            "For questions outside HR/HCM (programming, general knowledge, translations, recipes, creative writing),"
            + " decline politely in the user's language: first remind them you are their HR/HCM assistant limited"
            + " to people and team data, then offer to help with an HR/HCM question instead; never provide the"
            + " unrelated content.",
            "Call a tool and wait for its result before answering.",
            "For questions about a specific person, first call search_employees with the name from the question;"
            + " if it matches several employees, list the matching candidates (display name and office) and ask"
            + " which one you mean before answering; never pick one silently.",
            "For leave or return-date questions use timeoff; use presence only for who is present, absent"
            + " or on leave right now.");
    }

    private async Task<string> ExecuteToolAsync(
        AssistantToolCall toolCall,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            var arguments = ParseArguments(toolCall.ArgumentsJson);

            switch (toolCall.Name)
            {
                case "search_employees":
                {
                    var employees = await searchEmployees.ExecuteAsync(
                        new EmployeeSearchQuery(
                            arguments.GetString("name"),
                            arguments.GetString("office"),
                            language),
                        cancellationToken);
                    return JsonSerializer.Serialize(employees, ToolResultSerializerOptions);
                }

                case "presence":
                {
                    var rawDate = arguments.GetString("date");
                    if (!TryParseDate(rawDate, out var date))
                    {
                        // A missing date means today; only a malformed value errors.
                        if (!string.IsNullOrWhiteSpace(rawDate))
                        {
                            return ToolError(
                                $"Invalid date '{rawDate}': provide the date as yyyy-MM-dd.");
                        }

                        date = DateOnly.FromDateTime(DateTime.Today);
                    }

                    var snapshot = await getEmployeePresence.ExecuteAsync(
                        new PresenceQuery(date, language),
                        cancellationToken);
                    return JsonSerializer.Serialize(snapshot, ToolResultSerializerOptions);
                }

                case "timeoff":
                {
                    var requests = await getEmployeeTimeOff.ExecuteAsync(
                        new EmployeeDataQuery(
                            arguments.GetString("employee_id"),
                            arguments.GetString("name"),
                            language),
                        cancellationToken);
                    return JsonSerializer.Serialize(requests, ToolResultSerializerOptions);
                }

                case "tasks":
                {
                    var tasks = await getEmployeeTasks.ExecuteAsync(
                        new EmployeeDataQuery(
                            arguments.GetString("employee_id"),
                            arguments.GetString("name"),
                            language),
                        cancellationToken);
                    return JsonSerializer.Serialize(tasks, ToolResultSerializerOptions);
                }

                case "attendance_insights":
                {
                    var rawFrom = arguments.GetString("from");
                    var rawTo = arguments.GetString("to");
                    if (!TryParseDate(rawFrom, out var from)
                        || !TryParseDate(rawTo, out var to))
                    {
                        return ToolError(
                            $"Invalid date range '{rawFrom}'..'{rawTo}': provide from and to as yyyy-MM-dd.");
                    }

                    var insights = await getAttendanceInsights.ExecuteAsync(
                        new AttendanceInsightQuery(from, to, null, language),
                        cancellationToken);
                    return JsonSerializer.Serialize(insights, ToolResultSerializerOptions);
                }

                default:
                    return ToolError(
                        $"Unknown tool '{toolCall.Name}'. Available tools: "
                        + string.Join(", ", ToolCatalog.Select(tool => tool.Name)) + ".");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ToolError($"Tool '{toolCall.Name}' failed: {exception.Message}");
        }
    }

    private static ToolArguments ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new ToolArguments(null);
        }

        try
        {
            return new ToolArguments(JsonDocument.Parse(argumentsJson).RootElement.Clone());
        }
        catch (JsonException)
        {
            return new ToolArguments(null);
        }
    }

    private static bool TryParseDate(string? rawDate, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            rawDate,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date);
    }

    // Tool results and errors are LLM-facing payloads: keep accents and
    // punctuation readable instead of \uXXXX-escaped (quotes and control
    // characters remain escaped, so the output is still valid JSON).
    private static readonly JsonSerializerOptions ToolResultSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string ToolError(string message) =>
        JsonSerializer.Serialize(new { error = message }, ToolResultSerializerOptions);

    /// <summary>
    /// Defensive argument reader: a missing or malformed argument object yields
    /// null values, which the use cases treat as "no filter".
    /// </summary>
    private readonly struct ToolArguments(JsonElement? element)
    {
        public string? GetString(string property) =>
            element is { ValueKind: JsonValueKind.Object }
            && element.Value.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
