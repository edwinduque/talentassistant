using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Common;

namespace TalentAssistant.Infrastructure.Workforce;

/// <summary>
/// Synthetic implementation of the workforce port (SPEC-008). The five
/// employees that already have incidents are derived from the shared attendance
/// catalog so their display names stay identical in both languages; four demo
/// employees make the user's questions literal, with a second "Juan" (Juan
/// Herrera) forcing name disambiguation (SPEC-008 amendment 2026-10-18).
/// Presence states are supplied data of this synthetic HCM: never derived here
/// from punches or calendars. The snapshot covers local today and the next two
/// days (fixtures anchored to the machine's local date, as SPEC-008 assumes);
/// other dates return an empty snapshot, not an invented state.
/// </summary>
public sealed class SyntheticWorkforceSource : IHcmWorkforceSource
{
    public const string WorkingState = "working";
    public const string NotArrivedState = "not-arrived";
    public const string VacationState = "vacation";
    public const string AbsentState = "absent";

    private sealed record DemoEmployee(string Id, string DisplayName, string OfficeId);
    private sealed record EmployeeProfile(
        string? JobTitle,
        string? LastName,
        DateOnly? HireDate,
        int? BirthdayMonth,
        int? BirthdayDay,
        int? BirthYear);

    // Proper nouns are data and stay identical across languages (SPEC-007 §6).
    // Juan Herrera exists only to share the first name "Juan" with Juan Pérez
    // and force the assistant's disambiguation rule (SPEC-008 amendment).
    private static readonly IReadOnlyList<DemoEmployee> DemoEmployees =
        Array.AsReadOnly<DemoEmployee>(
        [
            new("employee-demo-juan", "Juan Pérez", "office-demo-madrid"),
            new("employee-demo-pedro", "Pedro Rojas", "office-demo-tokyo"),
            new("employee-demo-mauricio", "Mauricio Cardenas", "office-demo-madrid"),
            new("employee-demo-herrera", "Juan Herrera", "office-demo-tokyo")
        ]);

    // Explicit synthetic HCM fields approved for assistant employee search and
    // the upcoming-birthday query. No other employee columns are projected.
    // BirthYear mirrors db/hcm-real/seed.sql and only feeds the derived
    // AgeOnNextBirthday (SPEC-008 §3, approved 2026-10-24): it never reaches a
    // payload or an assistant-facing contract.
    private static readonly IReadOnlyDictionary<string, EmployeeProfile> EmployeeProfiles =
        new Dictionary<string, EmployeeProfile>(StringComparer.Ordinal)
        {
            ["employee-demo-juan"] = new(
                "Responsable de equipo", "Pérez", new DateOnly(2019, 2, 11), 4, 12, 1985),
            ["employee-demo-pedro"] = new(
                "Especialista de soporte", "Rojas", new DateOnly(2020, 6, 1), 7, 23, 1990),
            ["employee-demo-mauricio"] = new(
                "Técnico de nómina", "Cardenas", new DateOnly(2018, 9, 17), 11, 30, 1988),
            ["employee-demo-herrera"] = new(
                "Analista de jornada", "Herrera", new DateOnly(2021, 3, 22), 2, 18, 1992),
            ["employee-demo-madrid"] = new(
                "Operario de planta", "Vidal", new DateOnly(2022, 1, 10), null, null, null),
            ["employee-demo-tokyo-002"] = new(
                "Operario de planta", "Domínguez", new DateOnly(2022, 5, 16), 5, 9, 1995),
            ["employee-demo-madrid-004"] = new(
                null, "Soler", new DateOnly(2023, 4, 3), 9, 27, 1996),
            ["employee-demo-tokyo"] = new(
                "Mozo de almacén", "Pardo", new DateOnly(2023, 8, 21), 12, 2, 1997),
            ["employee-demo-madrid-002"] = new(
                null, "Castaño", new DateOnly(2024, 11, 5), 3, 14, 1998)
        };

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
                ("employee-demo-herrera", WorkingState),
                // Corrección del ancla temporal (SPEC-012 T3, default3
                // aprobado): su ausencia fixture (15..19 inclusivo) expiró
                // ayer, así que hoy ya no está de vacaciones; sin marcaciones
                // de hoy, hoy no ha comenzado jornada.
                ("employee-demo-pedro", NotArrivedState),
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
                ("employee-demo-pedro", VacationState),
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
                NameMatching.Matches(employee.DisplayName, query.Name));
        }

        var surnameContains = query.SurnameContains;
        if (!string.IsNullOrWhiteSpace(surnameContains))
        {
            employees = employees.Where(employee =>
                !string.IsNullOrWhiteSpace(employee.LastName)
                && NameMatching.Matches(employee.LastName, surnameContains));
        }

        if (!string.IsNullOrWhiteSpace(query.OfficeId))
        {
            employees = employees.Where(employee =>
                employee.OfficeIds.Any(id =>
                    string.Equals(id, query.OfficeId, StringComparison.OrdinalIgnoreCase)));
        }

        // Derived age (SPEC-008 §3, approved 2026-10-24): computed per call from
        // this source's own today, mirroring how presence anchors to the local
        // date; null when the fixture has no birthday.
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Task.FromResult<IReadOnlyList<HcmEmployeeDirectoryEntry>>(
            employees
                .Select(employee => employee with
                {
                    AgeOnNextBirthday = ComputeAgeOnNextBirthday(employee.Id, today)
                })
                .ToArray());
    }

    public Task<IReadOnlyList<HcmUpcomingBirthday>> GetUpcomingBirthdaysAsync(
        UpcomingBirthdayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = DirectoryFor(query.Language)
            .Where(employee => EmployeeProfiles.TryGetValue(employee.Id, out var profile)
                && profile.BirthdayMonth.HasValue
                && profile.BirthdayDay.HasValue)
            .Select(employee =>
            {
                var profile = EmployeeProfiles[employee.Id];
                var next = NextBirthday(
                    query.AsOfDate,
                    profile.BirthdayMonth!.Value,
                    profile.BirthdayDay!.Value);

                // Derived age (SPEC-008 §3): turned AT the returned date and
                // anchored to the query date, not to the wall clock.
                return new HcmUpcomingBirthday(
                    employee.Id,
                    employee.DisplayName,
                    next,
                    profile.BirthYear is { } birthYear ? next.Year - birthYear : null);
            })
            .OrderBy(employee => employee.NextBirthdayOn)
            .ToArray();

        if (candidates.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<HcmUpcomingBirthday>>(
                Array.Empty<HcmUpcomingBirthday>());
        }

        var nearestDate = candidates[0].NextBirthdayOn;
        return Task.FromResult<IReadOnlyList<HcmUpcomingBirthday>>(
            candidates
                .Where(employee => employee.NextBirthdayOn == nearestDate)
                .OrderBy(employee => employee.EmployeeId, StringComparer.Ordinal)
                .ToArray());
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
            .Select(insight =>
            {
                EmployeeProfiles.TryGetValue(insight.Employee.Id, out var profile);
                return new HcmEmployeeDirectoryEntry(
                    insight.Employee.Id,
                    insight.Employee.DisplayName,
                    insight.Employee.OfficeIds,
                    profile?.JobTitle,
                    profile?.LastName,
                    profile?.HireDate);
            })
            .DistinctBy(employee => employee.Id);

        var demoEmployees = DemoEmployees.Select(employee =>
        {
            var profile = EmployeeProfiles[employee.Id];
            return new HcmEmployeeDirectoryEntry(
                employee.Id,
                employee.DisplayName,
                Array.AsReadOnly([employee.OfficeId]),
                profile.JobTitle,
                profile.LastName,
                profile.HireDate);
        });

        return Array.AsReadOnly([.. fromCatalog, .. demoEmployees]);
    }

    private static DateOnly NextBirthday(DateOnly asOfDate, int month, int day)
    {
        for (var year = asOfDate.Year; year <= asOfDate.Year + 8; year++)
        {
            // A February 29 birthday occurs only on February 29; no substitute
            // date is inferred for non-leap years.
            if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            {
                continue;
            }

            var candidate = new DateOnly(year, month, day);
            if (candidate >= asOfDate)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No upcoming birthday could be found.");
    }

    /// <summary>
    /// Derived age approved 2026-10-24 (SPEC-008 §3): the age the employee
    /// turns at their next birthday from <paramref name="asOfDate"/>; null when
    /// the fixture has no birthday. The birth year never leaves this method.
    /// </summary>
    private static int? ComputeAgeOnNextBirthday(string employeeId, DateOnly asOfDate)
    {
        if (!EmployeeProfiles.TryGetValue(employeeId, out var profile)
            || profile.BirthdayMonth is not { } month
            || profile.BirthdayDay is not { } day
            || profile.BirthYear is not { } birthYear)
        {
            return null;
        }

        return NextBirthday(asOfDate, month, day).Year - birthYear;
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
                // Corrección (SPEC-012 T3, default4 aprobado: duración =
                // ends_before - starts_on): la ventana15..20 (exclusivo)
                // cubre15,16,17,18,19 = CINCO días; el "4" original era
                // internamente inconsistente (la misma clase de error que
                // el "2" de Mauricio).
                "5",
                L(language, "unit.workday"),
                L(language, "summary.pedro")),
            new HcmTimeOffRequest(
                "timeoff-mauricio-2026-10",
                "employee-demo-mauricio",
                L(language, "type.calamity"),
                L(language, "status.rejected"),
                new DateOnly(2026, 10, 16),
                new DateOnly(2026, 10, 17),
                // Corrección (SPEC-012 T3, default4 aprobado): la ventana
                // 16..17 (exclusivo) cubre UN día; el "2" original era
                // internamente inconsistente y la derivación del contrato
                // produce1.
                "1",
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
                .Where(employee => NameMatching.Matches(employee.DisplayName, query.Name))
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
