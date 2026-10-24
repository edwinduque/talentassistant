namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Query-side port for workforce capabilities (employee directory, birthdays,
/// and presence) that exist alongside the attendance port (SPEC-008).
/// </summary>
public interface IHcmWorkforceSource
{
    Task<IReadOnlyList<HcmEmployeeDirectoryEntry>> SearchEmployeesAsync(
        EmployeeSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HcmUpcomingBirthday>> GetUpcomingBirthdaysAsync(
        UpcomingBirthdayQuery query,
        CancellationToken cancellationToken = default);

    Task<HcmPresenceSnapshot> GetPresenceAsync(
        PresenceQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HcmTimeOffRequest>> GetTimeOffAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HcmTask>> GetTasksAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default);
}
