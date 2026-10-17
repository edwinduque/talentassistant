namespace TalentAssistant.Application.Workforce;

public sealed class SearchEmployees(IHcmWorkforceSource workforceSource)
{
    public async Task<IReadOnlyList<HcmEmployeeDirectoryEntry>> ExecuteAsync(
        EmployeeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await workforceSource.SearchEmployeesAsync(query, cancellationToken);
    }
}

public sealed class GetEmployeePresence(IHcmWorkforceSource workforceSource)
{
    public async Task<HcmPresenceSnapshot> ExecuteAsync(
        PresenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await workforceSource.GetPresenceAsync(query, cancellationToken);
    }
}

public sealed class GetEmployeeTimeOff(IHcmWorkforceSource workforceSource)
{
    public async Task<IReadOnlyList<HcmTimeOffRequest>> ExecuteAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await workforceSource.GetTimeOffAsync(query, cancellationToken);
    }
}

public sealed class GetEmployeeTasks(IHcmWorkforceSource workforceSource)
{
    public async Task<IReadOnlyList<HcmTask>> ExecuteAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await workforceSource.GetTasksAsync(query, cancellationToken);
    }
}
