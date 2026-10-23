using System.Net;
using System.Net.Http.Json;

namespace TalentAssistant.Web.Services;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserName,
    string FullName,
    string PreferredLanguage,
    IReadOnlyList<string> Roles);

/// <summary>
/// SPEC-016: a user as served by the admin surface. Hashes and internal
/// data never leave the backend.
/// </summary>
public sealed record AdminUserResult(
    string Id,
    string UserName,
    string FullName,
    string PreferredLanguage,
    bool IsAdmin);

public enum AdminCreateUserOutcome
{
    Success,
    InvalidDetails,
    NotAuthorized,
    Failed,
}

public sealed record AdminCreateUserCommand(
    string UserName,
    string Password,
    string? FullName,
    string? PreferredLanguage,
    bool IsAdmin);

public sealed record AdminCreateUserResult(
    AdminCreateUserOutcome Outcome,
    AdminUserResult? User,
    string? RejectDetail);

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
                payload.PreferredLanguage ?? SessionCulture.Default,
                payload.Roles ?? Array.Empty<string>());
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

    /// <summary>
    /// Creates a user on the admin surface (SPEC-016). The <paramref name="accessToken"/>
    /// must belong to an administrator: the backend rejects anyone else.
    /// </summary>
    public async Task<AdminCreateUserResult> CreateUserAsync(
        string accessToken,
        AdminCreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "admin/users");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(command);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var payload = await response.Content.ReadFromJsonAsync<AdminUserPayload>(
                    cancellationToken: cancellationToken);
                return payload is null
                    ? new AdminCreateUserResult(AdminCreateUserOutcome.Failed, null, null)
                    : new AdminCreateUserResult(
                        AdminCreateUserOutcome.Success,
                        new AdminUserResult(
                            payload.Id,
                            payload.UserName,
                            payload.FullName ?? string.Empty,
                            payload.PreferredLanguage ?? "es",
                            payload.IsAdmin),
                        null);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new AdminCreateUserResult(AdminCreateUserOutcome.NotAuthorized, null, null);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
                    cancellationToken: cancellationToken);
                return new AdminCreateUserResult(
                    AdminCreateUserOutcome.InvalidDetails,
                    null,
                    problem?.Detail ?? problem?.Title);
            }

            logger.LogWarning(
                "The admin user creation endpoint returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            return new AdminCreateUserResult(AdminCreateUserOutcome.Failed, null, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the admin user creation endpoint.");
            return new AdminCreateUserResult(AdminCreateUserOutcome.Failed, null, null);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The admin user creation request timed out.");
            return new AdminCreateUserResult(AdminCreateUserOutcome.Failed, null, null);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The admin user creation response was not valid JSON.");
            return new AdminCreateUserResult(AdminCreateUserOutcome.Failed, null, null);
        }
    }

    /// <summary>
    /// Lists the users of the admin surface (SPEC-016). Null when the surface
    /// is unreachable or the caller is not authorized.
    /// </summary>
    public async Task<IReadOnlyList<AdminUserResult>?> ListUsersAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "admin/users");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The admin users endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<List<AdminUserPayload>>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                return null;
            }

            return payload
                .Select(ToAdminUserResult)
                .ToList()
                .AsReadOnly();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the admin users endpoint.");
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The admin users request timed out.");
            return null;
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The admin users response was not valid JSON.");
            return null;
        }
    }

    private static AdminUserResult ToAdminUserResult(AdminUserPayload payload) =>
        new(payload.Id, payload.UserName, payload.FullName ?? string.Empty, payload.PreferredLanguage ?? "es", payload.IsAdmin);

    private sealed record AdminUserPayload(
        string Id,
        string UserName,
        string? FullName,
        string? PreferredLanguage,
        bool IsAdmin);

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
        string PreferredLanguage,
        IReadOnlyList<string>? Roles = null);

    private sealed record ProfileRequest(string FullName, string PreferredLanguage);

    private sealed record ProfileResponse(string UserName, string FullName, string PreferredLanguage);

    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record ProblemPayload(string? Title, string? Detail);
}
