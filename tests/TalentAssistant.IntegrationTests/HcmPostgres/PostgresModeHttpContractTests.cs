using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Infrastructure.Authentication;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-036 over HTTP: with <c>Hcm:Source=Postgres</c> the five data endpoints of
/// the assistant answer from the seeded contract views of the local PostgreSQL
/// (same response formats as the synthetic mode), proving the switch registers
/// the Postgres adapters end to end instead of the synthetic/MySQL ones.
/// The seeded presence rows key on the database's <c>CURRENT_DATE</c> - its UTC
/// day - which the test host shares, so the presence query uses the UTC day.
/// </summary>
public sealed class PostgresModeHttpContractTests
    : IClassFixture<PostgresModeApiFactory>, IAsyncLifetime
{
    private readonly PostgresModeApiFactory _factory;
    private HttpClient _client = null!;
    private string _token = null!;

    public PostgresModeHttpContractTests(PostgresModeApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // SPEC-006 seed step, reused as-is and idempotent (it never overwrites
        // an existing password), so repeating it before every test class is safe.
        using var scope = _factory.Services.CreateScope();
        var seedInitializer = scope.ServiceProvider
            .GetRequiredService<InitializeLocalAuthenticationData>();
        await seedInitializer.InitializeAsync();

        _client = _factory.CreateClient();
        _token = await HttpTestAuth.LoginAsync(_client);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TheFiveDataEndpointsServeTheSeededContractInPostgresMode()
    {
        using (var employees = await GetAsync("/employees"))
        {
            var root = employees.RootElement;
            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            Assert.Equal(9, root.GetArrayLength());

            var names = root.EnumerateArray()
                .Select(employee => employee.GetProperty("displayName").GetString())
                .ToArray();
            Assert.Contains("Juan Pérez", names);
        }

        // The local seed keys presence to CURRENT_DATE (the database's UTC day);
        // the test host shares the database clock, so the UTC day is the seeded
        // "today" regardless of the host's local time zone.
        var presenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var presenceUrl = "/presence?date="
            + presenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using (var presence = await GetAsync(presenceUrl))
        {
            var root = presence.RootElement;
            Assert.Equal(
                presenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                root.GetProperty("date").GetString());

            var entries = root.GetProperty("entries");
            Assert.Equal(9, entries.GetArrayLength());
            var pedro = entries.EnumerateArray().Single(entry =>
                entry.GetProperty("employee").GetProperty("id").GetString()
                    == "employee-demo-pedro");
            Assert.Equal("vacation", pedro.GetProperty("state").GetString());
        }

        using (var timeOff = await GetAsync("/timeoff"))
        {
            Assert.Equal(3, timeOff.RootElement.GetArrayLength());
        }

        using (var tasks = await GetAsync("/tasks"))
        {
            Assert.Equal(4, tasks.RootElement.GetArrayLength());
        }

        using (var insights = await GetAsync(
            "/attendance/insights?from=2026-10-13&to=2026-10-17"))
        {
            var root = insights.RootElement;
            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            var incident = Assert.Single(root.EnumerateArray());
            Assert.Equal(
                "incident-2026-10-13-juan-late",
                incident.GetProperty("incidentId").GetString());
            Assert.Equal(
                "employee-demo-juan",
                incident.GetProperty("employee").GetProperty("id").GetString());
        }
    }

    private async Task<JsonDocument> GetAsync(string url)
    {
        using var request = HttpTestAuth.BearerRequest(HttpMethod.Get, url, _token);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{url} returned {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }
}
