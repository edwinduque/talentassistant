using System.Net;
using System.Net.Http.Json;

namespace TalentAssistant.Web.Services;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserName);

/// <summary>
/// Typed client for the backend API authentication contract
/// (<c>POST auth/login</c>, <c>POST auth/logout</c>).
/// </summary>
public sealed class AuthApiClient(
    HttpClient httpClient,
    ILogger<AuthApiClient> logger)
{
    public async Task<LoginResult?> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "auth/login",
                new LoginRequest(userName, password),
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The login endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(
                cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            {
                logger.LogWarning("The login endpoint returned an empty response.");
                return null;
            }

            return new LoginResult(
                payload.AccessToken,
                payload.ExpiresAtUtc,
                payload.UserName);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the login endpoint.");
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The login request timed out.");
            return null;
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The login response was not valid JSON.");
            return null;
        }
    }

    public async Task LogoutAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The logout endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the logout endpoint.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The logout request timed out.");
        }
    }

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginResponse(
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        string UserName);
}
