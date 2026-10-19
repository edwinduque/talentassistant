using Npgsql;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Hcm;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.HcmPostgres;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-038 degradation proof at the source level: every public operation maps
/// any Npgsql connection failure (this proof uses a closed loopback port only -
/// no external network) to the <see cref="HcmSourceUnavailableException"/>
/// marker with a fixed message that leaks neither the connection string nor any
/// person value. No retry is attempted: the first failure surfaces immediately.
/// </summary>
public sealed class PostgresSourceDegradationTests
{
    // Closed loopback port only: no external network (SPEC-011 S4). Npgsql
    // names the connect timeout "Timeout" (its 10.x builder rejects
    // "Connect Timeout").
    private const string UnreachableConnectionString = "Host=127.0.0.1;Port=59999;Timeout=2";

    private const string ExpectedMessage = "The HCM Postgres source is unavailable.";

    [Fact]
    public async Task EveryWorkforceOperationThrowsTheMarkerWhenTheSourceIsDown()
    {
        await using var dataSource = NpgsqlDataSource.Create(UnreachableConnectionString);
        var source = new PostgresWorkforceSource(dataSource, HcmPostgresAdapterOptions.Default);

        var search = await Assert.ThrowsAsync<HcmSourceUnavailableException>(
            () => source.SearchEmployeesAsync(new EmployeeSearchQuery()));
        Assert.Equal(ExpectedMessage, search.Message);
        Assert.IsType<NpgsqlException>(search.InnerException);

        await Assert.ThrowsAsync<HcmSourceUnavailableException>(
            () => source.GetPresenceAsync(new PresenceQuery(DateOnly.FromDateTime(DateTime.Today))));
        await Assert.ThrowsAsync<HcmSourceUnavailableException>(
            () => source.GetTimeOffAsync(new EmployeeDataQuery()));
        await Assert.ThrowsAsync<HcmSourceUnavailableException>(
            () => source.GetTasksAsync(new EmployeeDataQuery()));
    }

    [Fact]
    public async Task TheAttendanceOperationThrowsTheMarkerWhenTheSourceIsDown()
    {
        await using var dataSource = NpgsqlDataSource.Create(UnreachableConnectionString);
        var source = new PostgresAttendanceSource(dataSource, HcmPostgresAdapterOptions.Default);

        var exception = await Assert.ThrowsAsync<HcmSourceUnavailableException>(
            () => source.GetAttendanceInsightsAsync(
                new AttendanceInsightQuery(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 17))));

        Assert.Equal(ExpectedMessage, exception.Message);
        Assert.IsType<NpgsqlException>(exception.InnerException);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.Ordinal);
    }
}
