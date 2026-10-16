using System.Globalization;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure;
using TalentAssistant.Application.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TalentAssistant");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:TalentAssistant with User Secrets or an environment variable.");
}

var attendanceSource = builder.Configuration["Attendance:Source"] ?? "Memory";
var useMySqlAttendanceSource = string.Equals(
    attendanceSource,
    "MySql",
    StringComparison.OrdinalIgnoreCase);
if (!useMySqlAttendanceSource
    && !string.Equals(attendanceSource, "Memory", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Attendance:Source must be configured as Memory or MySql.");
}

builder.Services.AddInfrastructure(connectionString, useMySqlAttendanceSource);
builder.Services.AddScoped<GetAttendanceInsights>();

var app = builder.Build();

if (args.Contains("--initialize-local-data", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider
        .GetRequiredService<InitializeLocalSyntheticAttendanceData>();
    await initializer.InitializeAsync();
    return;
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("healthy")))
    .WithName("Health");

app.MapGet("/health/database", async (
    IDatabaseAvailabilityCheck databaseCheck,
    CancellationToken cancellationToken) =>
{
    if (await databaseCheck.IsAvailableAsync(cancellationToken))
    {
        return Results.Ok(new HealthResponse("healthy"));
    }

    return Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Database unavailable",
        detail: "The database could not be reached.");
}).WithName("DatabaseHealth");

app.MapGet("/attendance/insights", async (
    string? from,
    string? to,
    string? employeeId,
    GetAttendanceInsights getAttendanceInsights,
    CancellationToken cancellationToken) =>
{
    if (!DateOnly.TryParseExact(
            from,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fromDate)
        || !DateOnly.TryParseExact(
            to,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var toDate)
        || fromDate >= toDate)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid date range",
            detail: "Provide from and to dates as yyyy-MM-dd, with from earlier than to.");
    }

    try
    {
        var query = new AttendanceInsightQuery(fromDate, toDate, employeeId);
        var insights = await getAttendanceInsights.ExecuteAsync(query, cancellationToken);
        return Results.Ok(insights);
    }
    catch (TimeZoneNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
    catch (InvalidTimeZoneException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
    catch (AttendanceSourceUnavailableException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
}).WithName("AttendanceInsights");

app.Run();

internal sealed record HealthResponse(string Status);
