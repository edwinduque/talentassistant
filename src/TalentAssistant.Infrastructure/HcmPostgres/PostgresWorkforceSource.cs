using System.Data;
using Npgsql;
using NpgsqlTypes;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Hcm;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Common;

namespace TalentAssistant.Infrastructure.HcmPostgres;

/// <summary>
/// Read-only PostgreSQL implementation of the workforce port (SPEC-011 S2).
/// Every statement is a fixed, parameterized query against the fully qualified
/// contract views (no search-path dependency, values only ever bound as
/// Npgsql parameters); each operation opens one connection, runs inside a
/// READ ONLY transaction with the configured command timeout, commits and
/// closes. The office filter runs in SQL; the name filter folds accents in C#
/// through <see cref="NameMatching"/>, the same matcher the synthetic source
/// uses, so a name query resolves the same employees in both modes. Any
/// Npgsql failure surfaces as <see cref="HcmSourceUnavailableException"/>:
/// no retries, no fallback to synthetic data, and no logged values.
/// </summary>
public sealed class PostgresWorkforceSource(
    NpgsqlDataSource dataSource,
    HcmPostgresAdapterOptions options) : IHcmWorkforceSource
{
    private const string EnglishLanguage = "en";
    private const string UnavailableMessage = "The HCM Postgres source is unavailable.";

    // Npgsql 10 exposes no read-only transaction option, so the READ ONLY mode
    // is asserted with PostgreSQL's own statement as the FIRST statement of the
    // transaction (SPEC-011 AC-037): server-enforced, not convention.
    private const string ReadOnlyTransactionSql = "SET TRANSACTION READ ONLY";

    private const string SearchEmployeesSql = """
        SELECT employee_id, display_name, office_ids
        FROM hcm_contract.talentassistant_employees
        WHERE @office_id IS NULL OR office_ids && ARRAY[@office_id]
        ORDER BY employee_id
        """;

    private const string DirectorySql = """
        SELECT employee_id, display_name
        FROM hcm_contract.talentassistant_employees
        ORDER BY employee_id
        """;

    private const string PresenceSql = """
        SELECT employee_id, employee_display_name, employee_office_ids, state
        FROM hcm_contract.talentassistant_presence
        WHERE presence_date = @presence_date
        ORDER BY employee_id
        """;

    private const string TimeOffSql = """
        SELECT reference, employee_id, type_es, type_en, status_es, status_en,
               starts_on, ends_before, duration, duration_unit_es, duration_unit_en,
               summary_es, summary_en
        FROM hcm_contract.talentassistant_timeoff
        WHERE @employee_ids IS NULL OR employee_id = ANY(@employee_ids)
        ORDER BY reference
        """;

    private const string TasksSql = """
        SELECT reference, employee_id, title_es, title_en, status_es, status_en,
               due_on, summary_es, summary_en
        FROM hcm_contract.talentassistant_tasks
        WHERE @employee_ids IS NULL OR employee_id = ANY(@employee_ids)
        ORDER BY reference
        """;

    public async Task<IReadOnlyList<HcmEmployeeDirectoryEntry>> SearchEmployeesAsync(
        EmployeeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                cancellationToken);
            await using var command = CreateCommand(connection, transaction, SearchEmployeesSql);
            AddParameter(command, "office_id", NpgsqlDbType.Text, query.OfficeId);

            var employees = new List<HcmEmployeeDirectoryEntry>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var employee = new HcmEmployeeDirectoryEntry(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetFieldValue<string[]>(2));

                    // The name filter runs in C# with the shared matcher; the
                    // office filter above runs in SQL (both decided for T2).
                    if (string.IsNullOrWhiteSpace(query.Name)
                        || NameMatching.Matches(employee.DisplayName, query.Name))
                    {
                        employees.Add(employee);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return employees;
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
    }

    public async Task<HcmPresenceSnapshot> GetPresenceAsync(
        PresenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                cancellationToken);
            await using var command = CreateCommand(connection, transaction, PresenceSql);
            AddParameter(command, "presence_date", NpgsqlDbType.Date, query.Date);

            var entries = new List<HcmEmployeePresence>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var employee = new HcmEmployeeReference(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetFieldValue<string[]>(2));

                    // The state is source-owned data: passed through verbatim
                    // (SPEC-011 §3: Talent Assistant never derives it).
                    entries.Add(new HcmEmployeePresence(employee, reader.GetString(3)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new HcmPresenceSnapshot(query.Date, entries);
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
    }

    public async Task<IReadOnlyList<HcmTimeOffRequest>> GetTimeOffAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                cancellationToken);

            var employeeIds = await ResolveEmployeeIdsAsync(
                connection,
                transaction,
                query,
                cancellationToken);
            var english = IsEnglish(query.Language);

            await using var command = CreateCommand(connection, transaction, TimeOffSql);
            AddParameter(
                command,
                "employee_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                employeeIds);

            var requests = new List<HcmTimeOffRequest>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    requests.Add(new HcmTimeOffRequest(
                        Reference: reader.GetString(0),
                        EmployeeId: reader.GetString(1),
                        Type: Pick(reader, 2, 3, english),
                        Status: Pick(reader, 4, 5, english),
                        StartsOn: reader.GetFieldValue<DateOnly>(6),
                        EndsBefore: reader.IsDBNull(7)
                            ? null
                            : reader.GetFieldValue<DateOnly>(7),
                        Duration: reader.IsDBNull(8) ? null : reader.GetString(8),
                        DurationUnit: PickNullable(reader, 9, 10, english),
                        Summary: PickNullable(reader, 11, 12, english)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return requests;
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
    }

    public async Task<IReadOnlyList<HcmTask>> GetTasksAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await BeginReadOnlyTransactionAsync(
                connection,
                cancellationToken);

            var employeeIds = await ResolveEmployeeIdsAsync(
                connection,
                transaction,
                query,
                cancellationToken);
            var english = IsEnglish(query.Language);

            await using var command = CreateCommand(connection, transaction, TasksSql);
            AddParameter(
                command,
                "employee_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                employeeIds);

            var tasks = new List<HcmTask>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    tasks.Add(new HcmTask(
                        Reference: reader.GetString(0),
                        EmployeeId: reader.GetString(1),
                        Title: Pick(reader, 2, 3, english),
                        Status: Pick(reader, 4, 5, english),
                        DueOn: reader.IsDBNull(6)
                            ? null
                            : reader.GetFieldValue<DateOnly>(6),
                        Summary: PickNullable(reader, 7, 8, english)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return tasks;
        }
        catch (NpgsqlException exception)
        {
            throw new HcmSourceUnavailableException(UnavailableMessage, exception);
        }
    }

    /// <summary>
    /// Resolves the employee ids a data query applies to, inside the same read
    /// only transaction: an exact id wins, a name folds over the employees view
    /// and a name matching several employees returns the UNION of their ids
    /// (never a silent pick); without filters the whole collection is served.
    /// </summary>
    private async Task<string[]?> ResolveEmployeeIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EmployeeDataQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.EmployeeId))
        {
            // Exact match, as decided for T2 (SPEC-011): no fuzzy id resolution.
            return [query.EmployeeId];
        }

        if (string.IsNullOrWhiteSpace(query.Name))
        {
            // No filter: the whole collection (no silent selection).
            return null;
        }

        var employeeIds = new List<string>();
        await using var command = CreateCommand(connection, transaction, DirectorySql);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (NameMatching.Matches(reader.GetString(1), query.Name))
                {
                    employeeIds.Add(reader.GetString(0));
                }
            }
        }

        return employeeIds.ToArray();
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
            await using var command = CreateCommand(
                connection,
                transaction,
                ReadOnlyTransactionSql);
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
