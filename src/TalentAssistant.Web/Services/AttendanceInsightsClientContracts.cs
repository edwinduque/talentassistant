namespace TalentAssistant.Web.Services;

public sealed record AttendanceInsightsResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<AttendanceInsightResponse> Insights);

public sealed record AttendanceInsightResponse(
    string IncidentId,
    HcmEmployeeResponse Employee,
    HcmOfficeResponse Office,
    DateTimeOffset IncidentAtUtc,
    HcmWorkScheduleResponse Schedule,
    IReadOnlyList<HcmPunchResponse> Punches,
    HcmRuleFindingResponse RuleFinding,
    HcmTimeOffContextResponse? RelatedTimeOff);

public sealed record HcmEmployeeResponse(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OfficeIds);

public sealed record HcmOfficeResponse(
    string Id,
    string Country,
    string City,
    string IanaTimeZoneId);

public sealed record HcmWorkScheduleResponse(
    DateOnly WorkDate,
    TimeOnly StartsAt,
    TimeOnly EndsAt);

public sealed record HcmPunchResponse(
    DateTimeOffset OccurredAtUtc,
    string Description);

public sealed record HcmRuleFindingResponse(
    string RuleReference,
    bool RequiresReview,
    string Summary,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> ReviewItems);

public sealed record HcmTimeOffContextResponse(
    string Reference,
    DateOnly StartsOn,
    DateOnly EndsBefore,
    string Summary);
