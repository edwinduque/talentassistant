using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Employee search by display name (case-insensitive contains) and/or office.
/// An empty query returns the whole directory; multiple name matches return the
/// list instead of choosing silently (SPEC-008 §3).
/// </summary>
public sealed record EmployeeSearchQuery(
    string? Name = null,
    string? OfficeId = null,
    string Language = "es");

/// <summary>Presence snapshot for a single local date in a supported UI language.</summary>
public sealed record PresenceQuery(DateOnly Date, string Language = "es");

/// <summary>Directory entry; display names are data from the source.</summary>
public sealed record HcmEmployeeDirectoryEntry(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OfficeIds);

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
