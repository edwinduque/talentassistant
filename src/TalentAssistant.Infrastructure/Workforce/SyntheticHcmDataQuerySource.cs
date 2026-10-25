using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;

namespace TalentAssistant.Infrastructure.Workforce;

public sealed class SyntheticHcmDataQuerySource(
    IHcmWorkforceSource workforceSource,
    IHcmAttendanceSource attendanceSource) : IHcmDataQuerySource
{
    public async Task<HcmDataQueryResult> ExecuteAsync(
        HcmValidatedDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var rows = query.DataSet.Name switch
        {
            "employees" => await GetEmployeesAsync(query.Language, cancellationToken),
            "presence" => await GetPresenceAsync(query.Language, cancellationToken),
            "timeoff" => await GetTimeOffAsync(query.Language, cancellationToken),
            "tasks" => await GetTasksAsync(query.Language, cancellationToken),
            "attendance_insights" => await GetAttendanceInsightsAsync(
                query.Language,
                cancellationToken),
            _ => throw new InvalidOperationException("The validated HCM data set is not registered.")
        };

        return HcmDataQueryProcessor.Execute(rows, query);
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetEmployeesAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var employees = await workforceSource.SearchEmployeesAsync(
            new EmployeeSearchQuery(Language: language),
            cancellationToken);

        return employees.Select(employee => Row(
            ("employee_id", employee.Id),
            ("display_name", employee.DisplayName),
            ("office_ids", employee.OfficeIds),
            ("birthday_month", employee.BirthdayMonth),
            ("birthday_day", employee.BirthdayDay),
            ("job_title", employee.JobTitle),
            ("last_name", employee.LastName),
            ("hire_date", employee.HireDate),
            ("age_on_next_birthday", employee.AgeOnNextBirthday))).ToArray();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetPresenceAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var offset = 0; offset <= 2; offset++)
        {
            var snapshot = await workforceSource.GetPresenceAsync(
                new PresenceQuery(today.AddDays(offset), language),
                cancellationToken);
            rows.AddRange(snapshot.Entries.Select(entry => Row(
                ("presence_date", snapshot.Date),
                ("employee_id", entry.Employee.Id),
                ("employee_display_name", entry.Employee.DisplayName),
                ("employee_office_ids", entry.Employee.OfficeIds),
                ("state", entry.State))));
        }

        return rows;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetTimeOffAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var requests = await workforceSource.GetTimeOffAsync(
            new EmployeeDataQuery(Language: language),
            cancellationToken);
        return requests.Select(request => Row(
            ("reference", request.Reference),
            ("employee_id", request.EmployeeId),
            ("type", request.Type),
            ("status", request.Status),
            ("starts_on", request.StartsOn),
            ("ends_before", request.EndsBefore),
            ("duration", request.Duration),
            ("duration_unit", request.DurationUnit),
            ("summary", request.Summary))).ToArray();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetTasksAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var tasks = await workforceSource.GetTasksAsync(
            new EmployeeDataQuery(Language: language),
            cancellationToken);
        return tasks.Select(task => Row(
            ("reference", task.Reference),
            ("employee_id", task.EmployeeId),
            ("title", task.Title),
            ("status", task.Status),
            ("due_on", task.DueOn),
            ("summary", task.Summary))).ToArray();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetAttendanceInsightsAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var insights = await attendanceSource.GetAttendanceInsightsAsync(
            new AttendanceInsightQuery(
                DateOnly.MinValue,
                DateOnly.MaxValue,
                Language: language),
            cancellationToken);

        return insights.Select(insight =>
        {
            var related = insight.RelatedTimeOff;
            return Row(
                ("incident_id", insight.IncidentId),
                ("employee_id", insight.Employee.Id),
                ("employee_display_name", insight.Employee.DisplayName),
                ("employee_office_ids", insight.Employee.OfficeIds),
                ("office_id", insight.Office.Id),
                ("office_country", insight.Office.Country),
                ("office_city", insight.Office.City),
                ("office_iana_time_zone", insight.Office.IanaTimeZoneId),
                ("incident_at_utc", insight.IncidentAtUtc),
                ("work_date", insight.Schedule.WorkDate),
                ("schedule_starts_at", insight.Schedule.StartsAt),
                ("schedule_ends_at", insight.Schedule.EndsAt),
                ("punches", insight.Punches.Select(punch => Row(
                    ("occurred_at_utc", punch.OccurredAtUtc),
                    ("description", punch.Description))).ToArray()),
                ("rule_reference", insight.RuleFinding.RuleReference),
                ("rule_requires_review", insight.RuleFinding.RequiresReview),
                ("rule_summary", insight.RuleFinding.Summary),
                ("rule_evaluated_at_utc", insight.RuleFinding.EvaluatedAtUtc),
                ("rule_evidence", insight.RuleFinding.Evidence),
                ("rule_review_items", insight.RuleFinding.ReviewItems),
                ("related_timeoff_reference", related?.Reference),
                ("related_starts_on", related?.StartsOn),
                ("related_ends_before", related?.EndsBefore),
                ("related_summary", related?.Summary),
                ("related_type", related?.Type),
                ("related_status", related?.Status),
                ("related_duration", related?.Duration),
                ("related_duration_unit", related?.DurationUnit));
        }).ToArray();
    }

    private static IReadOnlyDictionary<string, object?> Row(
        params (string Name, object? Value)[] fields) =>
        fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);
}
