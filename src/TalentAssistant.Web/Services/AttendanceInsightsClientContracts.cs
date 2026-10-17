namespace TalentAssistant.Web.Services;

/// <summary>
/// Failure kind reported by <c>GET attendance/insights</c>. User-facing copy lives
/// in the <c>AttendanceInsights</c> page resource, keyed by this value, so the
/// contract stays culture-neutral (SPEC-006 S3).
/// </summary>
public enum AttendanceInsightsFailure
{
    None,
    BadRequest,
    Unreachable,
    Timeout,
    InvalidPayload,
}

public sealed record AttendanceInsightsResult(
    AttendanceInsightsFailure Failure,
    IReadOnlyList<AttendanceInsightResponse> Insights)
{
    public bool IsSuccess => Failure == AttendanceInsightsFailure.None;
}

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
    string Summary,
    string? Type = null,
    string? Status = null,
    string? Duration = null,
    string? DurationUnit = null);
