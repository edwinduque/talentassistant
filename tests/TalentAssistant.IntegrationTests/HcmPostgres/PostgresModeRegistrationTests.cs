using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.HcmPostgres;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// SPEC-011 S3 no-mixed-mode proof: with <c>Hcm:Source=Postgres</c> the DI
/// container exposes exactly one registration per HCM port and query source,
/// all backed by PostgreSQL. Synthetic and dataset adapters are not registered.
/// </summary>
public sealed class PostgresModeRegistrationTests : IClassFixture<DegradedPostgresModeApiFactory>
{
    private readonly DegradedPostgresModeApiFactory _factory;

    public PostgresModeRegistrationTests(DegradedPostgresModeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void PostgresModeRegistersOnlyTheTwoPostgresAdapters()
    {
        // Exactly one registration per port: a second (synthetic or dataset)
        // registration would make the mixed mode possible.
        var workforce = Assert.Single(_factory.Services.GetServices<IHcmWorkforceSource>());
        Assert.IsType<PostgresWorkforceSource>(workforce);

        var attendance = Assert.Single(_factory.Services.GetServices<IHcmAttendanceSource>());
        Assert.IsType<PostgresAttendanceSource>(attendance);

        var querySource = Assert.Single(_factory.Services.GetServices<IHcmDataQuerySource>());
        Assert.IsType<PostgresHcmDataQuerySource>(querySource);

        // The connection pool is registered as a singleton from the configured
        // connection string; building it does not open a connection, and the
        // resolve fails if the registration is missing.
        var dataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();
        Assert.NotNull(dataSource);
    }
}
