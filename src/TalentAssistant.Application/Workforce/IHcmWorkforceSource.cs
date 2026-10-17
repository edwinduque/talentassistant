namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Query-side port for workforce capabilities (directory and presence) that
/// exist alongside the attendance port without changing it (SPEC-008).
/// </summary>
public interface IHcmWorkforceSource
{
    Task<IReadOnlyList<HcmEmployeeDirectoryEntry>> SearchEmployeesAsync(
        EmployeeSearchQuery query,
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
