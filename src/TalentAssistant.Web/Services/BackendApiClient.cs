using System.Net;
using System.Globalization;
using System.Net.Http.Json;

namespace TalentAssistant.Web.Services;

public sealed record BackendHealthResult(bool IsHealthy, string Message);

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

                var message = response.StatusCode == HttpStatusCode.BadRequest
                    ? "Check the selected date range and employee."
                    : "The attendance source could not be reached.";

                return new AttendanceInsightsResult(false, message, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<AttendanceInsightResponse>>(
                cancellationToken: cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("The attendance insights endpoint returned an empty response.");
                return new AttendanceInsightsResult(
                    false,
                    "The API response could not be read.",
                    []);
            }

            return new AttendanceInsightsResult(
                true,
                payload.Count == 0
                    ? "No attendance findings matched the selected filters."
                    : $"{payload.Count} attendance finding(s) returned.",
                payload);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the attendance insights endpoint.");
            return new AttendanceInsightsResult(
                false,
                "The attendance source could not be reached.",
                []);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The attendance insights request timed out.");
            return new AttendanceInsightsResult(
                false,
                "The attendance request did not respond in time.",
                []);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The attendance insights response was not valid JSON.");
            return new AttendanceInsightsResult(
                false,
                "The API response could not be read.",
                []);
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

                return new BackendHealthResult(false, "The API returned an error response.");
            }

            var payload = await response.Content.ReadFromJsonAsync<BackendHealthPayload>(
                cancellationToken: cancellationToken);
            if (!string.Equals(payload?.Status, "healthy", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("The backend health endpoint returned an unexpected response.");
                return new BackendHealthResult(false, "The API response was not recognized.");
            }

            return new BackendHealthResult(true, "The API is responding.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the backend health endpoint.");
            return new BackendHealthResult(
                false,
                "The API could not be reached. Check that it is running.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The backend health request timed out.");
            return new BackendHealthResult(false, "The API did not respond in time.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning(exception, "The backend health response was not valid JSON.");
            return new BackendHealthResult(false, "The API response could not be read.");
        }
    }

    private sealed record BackendHealthPayload(string Status);
}
