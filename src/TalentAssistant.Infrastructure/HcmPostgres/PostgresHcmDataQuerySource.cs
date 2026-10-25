using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TalentAssistant.Application.Hcm;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Common;

namespace TalentAssistant.Infrastructure.HcmPostgres;

/// <summary>
/// Executes validated query plans against the versioned HCM views. SQL
/// identifiers come only from this source-owned catalog; user/model values are
/// always bound parameters in a server-enforced read-only transaction.
/// </summary>
public sealed class PostgresHcmDataQuerySource(
    NpgsqlDataSource dataSource,
    HcmPostgresAdapterOptions options) : IHcmDataQuerySource
{
    private const string ReadOnlyTransactionSql = "SET TRANSACTION READ ONLY";
    private const string UnavailableMessage = "The HCM Postgres source is unavailable.";
    private const string ContractMismatchMessage =
        "The HCM Postgres source returned a payload that does not match the contract.";

    private static readonly IReadOnlyDictionary<string, PostgresDataSet> DataSets =
        CreateDataSets();

    public async Task<HcmDataQueryResult> ExecuteAsync(
        HcmValidatedDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var compiled = Compile(query);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                options.CommandTimeoutSeconds,
                cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = compiled.Sql;
            command.CommandTimeout = options.CommandTimeoutSeconds;
            command.Transaction = transaction;
            foreach (var parameter in compiled.Parameters)
            {
                var dbParameter = command.Parameters.Add(parameter.Name, parameter.Type);
                dbParameter.Value = parameter.Value ?? DBNull.Value;
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            var hasExtraRow = false;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (rows.Count == query.Limit)
                    {
                        hasExtraRow = true;
                        break;
                    }

                    var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (var index = 0; index < compiled.Columns.Count; index++)
                    {
                        var column = compiled.Columns[index];
                        row[column.OutputName] = ReadValue(
                            reader,
                            index,
                            column,
                            query.Language);
                    }

                    rows.Add(row);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new HcmDataQueryResult(rows, hasExtraRow);
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new HcmSourceUnavailableException(ContractMismatchMessage, exception);
        }
    }

    private static CompiledQuery Compile(HcmValidatedDataQuery query)
    {
        if (!DataSets.TryGetValue(query.DataSet.Name, out var dataSet))
        {
            throw new InvalidOperationException("The validated HCM data set has no PostgreSQL mapping.");
        }

        var parameters = new List<SqlParameterDefinition>();
        var columns = new List<OutputColumn>();
        var selectExpressions = new List<string>();

        if (query.Aggregate is null)
        {
            foreach (var field in query.Fields)
            {
                selectExpressions.Add(
                    $"{GetColumn(dataSet, field.Name, query.Language)} AS {Quote(field.Name)}");
                columns.Add(new OutputColumn(field.Name, field.Type, field.Name));
            }
        }
        else
        {
            foreach (var field in query.GroupBy)
            {
                selectExpressions.Add(
                    $"{GetColumn(dataSet, field.Name, query.Language)} AS {Quote(field.Name)}");
                columns.Add(new OutputColumn(field.Name, field.Type, field.Name));
            }

            var aggregateAlias = query.Aggregate.Value switch
            {
                HcmQueryAggregate.Count => "count",
                HcmQueryAggregate.Minimum => "minimum",
                HcmQueryAggregate.Maximum => "maximum",
                _ => throw new InvalidOperationException("The validated aggregate has no SQL mapping.")
            };
            var aggregateExpression = query.Aggregate.Value switch
            {
                HcmQueryAggregate.Count => "COUNT(*)",
                HcmQueryAggregate.Minimum =>
                    $"MIN({GetColumn(dataSet, query.AggregateField!.Name, query.Language)})",
                HcmQueryAggregate.Maximum =>
                    $"MAX({GetColumn(dataSet, query.AggregateField!.Name, query.Language)})",
                _ => throw new InvalidOperationException("The validated aggregate has no SQL mapping.")
            };
            selectExpressions.Add($"{aggregateExpression} AS {Quote(aggregateAlias)}");
            columns.Add(new OutputColumn(
                aggregateAlias,
                query.Aggregate == HcmQueryAggregate.Count
                    ? HcmQueryFieldType.Integer
                    : query.AggregateField!.Type,
                query.AggregateField?.Name));
        }

        var where = new List<string>();
        for (var index = 0; index < query.Filters.Count; index++)
        {
            var filter = query.Filters[index];
            var field = GetColumn(dataSet, filter.Field.Name, query.Language);
            var parameterName = $"query_{index}";
            var (expression, value, type) = BuildPredicate(field, filter, parameterName);
            where.Add(expression);
            parameters.Add(new SqlParameterDefinition(parameterName, type, value));
        }

        var sql = $"SELECT {string.Join(", ", selectExpressions)} FROM {Quote(dataSet.Schema)}.{Quote(dataSet.View)}";
        if (where.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", where);
        }

        if (query.GroupBy.Count > 0)
        {
            sql += " GROUP BY " + string.Join(
                ", ",
                query.GroupBy.Select(field => GetColumn(dataSet, field.Name, query.Language)));
        }

        if (query.OrderByAggregate)
        {
            sql += " ORDER BY " + Quote("count") + (query.Descending ? " DESC" : " ASC");
        }
        else if (query.OrderBy is not null)
        {
            sql += " ORDER BY "
                + GetColumn(dataSet, query.OrderBy.Name, query.Language)
                + (query.Descending ? " DESC" : " ASC");
        }

        var limitName = "query_limit_plus_one";
        sql += $" LIMIT @{limitName}";
        parameters.Add(new SqlParameterDefinition(
            limitName,
            NpgsqlDbType.Integer,
            query.Limit + 1));

        return new CompiledQuery(sql, parameters, columns);
    }

    private static (string Expression, object Value, NpgsqlDbType Type) BuildPredicate(
        string column,
        HcmValidatedQueryFilter filter,
        string parameterName)
    {
        if (filter.Operator == HcmQueryOperator.Contains
            && filter.Field.Type == HcmQueryFieldType.TextArray)
        {
            return ($"@{parameterName} = ANY({column})", filter.Value, NpgsqlDbType.Text);
        }

        if (filter.Operator == HcmQueryOperator.Contains)
        {
            var value = (string)filter.Value;
            var escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var textColumn = filter.Field.AccentInsensitive
                ? NormalizeNameColumn(column)
                : column;
            var likeOperator = filter.Field.AccentInsensitive ? "LIKE" : "ILIKE";
            return (
                $"{textColumn} {likeOperator} @{parameterName} ESCAPE E'\\\\'",
                $"%{escaped}%",
                NpgsqlDbType.Text);
        }

        var comparison = filter.Operator switch
        {
            HcmQueryOperator.Equals => "=",
            HcmQueryOperator.GreaterThan => ">",
            HcmQueryOperator.GreaterThanOrEqual => ">=",
            HcmQueryOperator.LessThan => "<",
            HcmQueryOperator.LessThanOrEqual => "<=",
            _ => throw new InvalidOperationException("The validated filter has no SQL mapping.")
        };

        var comparisonColumn = filter.Field.AccentInsensitive
            ? NormalizeNameColumn(column)
            : column;
        return (
            $"{comparisonColumn} {comparison} @{parameterName}",
            filter.Value,
            GetParameterType(filter.Field.Type));
    }

    private static string NormalizeNameColumn(string column) =>
        $"translate(lower({column}), 'áàäâãåéèëêíìïîóòöôõúùüûñç', 'aaaaaaeeeeiiiiooooouuuunc')";

    private static NpgsqlDbType GetParameterType(HcmQueryFieldType type) => type switch
    {
        HcmQueryFieldType.Text => NpgsqlDbType.Text,
        HcmQueryFieldType.Integer => NpgsqlDbType.Integer,
        HcmQueryFieldType.Decimal => NpgsqlDbType.Numeric,
        HcmQueryFieldType.Boolean => NpgsqlDbType.Boolean,
        HcmQueryFieldType.Date => NpgsqlDbType.Date,
        HcmQueryFieldType.Time => NpgsqlDbType.Time,
        HcmQueryFieldType.DateTimeOffset => NpgsqlDbType.TimestampTz,
        _ => throw new InvalidOperationException("The validated field type has no SQL parameter mapping.")
    };

    private static string GetColumn(PostgresDataSet dataSet, string fieldName, string language)
    {
        if (!dataSet.Columns.TryGetValue(fieldName, out var column))
        {
            throw new InvalidOperationException("The validated HCM field has no PostgreSQL mapping.");
        }

        var name = string.Equals(language, "en", StringComparison.Ordinal)
            ? column.EnglishColumn ?? column.SpanishColumn
            : column.SpanishColumn;
        return Quote(name);
    }

    private static object? ReadValue(
        NpgsqlDataReader reader,
        int ordinal,
        OutputColumn column,
        string language)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        if (string.Equals(column.OutputName, "count", StringComparison.Ordinal))
        {
            return reader.GetInt64(ordinal);
        }

        if (column.FieldType == HcmQueryFieldType.Json)
        {
            var json = reader.GetString(ordinal);
            return column.SourceField switch
            {
                "punches" => ReadPunches(json, language),
                "rule_evidence" or "rule_review_items" => ReadStringArray(json),
                _ => JsonDocument.Parse(json).RootElement.Clone()
            };
        }

        return column.FieldType switch
        {
            HcmQueryFieldType.Text => reader.GetString(ordinal),
            HcmQueryFieldType.TextArray => reader.GetFieldValue<string[]>(ordinal),
            HcmQueryFieldType.Integer => reader.GetInt32(ordinal),
            HcmQueryFieldType.Decimal => reader.GetDecimal(ordinal),
            HcmQueryFieldType.Boolean => reader.GetBoolean(ordinal),
            HcmQueryFieldType.Date => reader.GetFieldValue<DateOnly>(ordinal),
            HcmQueryFieldType.Time => reader.GetFieldValue<TimeOnly>(ordinal),
            HcmQueryFieldType.DateTimeOffset => reader.GetFieldValue<DateTimeOffset>(ordinal),
            _ => throw new InvalidOperationException("The validated field type has no PostgreSQL reader mapping.")
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadPunches(
        string json,
        string language)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The HCM punches field must contain an array.");
        }

        var english = string.Equals(language, "en", StringComparison.Ordinal);
        return document.RootElement.EnumerateArray()
            .Select(item => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["occurred_at_utc"] = DateTimeOffset.Parse(
                    item.GetProperty("occurred_at_utc").GetString()!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                ["description"] = item.GetProperty(
                    english ? "description_en" : "description_es").GetString()
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The HCM localized array field must contain an array.");
        }

        return document.RootElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : item.GetRawText())
            .ToArray();
    }

    private static async Task<NpgsqlTransaction> BeginReadOnlyTransactionAsync(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ReadOnlyTransactionSql;
            command.CommandTimeout = commandTimeoutSeconds;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static IReadOnlyDictionary<string, PostgresDataSet> CreateDataSets()
    {
        var dataSets = new[]
        {
            DataSet("employees", "hcm_contract", "talentassistant_employees",
                Columns(
                    ("employee_id", "employee_id"),
                    ("display_name", "display_name"),
                    ("email", "email"),
                    ("office_ids", "office_ids"),
                    ("area", "area"),
                    ("gender", "gender"),
                    ("manager_employee_id", "manager_employee_id"),
                    ("manager_display_name", "manager_display_name"),
                    ("years_in_company", "years_in_company"),
                    ("birthday_month", "birthday_month"),
                    ("birthday_day", "birthday_day"),
                    ("job_title", "job_title"),
                    ("last_name", "last_name"),
                    ("hire_date", "hire_date"),
                    ("age_on_next_birthday", "age_on_next_birthday"))),
            DataSet("presence", "hcm_contract", "talentassistant_presence",
                Columns(
                    ("presence_date", "presence_date"),
                    ("employee_id", "employee_id"),
                    ("employee_display_name", "employee_display_name"),
                    ("employee_office_ids", "employee_office_ids"),
                    ("state", "state"))),
            DataSet("timeoff", "hcm_contract", "talentassistant_timeoff",
                ColumnMappings(
                    Column("reference", "reference"),
                    Column("employee_id", "employee_id"),
                    LocalizedColumn("type", "type_es", "type_en"),
                    LocalizedColumn("status", "status_es", "status_en"),
                    Column("starts_on", "starts_on"),
                    Column("ends_before", "ends_before"),
                    Column("duration", "duration"),
                    LocalizedColumn("duration_unit", "duration_unit_es", "duration_unit_en"),
                    LocalizedColumn("summary", "summary_es", "summary_en"))),
            DataSet("tasks", "hcm_contract", "talentassistant_tasks",
                ColumnMappings(
                    Column("reference", "reference"),
                    Column("employee_id", "employee_id"),
                    LocalizedColumn("title", "title_es", "title_en"),
                    LocalizedColumn("status", "status_es", "status_en"),
                    Column("due_on", "due_on"),
                    LocalizedColumn("summary", "summary_es", "summary_en"))),
            DataSet("attendance_insights", "hcm_contract", "talentassistant_attendance_insights",
                ColumnMappings(
                    Column("incident_id", "incident_id"),
                    Column("employee_id", "employee_id"),
                    Column("employee_display_name", "employee_display_name"),
                    Column("employee_office_ids", "employee_office_ids"),
                    Column("office_id", "office_id"),
                    Column("office_country", "office_country"),
                    Column("office_city", "office_city"),
                    Column("office_iana_time_zone", "office_iana_time_zone"),
                    Column("incident_at_utc", "incident_at_utc"),
                    Column("work_date", "work_date"),
                    Column("schedule_starts_at", "schedule_starts_at"),
                    Column("schedule_ends_at", "schedule_ends_at"),
                    Column("punches", "punches"),
                    Column("rule_reference", "rule_reference"),
                    Column("rule_requires_review", "rule_requires_review"),
                    LocalizedColumn("rule_summary", "rule_summary_es", "rule_summary_en"),
                    Column("rule_evaluated_at_utc", "rule_evaluated_at_utc"),
                    LocalizedColumn("rule_evidence", "rule_evidence_es", "rule_evidence_en"),
                    LocalizedColumn("rule_review_items", "rule_review_items_es", "rule_review_items_en"),
                    Column("related_timeoff_reference", "related_timeoff_reference"),
                    Column("related_starts_on", "related_starts_on"),
                    Column("related_ends_before", "related_ends_before"),
                    LocalizedColumn("related_summary", "related_summary_es", "related_summary_en"),
                    LocalizedColumn("related_type", "related_type_es", "related_type_en"),
                    LocalizedColumn("related_status", "related_status_es", "related_status_en"),
                    Column("related_duration", "related_duration"),
                    LocalizedColumn(
                        "related_duration_unit",
                        "related_duration_unit_es",
                        "related_duration_unit_en")))
        };

        return dataSets.ToDictionary(dataSet => dataSet.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static PostgresDataSet DataSet(
        string name,
        string schema,
        string view,
        IReadOnlyDictionary<string, PostgresColumn> columns) =>
        new(name, schema, view, columns);

    private static IReadOnlyDictionary<string, PostgresColumn> Columns(
        params (string Field, string Column)[] columns) =>
        columns.ToDictionary(
            column => column.Field,
            column => new PostgresColumn(column.Column, null),
            StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, PostgresColumn> ColumnMappings(
        params PostgresColumnMapping[] columns) =>
        columns.ToDictionary(
            column => column.Field,
            column => new PostgresColumn(column.SpanishColumn, column.EnglishColumn),
            StringComparer.OrdinalIgnoreCase);

    private static PostgresColumnMapping Column(string field, string column) =>
        new(field, column, null);

    private static PostgresColumnMapping LocalizedColumn(
        string field,
        string spanishColumn,
        string englishColumn) =>
        new(field, spanishColumn, englishColumn);

    private sealed record PostgresDataSet(
        string Name,
        string Schema,
        string View,
        IReadOnlyDictionary<string, PostgresColumn> Columns);

    private sealed record PostgresColumn(string SpanishColumn, string? EnglishColumn);
    private sealed record PostgresColumnMapping(
        string Field,
        string SpanishColumn,
        string? EnglishColumn);
    private sealed record SqlParameterDefinition(string Name, NpgsqlDbType Type, object Value);
    private sealed record OutputColumn(string OutputName, HcmQueryFieldType FieldType, string? SourceField);
    private sealed record CompiledQuery(
        string Sql,
        IReadOnlyList<SqlParameterDefinition> Parameters,
        IReadOnlyList<OutputColumn> Columns);
}
