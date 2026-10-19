using System.Data;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Hcm;

namespace TalentAssistant.Infrastructure.HcmPostgres;

/// <summary>
/// Read-only PostgreSQL implementation of the attendance port (SPEC-011 S2).
/// One fixed, parameterized statement against the fully qualified contract view
/// per operation, executed inside a READ ONLY transaction with the configured
/// command timeout. The view already carries the rule evaluated by the HCM and
/// the bilingual text of every localized field: <c>_es</c>/<c>_en</c> columns
/// and the <c>description_es</c>/<c>description_en</c> items of the jsonb
/// arrays are picked by the query language (unknown language falls back to
/// Spanish, matching the synthetic source). Related-TimeOff columns that are
/// null yield a null context. Any Npgsql or payload failure surfaces as
/// <see cref="HcmSourceUnavailableException"/>: no retries, no synthetic
/// fallback, no logged values.
/// </summary>
public sealed class PostgresAttendanceSource(
    NpgsqlDataSource dataSource,
    HcmPostgresAdapterOptions options) : IHcmAttendanceSource
{
    private const string EnglishLanguage = "en";
    private const string UnavailableMessage = "The HCM Postgres source is unavailable.";
    private const string ContractMismatchMessage =
        "The HCM Postgres source returned a payload that does not match the contract.";

    private const string ReadOnlyTransactionSql = "SET TRANSACTION READ ONLY";

    // The projection order below is this statement's exact column order; each
    // reader index in ReadInsight/ReadRelatedTimeOff maps one position here
    // (34 columns, 0-based):
    //  0 incident_id, 1 employee_id, 2 employee_display_name,
    //  3 employee_office_ids, 4 office_id, 5 office_country, 6 office_city,
    //  7 office_iana_time_zone, 8 incident_at_utc, 9 work_date,
    // 10 schedule_starts_at, 11 schedule_ends_at, 12 punches, 13 rule_reference,
    // 14 rule_requires_review, 15 rule_summary_es, 16 rule_summary_en,
    // 17 rule_evaluated_at_utc, 18 rule_evidence_es, 19 rule_evidence_en,
    // 20 rule_review_items_es, 21 rule_review_items_en,
    // 22 related_timeoff_reference, 23 related_starts_on, 24 related_ends_before,
    // 25 related_summary_es, 26 related_summary_en, 27 related_type_es,
    // 28 related_type_en, 29 related_status_es, 30 related_status_en,
    // 31 related_duration, 32 related_duration_unit_es, 33 related_duration_unit_en
    private const string InsightsSql = """
        SELECT incident_id, employee_id, employee_display_name, employee_office_ids,
               office_id, office_country, office_city, office_iana_time_zone,
               incident_at_utc, work_date, schedule_starts_at, schedule_ends_at,
               punches, rule_reference, rule_requires_review,
               rule_summary_es, rule_summary_en, rule_evaluated_at_utc,
               rule_evidence_es, rule_evidence_en, rule_review_items_es, rule_review_items_en,
               related_timeoff_reference, related_starts_on, related_ends_before,
               related_summary_es, related_summary_en, related_type_es, related_type_en,
               related_status_es, related_status_en, related_duration,
               related_duration_unit_es, related_duration_unit_en
        FROM hcm_contract.talentassistant_attendance_insights
        WHERE work_date >= @from_date AND work_date < @to_date
          AND (@employee_id IS NULL OR employee_id = @employee_id)
        ORDER BY incident_id
        """;

    public async Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FromInclusive >= query.ToExclusive)
        {
            throw new ArgumentException(
                "The start date must be earlier than the exclusive end date.",
                nameof(query));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var english = IsEnglish(query.Language);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                cancellationToken);
            await using var command = CreateCommand(connection, transaction, InsightsSql);
            AddParameter(command, "from_date", NpgsqlDbType.Date, query.FromInclusive);
            AddParameter(command, "to_date", NpgsqlDbType.Date, query.ToExclusive);
            AddParameter(command, "employee_id", NpgsqlDbType.Text, query.EmployeeId);

            var insights = new List<HcmAttendanceInsight>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    insights.Add(ReadInsight(reader, english));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return insights;
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            // A payload that violates the view contract degrades the same way:
            // an honest "source unavailable" instead of a raw 500.
            throw new HcmSourceUnavailableException(ContractMismatchMessage, exception);
        }
    }

    /// <summary>
    /// Maps one row of the denormalized contract view; every index matches the
    /// projection order documented at <see cref="InsightsSql"/>.
    /// </summary>
    private static HcmAttendanceInsight ReadInsight(NpgsqlDataReader reader, bool english)
    {
        var employee = new HcmEmployeeReference(
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<string[]>(3));
        var office = new HcmOfficeReference(
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7));

        var schedule = new HcmWorkSchedule(
            reader.GetFieldValue<DateOnly>(9),
            reader.GetFieldValue<TimeOnly>(10),
            reader.GetFieldValue<TimeOnly>(11));

        var ruleFinding = new HcmRuleFinding(
            reader.GetString(13),
            reader.GetBoolean(14),
            Pick(reader, 15, 16, english),
            reader.GetFieldValue<DateTimeOffset>(17),
            ReadTextArray(reader.GetString(english ? 19 : 18)),
            ReadTextArray(reader.GetString(english ? 21 : 20)));

        return new HcmAttendanceInsight(
            reader.GetString(0),
            employee,
            office,
            reader.GetFieldValue<DateTimeOffset>(8),
            schedule,
            ReadPunches(reader.GetString(12), english),
            ruleFinding,
            ReadRelatedTimeOff(reader, english));
    }

    /// <summary>
    /// Null related-TimeOff columns yield a null context (the port's DTO has no
    /// representation for an open-ended related request, so none is invented).
    /// </summary>
    private static HcmTimeOffContext? ReadRelatedTimeOff(NpgsqlDataReader reader, bool english)
    {
        if (reader.IsDBNull(22) || reader.IsDBNull(23) || reader.IsDBNull(24))
        {
            return null;
        }

        return new HcmTimeOffContext(
            reader.GetString(22),
            reader.GetFieldValue<DateOnly>(23),
            reader.GetFieldValue<DateOnly>(24),
            Pick(reader, 25, 26, english),
            PickNullable(reader, 27, 28, english),
            PickNullable(reader, 29, 30, english),
            reader.IsDBNull(31) ? null : reader.GetString(31),
            PickNullable(reader, 32, 33, english));
    }

    /// <summary>
    /// Reads the <c>punches</c> jsonb array; each item carries the source-owned
    /// timestamp plus the localized description pair picked by language.
    /// </summary>
    private static IReadOnlyList<HcmPunch> ReadPunches(string json, bool english)
    {
        var punches = new List<HcmPunch>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The punches column must hold a JSON array.");
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var rawTimestamp = RequiredText(item, "occurred_at_utc");
            if (!DateTimeOffset.TryParse(
                    rawTimestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var occurredAtUtc))
            {
                throw new FormatException("A punch timestamp is not a valid DateTimeOffset.");
            }

            punches.Add(new HcmPunch(
                occurredAtUtc,
                PickText(item, "description_es", "description_en", english)));
        }

        return punches;
    }

    /// <summary>Reads a jsonb array of localized strings (evidence, review items).</summary>
    private static IReadOnlyList<string> ReadTextArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("An evidence column must hold a JSON array.");
        }

        var values = new List<string>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            values.Add(item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : item.GetRawText());
        }

        return values;
    }

    private static string RequiredText(JsonElement item, string property)
    {
        if (item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        throw new JsonException($"The contract payload is missing the '{property}' property.");
    }

    /// <summary>
    /// Picks the requested language inside a jsonb item; a missing translation
    /// falls back to the available one (the contract may declare a single
    /// language for both).
    /// </summary>
    private static string PickText(
        JsonElement item,
        string spanishProperty,
        string englishProperty,
        bool english)
    {
        var preferred = english ? englishProperty : spanishProperty;
        var fallback = english ? spanishProperty : englishProperty;
        if (item.TryGetProperty(preferred, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        if (item.TryGetProperty(fallback, out value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<NpgsqlTransaction> BeginReadOnlyTransactionAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            await using var command = CreateCommand(connection, transaction, ReadOnlyTransactionSql);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            // The transaction never became usable: roll it back explicitly
            // before the failure (mapped upstream) leaves this scope.
            await transaction.DisposeAsync();
            throw;
        }
    }

    private NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        return command;
    }

    private static void AddParameter(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        // Every value reaches the database as a bound parameter; nothing is
        // ever concatenated into a statement (SPEC-011 AC-037).
        var parameter = command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }

    /// <summary>
    /// Unknown language falls back to Spanish, matching the synthetic source
    /// (only the exact "en" selects English - SPEC-007 §8).
    /// </summary>
    private static bool IsEnglish(string language) =>
        string.Equals(language, EnglishLanguage, StringComparison.Ordinal);

    private static string Pick(
        NpgsqlDataReader reader,
        int spanishOrdinal,
        int englishOrdinal,
        bool english) =>
        english ? reader.GetString(englishOrdinal) : reader.GetString(spanishOrdinal);

    private static string? PickNullable(
        NpgsqlDataReader reader,
        int spanishOrdinal,
        int englishOrdinal,
        bool english)
    {
        var ordinal = english ? englishOrdinal : spanishOrdinal;
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
