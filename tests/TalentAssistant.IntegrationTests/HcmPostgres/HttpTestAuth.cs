using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TalentAssistant.IntegrationTests.Http;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// Shared Bearer helpers for the HCM Postgres mode HTTP tests. The seed
/// credentials are the shared synthetic ones of <see cref="ChatContractApiFactory"/>
/// (idempotent seeding, never a real credential - NFR-004).
/// </summary>
internal static class HttpTestAuth
{
    internal static async Task<string> LoginAsync(HttpClient client)
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

    internal static HttpRequestMessage BearerRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
