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

    // Presentation texts for the workforce capabilities (SPEC-008 §6).
    private static readonly IReadOnlyDictionary<string, (string Es, string En)> WorkforceTexts =
        new Dictionary<string, (string Es, string En)>(StringComparer.Ordinal)
        {
            ["type.vacation"] = ("Vacación", "Vacation"),
            ["type.calamity"] = ("Calamidad", "Calamity"),
            ["status.approved"] = ("Aprobado", "Approved"),
            ["status.rejected"] = ("Rechazado", "Rejected"),
            ["status.pending"] = ("Pendiente", "Pending"),
            ["status.in-progress"] = ("En curso", "In progress"),
            ["status.completed"] = ("Completado", "Completed"),
            ["unit.workday"] = ("día laborable", "workday"),
            ["summary.juan"] = (
                "El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.",
                "The synthetic HCM reports an approved vacation absence for Juan Pérez."),
            ["summary.pedro"] = (
                "El HCM sintético informa la ausencia de vacaciones en curso de Pedro Rojas.",
                "The synthetic HCM reports an in-progress vacation absence for Pedro Rojas."),
            ["summary.mauricio"] = (
                "El HCM sintético informa una solicitud de calamidad rechazada de Mauricio Cardenas.",
                "The synthetic HCM reports a rejected calamity request for Mauricio Cardenas."),
            ["task.payroll"] = (
                "Preparar el informe de nómina de octubre",
                "Prepare the October payroll report"),
            ["task.onboarding"] = (
                "Revisar el alta de nuevas incorporaciones",
                "Review new hire onboarding"),
            ["task.archive"] = (
                "Archivar la documentación del trimestre",
                "Archive the quarterly documentation"),
            ["task.timesheet"] = (
                "Validar los marcajes de la semana",
                "Validate the week's punch records"),
            ["summary.task"] = (
                "El HCM sintético asigna esta tarea a Mauricio Cardenas.",
                "The synthetic HCM assigns this task to Mauricio Cardenas.")
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<HcmTimeOffRequest>>
        TimeOffByLanguage = new Dictionary<string, IReadOnlyList<HcmTimeOffRequest>>(
            StringComparer.Ordinal)
        {
            ["es"] = BuildTimeOff(SyntheticHcmAttendanceSource.Spanish),
            ["en"] = BuildTimeOff(SyntheticHcmAttendanceSource.English)
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<HcmTask>>
        TasksByLanguage = new Dictionary<string, IReadOnlyList<HcmTask>>(
            StringComparer.Ordinal)
        {
            ["es"] = BuildTasks(SyntheticHcmAttendanceSource.Spanish),
            ["en"] = BuildTasks(SyntheticHcmAttendanceSource.English)
        };

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

    private static string L(string language, string key)
    {
        if (!WorkforceTexts.TryGetValue(key, out var text))
        {
            throw new InvalidOperationException(
                $"The workforce text '{key}' is missing from the synthetic catalog.");
        }

        return string.Equals(language, SyntheticHcmAttendanceSource.English, StringComparison.Ordinal)
            ? text.En
            : text.Es;
    }

    private static IReadOnlyList<HcmTimeOffRequest> BuildTimeOff(string language) =>
        Array.AsReadOnly(
        [
            new HcmTimeOffRequest(
                "timeoff-juan-2026-10",
                "employee-demo-juan",
                L(language, "type.vacation"),
                L(language, "status.approved"),
                new DateOnly(2026, 10, 13),
                new DateOnly(2026, 10, 16),
                "3",
                L(language, "unit.workday"),
                L(language, "summary.juan")),
            new HcmTimeOffRequest(
                "timeoff-pedro-2026-10",
                "employee-demo-pedro",
                L(language, "type.vacation"),
                L(language, "status.approved"),
                new DateOnly(2026, 10, 15),
                new DateOnly(2026, 10, 20),
                "4",
                L(language, "unit.workday"),
                L(language, "summary.pedro")),
            new HcmTimeOffRequest(
                "timeoff-mauricio-2026-10",
                "employee-demo-mauricio",
                L(language, "type.calamity"),
                L(language, "status.rejected"),
                new DateOnly(2026, 10, 16),
                new DateOnly(2026, 10, 17),
                "2",
                L(language, "unit.workday"),
                L(language, "summary.mauricio"))
        ]);

    private static IReadOnlyList<HcmTask> BuildTasks(string language) =>
        Array.AsReadOnly(
        [
            new HcmTask(
                "task-mauricio-payroll",
                "employee-demo-mauricio",
                L(language, "task.payroll"),
                L(language, "status.pending"),
                new DateOnly(2026, 10, 20),
                L(language, "summary.task")),
            new HcmTask(
                "task-mauricio-onboarding",
                "employee-demo-mauricio",
                L(language, "task.onboarding"),
                L(language, "status.in-progress"),
                new DateOnly(2026, 10, 22),
                null),
            new HcmTask(
                "task-mauricio-archive",
                "employee-demo-mauricio",
                L(language, "task.archive"),
                L(language, "status.completed"),
                new DateOnly(2026, 10, 10),
                null),
            new HcmTask(
                "task-juan-timesheet",
                "employee-demo-juan",
                L(language, "task.timesheet"),
                L(language, "status.pending"),
                new DateOnly(2026, 10, 21),
                null)
        ]);

    private static HashSet<string>? ResolveEmployeeIds(EmployeeDataQuery query, string language)
    {
        if (!string.IsNullOrWhiteSpace(query.EmployeeId))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                query.EmployeeId
            };
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            return DirectoryFor(language)
                .Where(employee =>
                    employee.DisplayName.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .Select(employee => employee.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // No filter: the whole collection (no silent selection).
        return null;
    }

    public Task<IReadOnlyList<HcmTimeOffRequest>> GetTimeOffAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var employeeIds = ResolveEmployeeIds(query, query.Language);
        var requests = TimeOffFor(query.Language)
            .Where(request => employeeIds is null || employeeIds.Contains(request.EmployeeId))
            .ToArray();

        return Task.FromResult<IReadOnlyList<HcmTimeOffRequest>>(requests);
    }

    public Task<IReadOnlyList<HcmTask>> GetTasksAsync(
        EmployeeDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var employeeIds = ResolveEmployeeIds(query, query.Language);
        var tasks = TasksFor(query.Language)
            .Where(task => employeeIds is null || employeeIds.Contains(task.EmployeeId))
            .ToArray();

        return Task.FromResult<IReadOnlyList<HcmTask>>(tasks);
    }

    private static IReadOnlyList<HcmTimeOffRequest> TimeOffFor(string language) =>
        TimeOffByLanguage.TryGetValue(language, out var requests)
            ? requests
            : TimeOffByLanguage[SyntheticHcmAttendanceSource.Spanish];

    private static IReadOnlyList<HcmTask> TasksFor(string language) =>
        TasksByLanguage.TryGetValue(language, out var tasks)
            ? tasks
            : TasksByLanguage[SyntheticHcmAttendanceSource.Spanish];
}
