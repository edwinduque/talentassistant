using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.Persistence;
using TalentAssistant.IntegrationTests.Http;

namespace TalentAssistant.IntegrationTests.Authentication;

/// <summary>
/// SPEC-016: admin role on the seed user and the /admin/users contract,
/// enforced by the backend. Written RED-first: before the implementation the
/// endpoints do not exist (HTTP 404) and the seed user carries no role, so
/// every fact below fails until T2/T3 land.
/// </summary>
public sealed class AdminUserManagementTests : IClassFixture<ChatContractApiFactory>, IAsyncLifetime
{
    private const string NewUserPassword = "Demo!Guest123";

    private readonly ChatContractApiFactory _factory;

    public AdminUserManagementTests(ChatContractApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        // Same idempotent seed step as the chat contract tests: safe to repeat.
        using var scope = _factory.Services.CreateAsyncScope();
        var seedInitializer = scope.ServiceProvider
            .GetRequiredService<InitializeLocalAuthenticationData>();
        await seedInitializer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedUserCarriesTheAdminRole()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var seedUser = await userManager.FindByNameAsync(ChatContractApiFactory.SeedUserName);
        Assert.NotNull(seedUser);
        Assert.Contains("admin", await userManager.GetRolesAsync(seedUser));
    }

    [Fact]
    public async Task AdminCanListUsersThroughTheAdminEndpoint()
    {
        using var client = await LoginAsync(
            ChatContractApiFactory.SeedUserName,
            ChatContractApiFactory.SeedPassword);

        using var response = await client.GetAsync("admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<AdminUserSummary>>();
        Assert.NotNull(payload);
        Assert.Contains(payload, item => item.UserName == ChatContractApiFactory.SeedUserName);
    }

    [Fact]
    public async Task AdminCanCreateAUserThatCanImmediatelyLogin()
    {
        var newUserName = $"admin-demo-{Guid.NewGuid():N}@talentassistant.local";
        using var client = await LoginAsync(
            ChatContractApiFactory.SeedUserName,
            ChatContractApiFactory.SeedPassword);

        using var createResponse = await client.PostAsJsonAsync(
            "admin/users",
            new
            {
                userName = newUserName,
                password = NewUserPassword,
                fullName = "Demo Guest",
                preferredLanguage = "en",
                isAdmin = false
            });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        try
        {
            // The created admin-flag=false user can authenticate, but the
            // backend must not let it use the admin surface: 403 (SPEC-016).
            using var newClient = await LoginAsync(newUserName, NewUserPassword);
            using var forbidden = await newClient.GetAsync("admin/users");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        finally
        {
            // The test database stays minimal: the demo user does not survive.
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var created = await userManager.FindByNameAsync(newUserName);
            if (created is not null)
            {
                await userManager.DeleteAsync(created);
            }
        }
    }

    [Fact]
    public async Task RequestWithoutSessionIsUnauthorized()
    {
        using var anonymous = _factory.CreateClient();
        using var response = await anonymous.GetAsync("admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> LoginAsync(string userName, string password)
    {
        var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "auth/login",
            new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        var accessToken = payload?.AccessToken;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private sealed record LoginPayload(string AccessToken);
    private sealed record AdminUserSummary(
        string Id,
        string UserName,
        string FullName,
        string PreferredLanguage,
        bool IsAdmin);
}
