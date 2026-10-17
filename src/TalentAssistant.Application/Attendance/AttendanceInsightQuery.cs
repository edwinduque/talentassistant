namespace TalentAssistant.Application.Attendance;

public sealed record AttendanceInsightQuery(
    DateOnly FromInclusive,
    DateOnly ToExclusive,
    string? EmployeeId = null,
    string Language = "es");

public sealed record HcmOfficeReference(
    string Id,
    string Country,
    string City,
    string IanaTimeZoneId);

public sealed record HcmEmployeeReference(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OfficeIds);

public sealed record HcmWorkSchedule(
    DateOnly WorkDate,
    TimeOnly StartsAt,
    TimeOnly EndsAt);

public sealed record HcmPunch(
    DateTimeOffset OccurredAtUtc,
    string Description);

public sealed record HcmRuleFinding(
    string RuleReference,
    bool RequiresReview,
    string Summary,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> ReviewItems);

public sealed record HcmTimeOffContext(
    string Reference,
    DateOnly StartsOn,
    DateOnly EndsBefore,
    string Summary,
    string? Type = null,
    string? Status = null,
    string? Duration = null,
    string? DurationUnit = null);

public sealed record HcmAttendanceInsight(
    string IncidentId,
    HcmEmployeeReference Employee,
    HcmOfficeReference Office,
    DateTimeOffset IncidentAtUtc,
    HcmWorkSchedule Schedule,
    IReadOnlyList<HcmPunch> Punches,
    HcmRuleFinding RuleFinding,
    HcmTimeOffContext? RelatedTimeOff);
