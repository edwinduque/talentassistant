using System.Net;
using System.Globalization;
using System.Net.Http.Json;

namespace TalentAssistant.Web.Services;

/// <summary>
/// Facts returned by <c>GET health</c>. User-facing copy lives in the
/// <c>Home</c> resource, keyed by this status, so the service stays
/// culture-neutral (SPEC-006 S3).
/// </summary>
public enum BackendHealthStatus
{
    Checking,
    Healthy,
    ApiErrorResponse,
    UnrecognizedResponse,
    Unreachable,
    Timeout,
    InvalidPayload,
}

public sealed record BackendHealthResult(BackendHealthStatus Status)
{
    public bool IsHealthy => Status == BackendHealthStatus.Healthy;
}

public sealed class BackendApiClient(
    HttpClient httpClient,
    ILogger<BackendApiClient> logger)
{
    public async Task<BackendHealthResult> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("health", cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The backend health endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);

                return new BackendHealthResult(BackendHealthStatus.ApiErrorResponse);
            }

            var payload = await response.Content.ReadFromJsonAsync<BackendHealthPayload>(
                cancellationToken: cancellationToken);
            if (!string.Equals(payload?.Status, "healthy", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("The backend health endpoint returned an unexpected response.");
                return new BackendHealthResult(BackendHealthStatus.UnrecognizedResponse);
            }

            return new BackendHealthResult(BackendHealthStatus.Healthy);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the backend health endpoint.");
            return new BackendHealthResult(BackendHealthStatus.Unreachable);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The backend health request timed out.");
            return new BackendHealthResult(BackendHealthStatus.Timeout);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The backend health response was not valid JSON.");
            return new BackendHealthResult(BackendHealthStatus.InvalidPayload);
        }
    }

    public async Task<WorkforceResult<EmployeeDirectoryItem>> GetEmployeesAsync(
        string? name,
        CancellationToken cancellationToken = default)
    {
        var path = "employees";
        if (!string.IsNullOrWhiteSpace(name))
        {
            path += $"?name={Uri.EscapeDataString(name)}";
        }

        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The employees endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                var failure = await IsSourceUnavailableAsync(response, cancellationToken)
                    ? WorkforceFailure.SourceUnavailable
                    : WorkforceFailure.ApiErrorResponse;
                return new WorkforceResult<EmployeeDirectoryItem>(failure, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<EmployeeDirectoryItem>>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("The employees endpoint returned an empty response.");
                return new WorkforceResult<EmployeeDirectoryItem>(
                    WorkforceFailure.InvalidPayload,
                    []);
            }

            return new WorkforceResult<EmployeeDirectoryItem>(WorkforceFailure.None, payload);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the employees endpoint.");
            return new WorkforceResult<EmployeeDirectoryItem>(
                WorkforceFailure.Unreachable,
                []);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The employees request timed out.");
            return new WorkforceResult<EmployeeDirectoryItem>(WorkforceFailure.Timeout, []);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The employees response was not valid JSON.");
            return new WorkforceResult<EmployeeDirectoryItem>(
                WorkforceFailure.InvalidPayload,
                []);
        }
    }

    public async Task<WorkforceResult<PresenceEntryItem>> GetPresenceAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = $"presence?date={Uri.EscapeDataString(value)}";

        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The presence endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);

                WorkforceFailure failure;
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    failure = WorkforceFailure.BadRequest;
                }
                else if (await IsSourceUnavailableAsync(response, cancellationToken))
                {
                    failure = WorkforceFailure.SourceUnavailable;
                }
                else
                {
                    failure = WorkforceFailure.ApiErrorResponse;
                }

                return new WorkforceResult<PresenceEntryItem>(failure, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<PresenceSnapshotResponse>(
                cancellationToken: cancellationToken);
            if (payload?.Entries is null)
            {
                logger.LogWarning("The presence endpoint returned an empty response.");
                return new WorkforceResult<PresenceEntryItem>(WorkforceFailure.InvalidPayload, []);
            }

            return new WorkforceResult<PresenceEntryItem>(
                WorkforceFailure.None,
                payload.Entries);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the presence endpoint.");
            return new WorkforceResult<PresenceEntryItem>(WorkforceFailure.Unreachable, []);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The presence request timed out.");
            return new WorkforceResult<PresenceEntryItem>(WorkforceFailure.Timeout, []);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The presence response was not valid JSON.");
            return new WorkforceResult<PresenceEntryItem>(WorkforceFailure.InvalidPayload, []);
        }
    }

    /// <summary>
    /// Sends one question plus the client-held history to <c>POST chat</c>
    /// (SPEC-009 S3). The bearer token rides on the same authenticated
    /// HttpClient as every other backend call.
    /// </summary>
    public async Task<AssistantChatResult> AskAssistantAsync(
        string message,
        IReadOnlyList<AssistantChatTurn> history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "chat",
                new ChatRequest(message, history),
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var failure = await MapChatFailureAsync(response, cancellationToken);
                logger.LogWarning(
                    "The chat endpoint returned HTTP {StatusCode} ({Failure}).",
                    (int)response.StatusCode,
                    failure);
                return new AssistantChatResult(failure, string.Empty, [], [], false);
            }

            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("The chat endpoint returned an empty response.");
                return new AssistantChatResult(
                    AssistantFailure.InvalidResponse,
                    string.Empty,
                    [],
                    [],
                    false);
            }

            return new AssistantChatResult(
                AssistantFailure.None,
                payload.Answer ?? string.Empty,
                payload.ToolsUsed ?? [],
                payload.Sources ?? [],
                payload.Completed);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the chat endpoint.");
            return new AssistantChatResult(AssistantFailure.Unreachable, string.Empty, [], [], false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The chat request timed out.");
            return new AssistantChatResult(AssistantFailure.Timeout, string.Empty, [], [], false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The chat response was not valid JSON.");
            return new AssistantChatResult(AssistantFailure.InvalidResponse, string.Empty, [], [], false);
        }
    }

    /// <summary>
    /// The API reports provider failures as 503/429 with a machine-readable
    /// {"reason": "..."} body; the Web layer owns the localized copy
    /// (SPEC-009 S1). A 400 is always a bad request; any other status without
    /// a known reason degrades to <see cref="AssistantFailure.Other"/>.
    /// </summary>
    private async Task<AssistantFailure> MapChatFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return AssistantFailure.BadRequest;
        }

        string? reason = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ChatErrorPayload>(
                cancellationToken: cancellationToken);
            reason = problem?.Reason;
        }
        catch (System.Text.Json.JsonException)
        {
            // The body is not the expected {reason} shape; fall back to the status code.
        }

        return reason switch
        {
            "NotConfigured" => AssistantFailure.NotConfigured,
            "RateLimited" => AssistantFailure.RateLimited,
            "Timeout" => AssistantFailure.Timeout,
            "Unreachable" => AssistantFailure.Unreachable,
            "InvalidResponse" => AssistantFailure.InvalidResponse,
            _ when response.StatusCode == HttpStatusCode.TooManyRequests =>
                AssistantFailure.RateLimited,
            _ => AssistantFailure.Other,
        };
    }

    /// <summary>
    /// The data endpoints report an unavailable HCM source as HTTP 503 with
    /// the single machine-readable reason "SourceUnavailable"; the Web layer
    /// owns the localized copy, mirroring the chat reason contract (SPEC-009
    /// S1, SPEC-011 S3). Any other body keeps its previous status mapping.
    /// </summary>
    private static async Task<bool> IsSourceUnavailableAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
        {
            return false;
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ChatErrorPayload>(
                cancellationToken: cancellationToken);
            return string.Equals(
                payload?.Reason,
                "SourceUnavailable",
                StringComparison.Ordinal);
        }
        catch (System.Text.Json.JsonException)
        {
            // Not the expected {reason} body: keep the previous mapping.
            return false;
        }
    }

    private sealed record BackendHealthPayload(string Status);

    private sealed record ChatRequest(
        string Message,
        IReadOnlyList<AssistantChatTurn> History);

    private sealed record ChatResponse(
        string Answer,
        List<string> ToolsUsed,
        List<string> Sources,
        bool Completed);

    private sealed record ChatErrorPayload(string? Reason);
}

/// <summary>Directory row as served by the API (culture-neutral data).</summary>
public sealed record EmployeeDirectoryItem(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OfficeIds);

public sealed record PresenceEmployeeItem(string Id, string DisplayName);

public sealed record PresenceEntryItem(PresenceEmployeeItem Employee, string State);

public sealed record PresenceSnapshotResponse(
    DateOnly Date,
    List<PresenceEntryItem> Entries);

public enum WorkforceFailure
{
    None,
    BadRequest,
    Unreachable,
    Timeout,
    InvalidPayload,
    ApiErrorResponse,

    /// <summary>
    /// The HCM source itself cannot be reached (SPEC-011 S3): the API answers
    /// 503 with reason "SourceUnavailable" and never substitutes synthetic
    /// data. User-facing copy lives in the page resources.
    /// </summary>
    SourceUnavailable,
}

public sealed record WorkforceResult<T>(WorkforceFailure Failure, IReadOnlyList<T> Items);

/// <summary>One conversation turn exchanged with <c>POST chat</c> (culture-neutral roles: user/assistant).</summary>
public sealed record AssistantChatTurn(string Role, string Content);

/// <summary>
/// Closed failure set for the chat endpoint. Values mirror the API's
/// machine-readable reasons plus transport failures; user-facing copy lives
/// in the Assistant resource, keyed by this enum (SPEC-009 S3).
/// </summary>
public enum AssistantFailure
{
    None,
    BadRequest,
    NotConfigured,
    RateLimited,
    Timeout,
    Unreachable,
    InvalidResponse,
    Other,
}

/// <summary>Chat answer with the ordered tool trace (<c>ChatResult</c> from the API).</summary>
public sealed record AssistantChatResult(
    AssistantFailure Failure,
    string Answer,
    IReadOnlyList<string> ToolsUsed,
    IReadOnlyList<string> Sources,
    bool Completed)
{
    public bool IsSuccess => Failure == AssistantFailure.None;
}
