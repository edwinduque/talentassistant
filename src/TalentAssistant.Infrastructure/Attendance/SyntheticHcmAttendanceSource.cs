using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Infrastructure.Attendance;

public sealed class SyntheticHcmAttendanceSource : IHcmAttendanceSource
{
    private static readonly IReadOnlyList<HcmAttendanceInsight> Fixtures = Array.AsReadOnly(
        new HcmAttendanceInsight[]
        {
        new HcmAttendanceInsight(
            "incident-demo-madrid-start",
            new HcmEmployeeReference(
                "employee-demo-madrid",
                "Synthetic employee 001",
                ReadOnlyList("office-demo-madrid")),
            new HcmOfficeReference(
                "office-demo-madrid",
                "Spain",
                "Madrid",
                "Europe/Madrid"),
            new DateTimeOffset(2026, 10, 14, 22, 0, 0, TimeSpan.Zero),
            new HcmWorkSchedule(
                new DateOnly(2026, 10, 15),
                new TimeOnly(9, 0),
                new TimeOnly(17, 0)),
            ReadOnlyList(
                new HcmPunch(
                    new DateTimeOffset(2026, 10, 15, 7, 2, 0, TimeSpan.Zero),
                    "Synthetic HCM punch event"),
                new HcmPunch(
                    new DateTimeOffset(2026, 10, 15, 11, 30, 0, TimeSpan.Zero),
                    "Synthetic HCM punch event")),
            new HcmRuleFinding(
                "HCM-SYN-RULE-001",
                true,
                "The synthetic HCM reports an attendance finding for review.",
                new DateTimeOffset(2026, 10, 15, 7, 5, 0, TimeSpan.Zero),
                ReadOnlyList(
                    "The HCM supplied the scheduled shift and its punch events.",
                    "The HCM marked this attendance context for review."),
                ReadOnlyList("Review the originating HCM record.")),
            new HcmTimeOffContext(
                "timeoff-demo-madrid",
                new DateOnly(2026, 10, 15),
                new DateOnly(2026, 10, 16),
                "The synthetic HCM related this TimeOff context to the attendance record.")),
        new HcmAttendanceInsight(
            "incident-demo-tokyo-start",
            new HcmEmployeeReference(
                "employee-demo-tokyo",
                "Synthetic employee 002",
                ReadOnlyList("office-demo-tokyo")),
            new HcmOfficeReference(
                "office-demo-tokyo",
                "Japan",
                "Tokyo",
                "Asia/Tokyo"),
            new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero),
            new HcmWorkSchedule(
                new DateOnly(2026, 10, 15),
                new TimeOnly(9, 0),
                new TimeOnly(18, 0)),
            ReadOnlyList(
                new HcmPunch(
                    new DateTimeOffset(2026, 10, 15, 0, 10, 0, TimeSpan.Zero),
                    "Synthetic HCM punch event"),
                new HcmPunch(
                    new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero),
                    "Synthetic HCM punch event")),
            new HcmRuleFinding(
                "HCM-SYN-RULE-002",
                false,
                "The synthetic HCM reports no additional review for this attendance context.",
                new DateTimeOffset(2026, 10, 15, 0, 15, 0, TimeSpan.Zero),
                ReadOnlyList("The HCM supplied the scheduled shift and its punch events."),
                ReadOnlyList<string>()),
            null),
        new HcmAttendanceInsight(
            "incident-demo-madrid-next-day",
            new HcmEmployeeReference(
                "employee-demo-madrid-002",
                "Synthetic employee 003",
                ReadOnlyList("office-demo-madrid")),
            new HcmOfficeReference(
                "office-demo-madrid",
                "Spain",
                "Madrid",
                "Europe/Madrid"),
            new DateTimeOffset(2026, 10, 15, 22, 0, 0, TimeSpan.Zero),
            new HcmWorkSchedule(
                new DateOnly(2026, 10, 16),
                new TimeOnly(9, 0),
                new TimeOnly(17, 0)),
            ReadOnlyList(
                new HcmPunch(
                    new DateTimeOffset(2026, 10, 16, 7, 5, 0, TimeSpan.Zero),
                    "Synthetic HCM punch event")),
            new HcmRuleFinding(
                "HCM-SYN-RULE-003",
                true,
                "The synthetic HCM reports an attendance finding for review.",
                new DateTimeOffset(2026, 10, 15, 22, 5, 0, TimeSpan.Zero),
                ReadOnlyList("The HCM supplied the scheduled shift and its punch events."),
                ReadOnlyList("Review the originating HCM record.")),
            null)
        });

    public Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (query.FromInclusive >= query.ToExclusive)
        {
            throw new ArgumentException(
                "The start date must be earlier than the exclusive end date.",
                nameof(query));
        }

        var matches = Fixtures
            .Where(insight => string.IsNullOrWhiteSpace(query.EmployeeId)
                || string.Equals(
                    insight.Employee.Id,
                    query.EmployeeId,
                    StringComparison.OrdinalIgnoreCase))
            .Where(insight => IsInOfficeDateRange(insight, query))
            .ToArray();

        return Task.FromResult<IReadOnlyList<HcmAttendanceInsight>>(matches);
    }

    private static bool IsInOfficeDateRange(
        HcmAttendanceInsight insight,
        AttendanceInsightQuery query)
    {
        var officeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            insight.Office.IanaTimeZoneId);
        var officeLocalDateTime = TimeZoneInfo.ConvertTime(
            insight.IncidentAtUtc,
            officeTimeZone);
        var officeLocalDate = DateOnly.FromDateTime(officeLocalDateTime.DateTime);

        return officeLocalDate >= query.FromInclusive
            && officeLocalDate < query.ToExclusive;
    }

    private static IReadOnlyList<T> ReadOnlyList<T>(params T[] values) =>
        Array.AsReadOnly(values);
}
