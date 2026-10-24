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
    GetUpcomingBirthdays getUpcomingBirthdays,
    GetEmployeePresence getEmployeePresence,
    GetEmployeeTimeOff getEmployeeTimeOff,
    GetEmployeeTasks getEmployeeTasks,
    GetAttendanceInsights getAttendanceInsights,
    AssistantOrchestratorOptions options)
{
    /// <summary>
    /// The closed catalog (SPEC-009 §6): search_employees, presence, timeoff,
    /// tasks, attendance_insights and upcoming_birthday. The model cannot extend it.
    /// </summary>
    public static IReadOnlyList<AssistantToolDefinition> ToolCatalog { get; } =
        Array.AsReadOnly<AssistantToolDefinition>(
        [
            new(
                "search_employees",
                "Search by display name, surname fragment, and/or office id; results include approved surname, hire date, and job title fields.",
                """{"type":"object","properties":{"name":{"type":["string","null"]},"office":{"type":["string","null"]},"surname_contains":{"type":["string","null"],"description":"Substring in the employee's surname"}},"required":[]}"""),
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
                """{"type":"object","properties":{"from":{"type":["string","null"],"description":"Inclusive start date as yyyy-MM-dd"},"to":{"type":["string","null"],"description":"Exclusive end date as yyyy-MM-dd"}},"required":["from","to"]}"""),
            new(
                "upcoming_birthday",
                "Get employees with the next upcoming birthday and date from the HCM source; ties are included and birth year is omitted.",
                """{"type":"object","properties":{},"required":[]}""")
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
            "Answer ONLY about HR/HCM topics. For factual questions about employees or the organization, use ONLY data returned by tools; never invent employees, dates or facts.",
            "When the tools return records that answer the question, present those record values as reported"
            + " (durations, dates, statuses); if the tools return no relevant data, say explicitly that the"
            + " information is not available.",
            "For questions outside HR/HCM (programming, general knowledge, translations, recipes, generic creative writing),"
            + " decline politely in the user's language: first remind them you are their HR/HCM assistant limited"
            + " to people and team data, then offer to help with an HR/HCM question instead; never provide the"
            + " unrelated content.",
            "For factual questions about employees or the organization, call a tool and wait for its result; job-offer drafts use the user's supplied requirements and do not require a data tool.",
            "Job-offer drafting is an allowed HR writing task: use only user-provided requirements. Include role, location, shift, salary, benefits, and work modality only when supplied; ask one concise follow-up if a missing detail is essential, otherwise omit it rather than inventing it. When requested, aim for about 500 characters and return the draft in chat only; never save or publish it.",
            "Employee salaries and bank account details remain restricted: never disclose or infer them, and say the information is unavailable if asked. A salary explicitly supplied by the user as an offer term may be repeated only in that job-offer draft.",
            "For questions about a specific person, first call search_employees with the name from the question;"
            + " if it matches several employees, list the matching candidates (display name and office) and ask"
            + " which one you mean before answering; never pick one silently.",
            "For hire-date questions, use the employee's name with search_employees and report the returned HireDate;"
            + " for surname-fragment questions, use search_employees with surname_contains and report all matches.",
            "For leave or return-date questions use timeoff; use presence only for who is present, absent"
            + " or on leave right now.",
            "For the next upcoming birthday, call upcoming_birthday; use the source-provided result and do not calculate it from employee birth data.");
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
                                Name: arguments.GetString("name"),
                                OfficeId: arguments.GetString("office"),
                                Language: language,
                                SurnameContains: arguments.GetString("surname_contains")),
                            cancellationToken);
                        return JsonSerializer.Serialize(employees, ToolResultSerializerOptions);
                    }

                case "upcoming_birthday":
                    {
                        var birthdays = await getUpcomingBirthdays.ExecuteAsync(
                            new UpcomingBirthdayQuery(
                                DateOnly.FromDateTime(DateTime.Today),
                                language),
                            cancellationToken);
                        return JsonSerializer.Serialize(birthdays, ToolResultSerializerOptions);
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
