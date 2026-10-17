using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;

namespace TalentAssistant.Infrastructure.Workforce;

/// <summary>
/// Synthetic implementation of the workforce port (SPEC-008). The five
/// employees that already have incidents are derived from the shared attendance
/// catalog so their display names stay identical in both languages; three demo
/// employees make the user's questions literal. Presence states are supplied
/// data of this synthetic HCM: never derived here from punches or calendars.
/// The snapshot covers local today and the next two days (fixtures anchored to
/// the machine's local date, as SPEC-008 assumes); other dates return an empty
/// snapshot, not an invented state.
/// </summary>
public sealed class SyntheticWorkforceSource : IHcmWorkforceSource
{
    public const string WorkingState = "working";
    public const string NotArrivedState = "not-arrived";
    public const string VacationState = "vacation";
    public const string AbsentState = "absent";

    private sealed record DemoEmployee(string Id, string DisplayName, string OfficeId);

    // Proper nouns are data and stay identical across languages (SPEC-007 §6).
    private static readonly IReadOnlyList<DemoEmployee> DemoEmployees =
        Array.AsReadOnly<DemoEmployee>(
        [
            new("employee-demo-juan", "Juan Pérez", "office-demo-madrid"),
            new("employee-demo-pedro", "Pedro Rojas", "office-demo-tokyo"),
            new("employee-demo-mauricio", "Mauricio Cardenas", "office-demo-madrid")
        ]);

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, string>>
        PresenceByDate = new Dictionary<DateOnly, IReadOnlyDictionary<string, string>>
        {
            [Today] = States(
                ("employee-demo-juan", WorkingState),
                ("employee-demo-pedro", WorkingState),
                ("employee-demo-madrid", WorkingState),
                ("employee-demo-tokyo-002", WorkingState),
                ("employee-demo-mauricio", NotArrivedState),
                ("employee-demo-madrid-004", NotArrivedState),
                ("employee-demo-tokyo", VacationState),
                ("employee-demo-madrid-002", VacationState)),
            [Today.AddDays(1)] = States(
                ("employee-demo-juan", WorkingState),
                ("employee-demo-mauricio", WorkingState),
                ("employee-demo-madrid", WorkingState),
                ("employee-demo-tokyo", WorkingState),
                ("employee-demo-pedro", NotArrivedState),
                ("employee-demo-tokyo-002", VacationState),
                ("employee-demo-madrid-002", AbsentState),
                ("employee-demo-madrid-004", AbsentState)),
            [Today.AddDays(2)] = States(
                ("employee-demo-pedro", WorkingState),
                ("employee-demo-mauricio", WorkingState),
                ("employee-demo-madrid-004", WorkingState),
                ("employee-demo-juan", NotArrivedState),
                ("employee-demo-madrid", VacationState),
                ("employee-demo-tokyo", AbsentState),
                ("employee-demo-tokyo-002", AbsentState),
                ("employee-demo-madrid-002", AbsentState))
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<HcmEmployeeDirectoryEntry>>
        DirectoryByLanguage = new Dictionary<string, IReadOnlyList<HcmEmployeeDirectoryEntry>>(
            StringComparer.Ordinal)
        {
            ["es"] = BuildDirectory(SyntheticHcmAttendanceSource.Spanish),
            ["en"] = BuildDirectory(SyntheticHcmAttendanceSource.English)
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, HcmEmployeeReference>>
        ReferencesByLanguage = new Dictionary<string, IReadOnlyDictionary<string, HcmEmployeeReference>>(
            StringComparer.Ordinal)
        {
            ["es"] = BuildReferences(SyntheticHcmAttendanceSource.Spanish),
            ["en"] = BuildReferences(SyntheticHcmAttendanceSource.English)
        };

    public Task<IReadOnlyList<HcmEmployeeDirectoryEntry>> SearchEmployeesAsync(
        EmployeeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<HcmEmployeeDirectoryEntry> employees = DirectoryFor(query.Language);

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            employees = employees.Where(employee =>
                employee.DisplayName.Contains(query.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.OfficeId))
        {
            employees = employees.Where(employee =>
                employee.OfficeIds.Any(id =>
                    string.Equals(id, query.OfficeId, StringComparison.OrdinalIgnoreCase)));
        }

        return Task.FromResult<IReadOnlyList<HcmEmployeeDirectoryEntry>>(employees.ToArray());
    }

    public Task<HcmPresenceSnapshot> GetPresenceAsync(
        PresenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (!PresenceByDate.TryGetValue(query.Date, out var states))
        {
            return Task.FromResult(new HcmPresenceSnapshot(
                query.Date,
                Array.AsReadOnly(Array.Empty<HcmEmployeePresence>())));
        }

        var references = ReferencesFor(query.Language);
        var entries = states
            .Select(state => new HcmEmployeePresence(references[state.Key], state.Value))
            .ToArray();

        return Task.FromResult(new HcmPresenceSnapshot(query.Date, Array.AsReadOnly(entries)));
    }

    private static IReadOnlyDictionary<string, string> States(
        params (string EmployeeId, string State)[] values) =>
        values.ToDictionary(value => value.EmployeeId, value => value.State, StringComparer.Ordinal);

    private static IReadOnlyList<HcmEmployeeDirectoryEntry> DirectoryFor(string language) =>
        DirectoryByLanguage.TryGetValue(language, out var directory)
            ? directory
            : DirectoryByLanguage[SyntheticHcmAttendanceSource.Spanish];

    private static IReadOnlyDictionary<string, HcmEmployeeReference> ReferencesFor(string language) =>
        ReferencesByLanguage.TryGetValue(language, out var references)
            ? references
            : ReferencesByLanguage[SyntheticHcmAttendanceSource.Spanish];

    private static IReadOnlyList<HcmEmployeeDirectoryEntry> BuildDirectory(string language)
    {
        var fromCatalog = SyntheticHcmAttendanceSource.FixturesFor(language)
            .Select(insight => new HcmEmployeeDirectoryEntry(
                insight.Employee.Id,
                insight.Employee.DisplayName,
                insight.Employee.OfficeIds))
            .DistinctBy(employee => employee.Id);

        var demoEmployees = DemoEmployees.Select(employee => new HcmEmployeeDirectoryEntry(
            employee.Id,
            employee.DisplayName,
            Array.AsReadOnly([employee.OfficeId])));

        return Array.AsReadOnly([.. fromCatalog, .. demoEmployees]);
    }

    private static IReadOnlyDictionary<string, HcmEmployeeReference> BuildReferences(string language)
    {
        var references = SyntheticHcmAttendanceSource.FixturesFor(language)
            .Select(insight => insight.Employee)
            .ToDictionary(employee => employee.Id, StringComparer.Ordinal);

        foreach (var demoEmployee in DemoEmployees)
        {
            references[demoEmployee.Id] = new HcmEmployeeReference(
                demoEmployee.Id,
                demoEmployee.DisplayName,
                Array.AsReadOnly([demoEmployee.OfficeId]));
        }

        return references;
    }
}
