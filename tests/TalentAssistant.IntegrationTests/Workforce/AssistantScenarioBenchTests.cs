using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Workforce;

/// <summary>
/// The user's literal question set (SPEC-008 §4) as an automated bench: each
/// question maps to the capability calls the future assistant will make
/// (search -> tool -> answer) over the deterministic fixtures. No AI, no network.
/// Note: the source folds accents on both sides of a name match (SPEC-008 S1
/// as amended 2026-10-18), so the bench's unaccented full name "Juan Perez"
/// resolves to the single employee Juan Pérez; the shared first name "Juan"
/// would match both Juans and the assistant must disambiguate (SPEC-009
/// AC-032), so the bench never narrows to one Juan by first name alone.
/// </summary>
public sealed class AssistantScenarioBenchTests
{
    private static readonly SyntheticWorkforceSource Source = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public const string Question1 = "Dime cuales empleados ya estan trabajando.";
    public const string Question2 = "Dime que empleados no han llegado aun.";
    public const string Question3 = "Dime que empleados estan de vacaciones.";
    public const string Question4 = "Dime cuantos dias de vacaciones tiene Juan Perez.";
    public const string Question5 = "Dime cuando regresa Pedro Rojas.";
    public const string Question6 = "Dime que tareas tiene pendiente Mauricio Cardenas.";

    [Fact]
    public async Task Question1AnswersWithEmployeesWorkingToday()
    {
        Assert.Equal("Dime cuales empleados ya estan trabajando.", Question1);

        var snapshot = await Source.GetPresenceAsync(new PresenceQuery(Today));
        var working = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.WorkingState)
            .Select(entry => entry.Employee.DisplayName)
            .ToArray();

        Assert.NotEmpty(working);
        Assert.Contains("Juan Pérez", working);
        Assert.Contains("Juan Herrera", working);

        // Pedro Rojas is on an approved leave that covers today (15->20 Oct,
        // ends_before 20): presence and timeoff must tell the same story
        // (fixture-consistency fix, 2026-10-18), so he is NOT working.
        Assert.DoesNotContain("Pedro Rojas", working);
    }

    [Fact]
    public async Task Question2AnswersWithEmployeesNotArrived()
    {
        Assert.Equal("Dime que empleados no han llegado aun.", Question2);

        var snapshot = await Source.GetPresenceAsync(new PresenceQuery(Today));
        var notArrived = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.NotArrivedState)
            .Select(entry => entry.Employee.DisplayName)
            .ToArray();

        Assert.NotEmpty(notArrived);
        Assert.Contains("Mauricio Cardenas", notArrived);
    }

    [Fact]
    public async Task Question3AnswersWithEmployeesOnVacation()
    {
        Assert.Equal("Dime que empleados estan de vacaciones.", Question3);

        var snapshot = await Source.GetPresenceAsync(new PresenceQuery(Today));
        var onVacation = snapshot.Entries
            .Count(entry => entry.State == SyntheticWorkforceSource.VacationState);

        Assert.True(onVacation >= 1, $"Expected at least one employee on vacation, found {onVacation}.");
    }

    [Fact]
    public async Task Question4AnswersJuanPerezVacationDaysFromTheSource()
    {
        Assert.Equal("Dime cuantos dias de vacaciones tiene Juan Perez.", Question4);

        // The unaccented full name from the literal question folds to the
        // accented display name and yields the single matching employee.
        var found = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan Perez"));
        var juan = Assert.Single(found);
        Assert.Equal("Juan Pérez", juan.DisplayName);

        var requests = await Source.GetTimeOffAsync(new EmployeeDataQuery(EmployeeId: juan.Id));
        var vacation = Assert.Single(requests);

        Assert.Equal("Vacación", vacation.Type);
        Assert.Equal("Aprobado", vacation.Status);
        Assert.Equal("3", vacation.Duration);
        Assert.Equal("día laborable", vacation.DurationUnit);

        // The accented full name resolves to the same single employee.
        Assert.Single(await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan Pérez")));
    }

    [Fact]
    public async Task Question5AnswersPedroRojasReturnDate()
    {
        Assert.Equal("Dime cuando regresa Pedro Rojas.", Question5);

        var found = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Pedro"));
        var pedro = Assert.Single(found);

        var requests = await Source.GetTimeOffAsync(new EmployeeDataQuery(EmployeeId: pedro.Id));
        var absence = Assert.Single(requests);

        Assert.Equal(new DateOnly(2026, 10, 20), absence.EndsBefore);
        Assert.Equal("Aprobado", absence.Status);
    }

    [Fact]
    public async Task Question6AnswersMauricioCardenasPendingTasks()
    {
        Assert.Equal("Dime que tareas tiene pendiente Mauricio Cardenas.", Question6);

        var found = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Mauricio"));
        var mauricio = Assert.Single(found);

        var tasks = await Source.GetTasksAsync(new EmployeeDataQuery(EmployeeId: mauricio.Id));
        var pending = tasks.Where(task => task.Status == "Pendiente").ToArray();

        Assert.NotEmpty(pending);
        Assert.Contains(tasks, task => task.Status == "En curso");
        Assert.Contains(tasks, task => task.Status == "Completado");
    }
}
