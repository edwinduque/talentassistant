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
    public async Task<AttendanceInsightsResult> GetAttendanceInsightsAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var from = fromInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = toExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = $"attendance/insights?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            path += $"&employeeId={Uri.EscapeDataString(employeeId)}";
        }

        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "The attendance insights endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);

                var failure = response.StatusCode == HttpStatusCode.BadRequest
                    ? AttendanceInsightsFailure.BadRequest
                    : AttendanceInsightsFailure.Unreachable;

                return new AttendanceInsightsResult(failure, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<AttendanceInsightResponse>>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("The attendance insights endpoint returned an empty response.");
                return new AttendanceInsightsResult(AttendanceInsightsFailure.InvalidPayload, []);
            }

            return new AttendanceInsightsResult(
                AttendanceInsightsFailure.None,
                payload);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the attendance insights endpoint.");
            return new AttendanceInsightsResult(AttendanceInsightsFailure.Unreachable, []);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The attendance insights request timed out.");
            return new AttendanceInsightsResult(AttendanceInsightsFailure.Timeout, []);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The attendance insights response was not valid JSON.");
            return new AttendanceInsightsResult(AttendanceInsightsFailure.InvalidPayload, []);
        }
    }

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
                return new WorkforceResult<EmployeeDirectoryItem>(
                    WorkforceFailure.ApiErrorResponse,
                    []);
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

                var failure = response.StatusCode == HttpStatusCode.BadRequest
                    ? WorkforceFailure.BadRequest
                    : WorkforceFailure.ApiErrorResponse;

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

    private sealed record BackendHealthPayload(string Status);
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
}

public sealed record WorkforceResult<T>(WorkforceFailure Failure, IReadOnlyList<T> Items);
