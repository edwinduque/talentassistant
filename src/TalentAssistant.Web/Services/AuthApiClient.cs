using System.Net;
using System.Net.Http.Json;

namespace TalentAssistant.Web.Services;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserName,
    string FullName,
    string PreferredLanguage);

/// <summary>User profile as returned by the API (<c>GET auth/me</c>, <c>POST auth/profile</c>).</summary>
public sealed record UserProfileResult(
    string UserName,
    string FullName,
    string PreferredLanguage);

/// <summary>Outcome of <c>POST auth/change-password</c>.</summary>
public enum ChangePasswordOutcome
{
    Success,
    WrongCurrentPassword,
    Rejected,
    Failed,
}

/// <summary>
/// Typed client for the backend API authentication contract
/// (<c>POST auth/login</c>, <c>POST auth/logout</c>, <c>GET auth/me</c>,
/// <c>POST auth/profile</c>, <c>POST auth/change-password</c>).
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
                payload.UserName,
                payload.FullName ?? string.Empty,
                payload.PreferredLanguage ?? SessionCulture.Default);
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

    /// <summary>
    /// Reads the profile of the user identified by <paramref name="accessToken"/>
    /// (<c>GET auth/me</c>).
    /// </summary>
    public async Task<UserProfileResult?> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The authenticated-user endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ProfileResponse>(
                cancellationToken: cancellationToken);
            return payload is null
                ? null
                : new UserProfileResult(
                    payload.UserName,
                    payload.FullName,
                    payload.PreferredLanguage);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the authenticated-user endpoint.");
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The authenticated-user request timed out.");
            return null;
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The authenticated-user response was not valid JSON.");
            return null;
        }
    }

    /// <summary>
    /// Saves the profile of the user identified by <paramref name="accessToken"/>
    /// (<c>POST auth/profile</c>). Returns <c>null</c> when the API rejects the
    /// request or cannot be reached.
    /// </summary>
    public async Task<UserProfileResult?> UpdateProfileAsync(
        string accessToken,
        string fullName,
        string preferredLanguage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/profile")
            {
                Content = JsonContent.Create(new ProfileRequest(fullName, preferredLanguage)),
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The profile endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ProfileResponse>(
                cancellationToken: cancellationToken);
            return payload is null
                ? null
                : new UserProfileResult(
                    payload.UserName,
                    payload.FullName,
                    payload.PreferredLanguage);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the profile endpoint.");
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The profile request timed out.");
            return null;
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The profile response was not valid JSON.");
            return null;
        }
    }

    /// <summary>
    /// Changes the password of the user identified by <paramref name="accessToken"/>
    /// (<c>POST auth/change-password</c>). The API requires the current password and
    /// applies the Identity password policy.
    /// </summary>
    public async Task<ChangePasswordOutcome> ChangePasswordAsync(
        string accessToken,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/change-password")
            {
                Content = JsonContent.Create(new ChangePasswordRequest(currentPassword, newPassword)),
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ChangePasswordOutcome.Success;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
                    cancellationToken: cancellationToken);

                // The API surfaces the Identity error descriptions in the problem
                // detail. Identity's default message for a wrong current password is
                // "Incorrect password."; every other 400 is a policy rejection.
                return problem?.Detail?.Contains(
                        "Incorrect password",
                        StringComparison.OrdinalIgnoreCase) == true
                    ? ChangePasswordOutcome.WrongCurrentPassword
                    : ChangePasswordOutcome.Rejected;
            }

            logger.LogWarning(
                "The change-password endpoint returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            return ChangePasswordOutcome.Failed;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the change-password endpoint.");
            return ChangePasswordOutcome.Failed;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The change-password request timed out.");
            return ChangePasswordOutcome.Failed;
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The change-password response was not valid JSON.");
            return ChangePasswordOutcome.Failed;
        }
    }

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginResponse(
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        string UserName,
        string FullName,
        string PreferredLanguage);

    private sealed record ProfileRequest(string FullName, string PreferredLanguage);

    private sealed record ProfileResponse(string UserName, string FullName, string PreferredLanguage);

    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record ProblemPayload(string? Title, string? Detail);
}
