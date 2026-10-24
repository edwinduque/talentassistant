using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Infrastructure.Assistant;
using TalentAssistant.Infrastructure.Authentication;

namespace TalentAssistant.IntegrationTests.Http;

/// <summary>
/// HTTP contract tests for the session-protected endpoints, driven through the
/// real API pipeline with <see cref="WebApplicationFactory{TEntryPoint}"/>:
/// SPEC-005 authorization (401 without token), SPEC-006 login issuing the access
/// token the Bearer flow consumes, and the SPEC-009 degraded /chat contract
/// (400 for a whitespace message, 503 with reason "NotConfigured" when no
/// provider key is configured).
/// The SPEC-006 Set-Cookie session regression intentionally stays browser-E2E:
/// these tests pin the Bearer contract the cookie-based Web session builds on,
/// and replaying cookie semantics from a non-browser client would exercise the
/// test client instead of the browser behavior under regression.
/// The factory always supplies an empty <c>AI:ApiKey</c>, so the language model
/// fails with NotConfigured before any HTTP call: no test in this class can
/// reach the provider endpoint (SPEC-009 §8.6).
/// </summary>
public sealed class ChatHttpContractTests : IClassFixture<ChatContractApiFactory>, IAsyncLifetime
{
    private readonly ChatContractApiFactory _factory;

    public ChatHttpContractTests(ChatContractApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");

        // The host must resolve the tests database from that variable: a factory
        // override losing precedence would silently seed another database.
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(connectionString, configuration["ConnectionStrings:TalentAssistant"]);

        // SPEC-006 seed step, reused as-is and idempotent (it never overwrites an
        // existing password), so repeating it before every test is safe.
        using var scope = _factory.Services.CreateScope();
        var seedInitializer = scope.ServiceProvider
            .GetRequiredService<InitializeLocalAuthenticationData>();
        await seedInitializer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostChatWithoutTokenReturns401()
    {
        var client = _factory.CreateClient();

        using var request = BearerRequest(HttpMethod.Post, "/chat", token: null, new { message = "hola" });
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginReturnsTokenAndChatWithEmptyMessageReturns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);

        using var request = BearerRequest(HttpMethod.Post, "/chat", token, new { message = " " });
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Missing message",
            payload.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PostChatWithTokenAndNoProviderKeyReturnsNotConfigured()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);

        using var request = BearerRequest(HttpMethod.Post, "/chat", token, new { message = "hola" });
        var response = await client.SendAsync(request);

        // Exact contract: HTTP 503 with the single machine-readable reason.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        var reasonProperty = Assert.Single(root.EnumerateObject());
        Assert.Equal("reason", reasonProperty.Name);
        Assert.Equal("NotConfigured", reasonProperty.Value.GetString());
    }

    [Fact]
    public async Task GetEmployeesWithTokenReturns200AndDemoData()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);

        using var request = BearerRequest(HttpMethod.Get, "/employees", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() > 0, "The synthetic directory must not be empty.");

        var foundJuanPerez = false;
        foreach (var employee in root.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Object, employee.ValueKind);
            Assert.Equal(
                new[] { "displayName", "id", "officeIds" },
                employee.EnumerateObject()
                    .Select(property => property.Name)
                    .Order(StringComparer.Ordinal));
            Assert.True(employee.TryGetProperty("id", out _), "Each entry must expose 'id'.");
            Assert.True(employee.TryGetProperty("displayName", out var displayName), "Each entry must expose 'displayName'.");
            Assert.True(employee.TryGetProperty("officeIds", out var officeIds), "Each entry must expose 'officeIds'.");
            Assert.Equal(JsonValueKind.Array, officeIds.ValueKind);

            if (string.Equals(displayName.GetString(), "Juan Pérez", StringComparison.Ordinal))
            {
                foundJuanPerez = true;
            }
        }

        Assert.True(foundJuanPerez, "The demo fixture employee 'Juan Pérez' must be listed.");
    }

    [Fact]
    public async Task GetEmployeesWithoutTokenReturns401()
    {
        var client = _factory.CreateClient();

        using var request = BearerRequest(HttpMethod.Get, "/employees", token: null);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void FactoryConfigurationNeverProvidesAnAiKey()
    {
        // Documents the no-network invariant: an empty key makes the language
        // model fail with NotConfigured before it builds any request.
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.True(
            string.IsNullOrEmpty(configuration["AI:ApiKey"]),
            "The integration factory must keep AI:ApiKey empty so no test can reach the provider.");

        var providerOptions = _factory.Services.GetRequiredService<AssistantProviderOptions>();
        Assert.True(
            string.IsNullOrEmpty(providerOptions.ApiKey),
            "The registered provider options must carry an empty API key.");
    }

    private async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                userName = ChatContractApiFactory.SeedUserName,
                password = ChatContractApiFactory.SeedPassword
            });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"POST /auth/login returned {(int)response.StatusCode}: {body}");

        using var payload = JsonDocument.Parse(body);
        Assert.True(
            payload.RootElement.TryGetProperty("accessToken", out var token),
            "The login response must expose 'accessToken'.");
        var accessToken = token.GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        return accessToken!;
    }

    private static HttpRequestMessage BearerRequest(
        HttpMethod method,
        string url,
        string? token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}

/// <summary>
/// Hosts the real API pipeline for the HTTP contract tests with in-memory
/// overrides only. Every value is synthetic (signing key, seed credentials) or
/// reused from the process environment (the tests database connection string);
/// no secret is ever printed or written to disk, and the AI key is explicitly
/// empty so the provider path degrades to NotConfigured without any network call.
/// </summary>
public sealed class ChatContractApiFactory : WebApplicationFactory<Program>
{
    public const string SeedUserName = "integration-http@talentassistant.local";
    public const string SeedPassword = "Integration!Http123";

    // Synthetic values; never a real credential (NFR-004).
    private const string SigningKey = "integration-factory-signing-key-0123456789abcdef";
    private const string DefaultAiModel = "openai/gpt-oss-120b";
    private const string DefaultAiBaseUrl = "https://api.groq.com/openai/v1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The overrides are injected through both configuration entry points of
        // the deferred host: in-memory configuration sources and host settings.
        // For minimal-hosting entry points the two do not land at the same
        // precedence position relative to appsettings.json, so applying the same
        // values through both keeps the factory deterministic either way.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(Overrides());
        });

        foreach (var (key, value) in Overrides())
        {
            builder.UseSetting(key, value);
        }
    }

    private static Dictionary<string, string?> Overrides() =>
        new()
        {
            ["ConnectionStrings:TalentAssistant"] = TestsConnectionString(),
            ["Attendance:Source"] = "Dataset",
            ["Authentication:SigningKey"] = SigningKey,
            ["Authentication:SeedUserName"] = SeedUserName,
            ["Authentication:SeedPassword"] = SeedPassword,
            ["AI:ApiKey"] = string.Empty,
            ["AI:Model"] = DefaultAiModel,
            ["AI:BaseUrl"] = DefaultAiBaseUrl
        };

    private static string TestsConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");
        }

        return connectionString;
    }
}
