using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Infrastructure.Authentication;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-038 degradation proof over HTTP: with <c>Hcm:Source=Postgres</c> and the
/// database unreachable (closed loopback port), every data endpoint answers
/// the machine-readable 503 reason "SourceUnavailable" instead of synthetic
/// data - the API must never fall back silently to the synthetic source
/// (SPEC-011 S3).
/// </summary>
public sealed class HcmPostgresDegradationHttpTests
    : IClassFixture<DegradedPostgresModeApiFactory>, IAsyncLifetime
{
    private readonly DegradedPostgresModeApiFactory _factory;
    private HttpClient _client = null!;
    private string _token = null!;

    public HcmPostgresDegradationHttpTests(DegradedPostgresModeApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // SPEC-006 seed step, reused as-is and idempotent, so repeating it
        // before every test class is safe.
        using var scope = _factory.Services.CreateScope();
        var seedInitializer = scope.ServiceProvider
            .GetRequiredService<InitializeLocalAuthenticationData>();
        await seedInitializer.InitializeAsync();

        _client = _factory.CreateClient();
        _token = await HttpTestAuth.LoginAsync(_client);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/employees")]
    [InlineData("/presence")]
    [InlineData("/timeoff")]
    [InlineData("/tasks")]
    [InlineData("/attendance/insights?from=2026-10-13&to=2026-10-17")]
    public async Task DataEndpointsAnswerSourceUnavailableWhenThePostgresSourceIsDown(string url)
    {
        using var request = HttpTestAuth.BearerRequest(HttpMethod.Get, url, _token);
        var response = await _client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"{url} returned {(int)response.StatusCode}: {body}");

        using var payload = JsonDocument.Parse(body);
        var root = payload.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        var reason = Assert.Single(root.EnumerateObject());
        Assert.Equal("reason", reason.Name);
        Assert.Equal("SourceUnavailable", reason.Value.GetString());
    }
}
