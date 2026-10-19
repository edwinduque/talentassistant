using System.Globalization;
using Npgsql;
using TalentAssistant.Infrastructure.HcmPostgres;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// Shared fixture for the adapter tests: one <see cref="NpgsqlDataSource"/>
/// opened as the application role from the runner-provided connection string
/// (never printed), plus both adapters under test sharing that data source.
/// </summary>
public abstract class PostgresSourceTestBase : IAsyncLifetime
{
    private NpgsqlDataSource? _dataSource;

    protected PostgresWorkforceSource Workforce { get; private set; } = null!;

    protected PostgresAttendanceSource Attendance { get; private set; } = null!;

    public Task InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(HcmPostgresTestEnvironment.ConnectionString());
        Workforce = new PostgresWorkforceSource(_dataSource, HcmPostgresAdapterOptions.Default);
        Attendance = new PostgresAttendanceSource(_dataSource, HcmPostgresAdapterOptions.Default);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    /// <summary>
    /// The database's CURRENT_DATE: the day the local seed anchors presence to.
    /// The suite host and the database share the same clock, but not the same
    /// time zone (host UTC-5, database UTC), so the seeded "today" is the UTC day.
    /// </summary>
    protected async Task<DateOnly> ReadDatabaseCurrentDateAsync()
    {
        await using var connection = await _dataSource!.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_date";
        var today = await command.ExecuteScalarAsync();
        Assert.True(
            today is DateOnly,
            $"The database current date came back as {today?.GetType().Name ?? "null"}.");
        return (DateOnly)today!;
    }
}
