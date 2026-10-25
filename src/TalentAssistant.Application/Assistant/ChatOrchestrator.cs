using System.Text.Encodings.Web;
using System.Text;
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
    QueryHcmData queryHcmData,
    AssistantOrchestratorOptions options)
{
    private const int MaximumToolResultBytes = 24 * 1024;
    private const int MaximumDirectoryResults = 25;
    private const int MaximumHistoryBytes = 32 * 1024;

    /// <summary>
    /// Closed tool catalog with one composable HR data query capability and
    /// source-owned domain tools. The model cannot extend the catalog.
    /// </summary>
    public static IReadOnlyList<AssistantToolDefinition> ToolCatalog { get; } =
        Array.AsReadOnly<AssistantToolDefinition>(
        [
            new(
                "search_employees",
                "Search by display name, surname fragment, and/or office/area. At least one filter is required; never call it unfiltered. Returns at most 25 matches; use query_hr_data for counts, groupings or complete lists. Results include approved surname, hire date, job title, area, gender, manager, email and derived AgeOnNextBirthday fields.",
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
                "Get employees with the next upcoming birthday and date from the HCM source, plus AgeOnNextBirthday (the age the employee turns on that date); ties are included and birth year is omitted.",
                """{"type":"object","properties":{},"required":[]}"""),
            new(
                "query_hr_data",
                "Read-only structured HR/HCM query over approved fields; never send SQL or code. Datasets and field ids: employees (employee_id, display_name, email, office_ids, area, gender, manager_employee_id, manager_display_name, years_in_company, birthday_month, birthday_day, job_title, last_name, hire_date, age_on_next_birthday); presence (presence_date, employee_id, employee_display_name, employee_office_ids, state); timeoff (reference, employee_id, type, status, starts_on, ends_before, duration, duration_unit, summary); tasks (reference, employee_id, title, status, due_on, summary); attendance_insights (incident_id, employee_id, employee_display_name, employee_office_ids, office_id, office_country, office_city, office_iana_time_zone, incident_at_utc, work_date, schedule_starts_at, schedule_ends_at, punches, rule_reference, rule_requires_review, rule_summary, rule_evaluated_at_utc, rule_evidence, rule_review_items, related_timeoff_reference, related_starts_on, related_ends_before, related_summary, related_type, related_status, related_duration, related_duration_unit). Salary, account number and raw birth year are not queryable. Select only required fields; max 250 rows and 64 KiB per result.",
                """{"type":"object","properties":{"dataset":{"type":["string","null"],"description":"One of employees, presence, timeoff, tasks, attendance_insights."},"fields":{"type":["array","null"],"items":{"type":"string"}},"filters":{"type":["array","object","null"],"description":"Array of filter objects, for example [{\"field\":\"birthday_month\",\"operator\":\"equals\",\"value\":\"11\"}]. Operators: equals, contains, greater_than, greater_than_or_equal, less_than, less_than_or_equal. Values may be strings or numbers."},"aggregate":{"type":["string","null"],"description":"count, min or max."},"aggregate_field":{"type":["string","null"]},"group_by":{"type":["array","null"],"items":{"type":"string"}},"order_by":{"type":["string","null"]},"order_direction":{"type":["string","null"],"description":"asc or desc."},"limit":{"type":["integer","null"]}},"required":["dataset"],"additionalProperties":false}""")
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
        var completedTurns = CompleteTurns(visible);

        // Keep whole user/assistant pairs: a count or byte cap that splits a
        // pair leaves an orphan turn the model cannot resolve. Newest pairs
        // win, and the result stays in chronological order.
        var kept = new List<ChatMessage>(completedTurns.Length);
        var bytes = 0;
        for (var index = completedTurns.Length - 2; index >= 0; index -= 2)
        {
            var user = completedTurns[index];
            var assistant = completedTurns[index + 1];
            var pairBytes = Encoding.UTF8.GetByteCount(user.Content)
                + Encoding.UTF8.GetByteCount(assistant.Content);
            if (kept.Count + 2 > options.MaxHistoryMessages
                || bytes + pairBytes > MaximumHistoryBytes)
            {
                break;
            }

            kept.InsertRange(0, [user, assistant]);
            bytes += pairBytes;
        }

        return kept;
    }

    // Only completed exchanges belong in the provider history: the Web keeps
    // the user turn of a failed call, and a run of unanswered questions makes
    // the model answer an old question again (observed live on 2026-10-25 with
    // "Cual es el correo de mauricio?" after five failed turns). A user message
    // is kept only when the immediately following message is its assistant
    // answer; trailing unanswered user messages are dropped.
    private static ChatMessage[] CompleteTurns(ChatMessage[] messages)
    {
        var completed = new List<ChatMessage>(messages.Length);
        for (var index = 0; index < messages.Length; index++)
        {
            if (messages[index].Role == "user"
                && index + 1 < messages.Length
                && messages[index + 1].Role == "assistant")
            {
                completed.Add(messages[index]);
                completed.Add(messages[index + 1]);
                index++;
            }
        }

        return completed.ToArray();
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
            "When you list tool records, copy each row's identifier and values exactly as returned for that"
            + " same row; never shift, merge, reorder or invent values between rows, and never invent an"
            + " employee id.",
            "Format the answer for a chat interface: short paragraphs, '-' bullet lists or numbered lists"
            + " when several records are listed, and **bold** only for key labels or values. Do not use"
            + " tables, links, images, HTML or fenced code blocks.",
            "For an employee's email, select the email field returned by the source and never construct or"
            + " guess an address.",
            "For questions outside HR/HCM (programming, general knowledge, translations, recipes, generic creative writing),"
            + " decline politely in the user's language: first remind them you are their HR/HCM assistant limited"
            + " to people and team data, then offer to help with an HR/HCM question instead; never provide the"
            + " unrelated content.",
            "For factual HR/HCM questions, use query_hr_data with the exact dataset and field ids from its tool description for flexible source-side selection, filters, counts, grouping, ranking and sorting; use domain tools when the source owns a specialized result such as upcoming birthdays. Never send SQL or code to a tool.",
            "The query_hr_data tool accepts a structured plan and is read-only; it accepts only approved data sets and non-sensitive fields. Request only fields needed to answer; for aggregates ask the source to compute the result instead of counting or ranking a large set of returned people in the model. If a result says Truncated is true, do not present its rows as a complete list; narrow the query or explain the limit.",
            "For headcounts, use query_hr_data with aggregate count and group_by: group by area for counts"
            + " per area and by gender for men/women counts (gender is F or M). area is the scalar"
            + " organizational area; office_ids is the array form kept for compatibility. For a manager"
            + " question, select manager_employee_id and manager_display_name and never infer the manager."
            + " Report years_in_company exactly as the source returns it.",
            "For query_hr_data filters, send an array of objects such as"
            + " [{\"field\":\"birthday_month\",\"operator\":\"equals\",\"value\":\"11\"}]. operator must be"
            + " exactly one of equals, contains, greater_than, greater_than_or_equal, less_than or"
            + " less_than_or_equal; numeric values may be sent as numbers.",
            "For factual questions about employees or the organization, call a data tool and wait for its result; job-offer drafts use the user's supplied requirements and do not require a data tool.",
            "Job-offer drafting is an allowed HR writing task: use only user-provided requirements. Include role, location, shift, salary, benefits, and work modality only when supplied; ask one concise follow-up if a missing detail is essential, otherwise omit it rather than inventing it. When requested, aim for about 500 characters and return the draft in chat only; never save or publish it.",
            "Employee salaries and bank account details remain restricted: never disclose or infer them, and say the information is unavailable if asked. A salary explicitly supplied by the user as an offer term may be repeated only in that job-offer draft.",
            "For a question seeking one employee's details, first call search_employees to resolve identity; it"
            + " requires a name, surname or office/area filter and returns at most 25 matches. If it returns"
            + " several candidates, list them and ask which one before querying details. For broad questions"
            + " (offices, headcounts, birthdays) use query_hr_data or upcoming_birthday, never an unfiltered"
            + " search_employees. A count or grouping by name fragment includes every match and never selects"
            + " one silently.",
            "For the next upcoming birthday, call upcoming_birthday; use the source-provided result and do not calculate it from employee birth data.",
            "Use the conversation history to resolve follow-ups: when a message refers back to an earlier turn"
            + " (for example 'esa persona', 'the person you mentioned' or an unnamed employee), take the referenced"
            + " name or value from the history and call the tools with those concrete values; ask which one only"
            + " when the history itself is ambiguous. Answer only the latest user message; use the history to"
            + " resolve references, never to answer an earlier question again.",
            "Do not rely on a memorized list of question templates: translate the requested HR/HCM result into a structured query over the available data sets, using multiple bounded calls only when the question spans data sets.",
            "Do not broaden or change a user's filters silently. A valid query with no matches is different from an invalid, unsupported or unavailable query; explain the observed outcome and never report a false zero.",
            "If a query is rejected or a field is unavailable, do not retry through another tool to bypass the restriction; state that the information is unavailable.",
            "When a question asks about age, report AgeOnNextBirthday exactly as the tool returned it (the age the"
            + " person turns at their next birthday); if it is null, say the information is not available.");
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
                        var name = arguments.GetString("name");
                        var office = arguments.GetString("office");
                        var surname = arguments.GetString("surname_contains");
                        if (string.IsNullOrWhiteSpace(name)
                            && string.IsNullOrWhiteSpace(office)
                            && string.IsNullOrWhiteSpace(surname))
                        {
                            return ToolError(
                                "Provide at least one of name, surname_contains or office;"
                                + " for counts or groupings use query_hr_data.");
                        }

                        var employees = await searchEmployees.ExecuteAsync(
                            new EmployeeSearchQuery(
                                Name: name,
                                OfficeId: office,
                                Language: language,
                                SurnameContains: surname),
                            cancellationToken);
                        var limited = employees.Count > MaximumDirectoryResults
                            ? employees.Take(MaximumDirectoryResults).ToArray()
                            : employees;
                        return SerializeToolResult(limited, "search_employees");
                    }

                case "upcoming_birthday":
                    {
                        var birthdays = await getUpcomingBirthdays.ExecuteAsync(
                            new UpcomingBirthdayQuery(
                                DateOnly.FromDateTime(DateTime.Today),
                                language),
                            cancellationToken);
                        return SerializeToolResult(birthdays, "upcoming_birthday");
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
                        return SerializeToolResult(snapshot, "presence");
                    }

                case "timeoff":
                    {
                        var requests = await getEmployeeTimeOff.ExecuteAsync(
                            new EmployeeDataQuery(
                                arguments.GetString("employee_id"),
                                arguments.GetString("name"),
                                language),
                            cancellationToken);
                        return SerializeToolResult(requests, "timeoff");
                    }

                case "tasks":
                    {
                        var tasks = await getEmployeeTasks.ExecuteAsync(
                            new EmployeeDataQuery(
                                arguments.GetString("employee_id"),
                                arguments.GetString("name"),
                                language),
                            cancellationToken);
                        return SerializeToolResult(tasks, "tasks");
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
                        return SerializeToolResult(insights, "attendance_insights");
                    }

                case "query_hr_data":
                    {
                        var request = arguments.GetHcmDataQueryRequest(language);
                        var result = await queryHcmData.ExecuteAsync(request, cancellationToken);
                        return SerializeToolResult(result, "query_hr_data");
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

    private static string SerializeToolResult(object? value, string toolName)
    {
        var json = JsonSerializer.Serialize(value, ToolResultSerializerOptions);
        return Encoding.UTF8.GetByteCount(json) > MaximumToolResultBytes
            ? ToolError(
                $"The {toolName} result exceeds the response limit; narrow the query or filters.")
            : json;
    }

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

        public HcmDataQueryRequest GetHcmDataQueryRequest(string language)
        {
            if (element is not { ValueKind: JsonValueKind.Object })
            {
                throw new HcmDataQueryValidationException("The query plan must be an object.");
            }

            EnsureProperties(
                element.Value,
                "dataset",
                "fields",
                "filters",
                "aggregate",
                "aggregate_field",
                "group_by",
                "order_by",
                "order_direction",
                "limit");

            var dataSet = RequiredString(element.Value, "dataset");
            return new HcmDataQueryRequest(
                dataSet,
                GetStringArray(element.Value, "fields"),
                GetFilters(element.Value),
                GetOptionalString(element.Value, "aggregate"),
                GetOptionalString(element.Value, "aggregate_field"),
                GetStringArray(element.Value, "group_by"),
                GetOptionalString(element.Value, "order_by"),
                GetOptionalString(element.Value, "order_direction"),
                GetOptionalInt32(element.Value, "limit"),
                language);
        }

        private static string RequiredString(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var value)
                || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new HcmDataQueryValidationException("The query data set is required.");
            }

            return value.GetString()!;
        }

        private static string? GetOptionalString(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var value)
                || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new HcmDataQueryValidationException("A query option has an invalid type.");
            }

            return value.GetString();
        }

        private static int? GetOptionalInt32(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var value)
                || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            {
                throw new HcmDataQueryValidationException("The query limit has an invalid type.");
            }

            return result;
        }

        private static IReadOnlyList<string>? GetStringArray(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var value)
                || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new HcmDataQueryValidationException("A query field list has an invalid type.");
            }

            var values = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    throw new HcmDataQueryValidationException("A query field list has an invalid item.");
                }

                values.Add(item.GetString()!);
            }

            return values;
        }

        private static IReadOnlyList<HcmDataQueryFilter>? GetFilters(JsonElement root)
        {
            if (!root.TryGetProperty("filters", out var value)
                || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                return [ParseFilter(value)];
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new HcmDataQueryValidationException("The query filters have an invalid type.");
            }

            var filters = new List<HcmDataQueryFilter>();
            foreach (var filter in value.EnumerateArray())
            {
                filters.Add(ParseFilter(filter));
            }

            return filters;
        }

        private static HcmDataQueryFilter ParseFilter(JsonElement filter)
        {
            // The provider may emit the compact [field, operator, value] form
            // instead of the documented object form. Both are accepted here;
            // the backend validator remains the authority on field/operator.
            if (filter.ValueKind == JsonValueKind.Array)
            {
                var parts = filter.EnumerateArray().ToArray();
                if (parts.Length != 3)
                {
                    throw new HcmDataQueryValidationException("A query filter has an invalid shape.");
                }

                return new HcmDataQueryFilter(
                    RequiredScalarString(parts[0]),
                    RequiredScalarString(parts[1]),
                    ScalarString(parts[2]));
            }

            if (filter.ValueKind != JsonValueKind.Object)
            {
                throw new HcmDataQueryValidationException("A query filter has an invalid shape.");
            }

            EnsureProperties(filter, "field", "operator", "value");
            return new HcmDataQueryFilter(
                RequiredString(filter, "field"),
                RequiredString(filter, "operator"),
                RequiredFilterValue(filter, "value"));
        }

        private static string RequiredFilterValue(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out var value))
            {
                throw new HcmDataQueryValidationException("A query filter is missing a value.");
            }

            return ScalarString(value);
        }

        private static string RequiredScalarString(JsonElement value) =>
            value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : throw new HcmDataQueryValidationException(
                    "A query filter is missing a field or operator.");

        private static string ScalarString(JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.String => value.GetString()!,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new HcmDataQueryValidationException(
                    "A query filter value has an invalid type.")
            };

        private static void EnsureProperties(JsonElement value, params string[] allowed)
        {
            var allowedProperties = new HashSet<string>(allowed, StringComparer.Ordinal);
            if (value.EnumerateObject().Any(property => !allowedProperties.Contains(property.Name)))
            {
                throw new HcmDataQueryValidationException("The query plan contains an unsupported property.");
            }
        }
    }
}
