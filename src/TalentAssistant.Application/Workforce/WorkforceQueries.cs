using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Employee search by display name, surname fragment, and/or office.
/// An empty query returns the whole directory; multiple matches return the
/// list instead of choosing silently (SPEC-008 §3).
/// </summary>
public sealed record EmployeeSearchQuery(
    string? Name = null,
    string? OfficeId = null,
    string Language = "es",
    string? SurnameContains = null);

/// <summary>Find the next calendar birthday on or after the caller's local date.</summary>
public sealed record UpcomingBirthdayQuery(DateOnly AsOfDate, string Language = "es");

/// <summary>Presence snapshot for a single local date in a supported UI language.</summary>
public sealed record PresenceQuery(DateOnly Date, string Language = "es");

/// <summary>
/// Employee-scoped data query: exact id, or case-insensitive name contains.
/// With no filter the whole collection is returned; a name matching several
/// employees returns the items of all of them instead of choosing silently.
/// </summary>
public sealed record EmployeeDataQuery(
    string? EmployeeId = null,
    string? Name = null,
    string Language = "es");

/// <summary>TimeOff request exactly as declared by the source (SPEC-008 §6).</summary>
public sealed record HcmTimeOffRequest(
    string Reference,
    string EmployeeId,
    string Type,
    string Status,
    DateOnly StartsOn,
    DateOnly? EndsBefore,
    string? Duration,
    string? DurationUnit,
    string? Summary);

/// <summary>Task exactly as declared by the source; status is source data.</summary>
public sealed record HcmTask(
    string Reference,
    string EmployeeId,
    string Title,
    string Status,
    DateOnly? DueOn,
    string? Summary);

/// <summary>
/// Directory entry with only employee fields approved for assistant search.
/// <c>AgeOnNextBirthday</c> is the approved derived age (SPEC-008 §3): the age
/// the employee turns at their next birthday, computed by the source; null when
/// the source has no birthday. The raw birth year is never a field here.
/// </summary>
public sealed record HcmEmployeeDirectoryEntry(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OfficeIds,
    string? JobTitle = null,
    string? LastName = null,
    DateOnly? HireDate = null,
    int? AgeOnNextBirthday = null,
    string? Gender = null,
    string? ManagerEmployeeId = null,
    string? ManagerDisplayName = null,
    string? Email = null,
    [property: System.Text.Json.Serialization.JsonIgnore]
    int? BirthdayMonth = null,
    [property: System.Text.Json.Serialization.JsonIgnore]
    int? BirthdayDay = null);

/// <summary>
/// The next birthday occurrence supplied by the HCM source. The raw birth year
/// is intentionally never part of this assistant-facing result; the derived
/// age the employee turns at that birthday is the approved projection
/// (SPEC-008 §3, amendment 2026-10-24).
/// </summary>
public sealed record HcmUpcomingBirthday(
    string EmployeeId,
    string DisplayName,
    DateOnly NextBirthdayOn,
    int? AgeOnNextBirthday = null);

/// <summary>
/// Presence state supplied by the source (values such as working,
/// not-arrived, vacation, absent). Talent Assistant never derives them.
/// </summary>
public sealed record HcmEmployeePresence(
    HcmEmployeeReference Employee,
    string State);

public sealed record HcmPresenceSnapshot(
    DateOnly Date,
    IReadOnlyList<HcmEmployeePresence> Entries);
