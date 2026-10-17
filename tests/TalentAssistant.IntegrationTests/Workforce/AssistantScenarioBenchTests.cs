using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Workforce;

/// <summary>
/// The user's literal question set (SPEC-008 §4) as an automated bench: each
/// question maps to the capability calls the future assistant will make
/// (search -> tool -> answer) over the deterministic fixtures. No AI, no network.
/// Note: the source performs case-insensitive contains without accent folding
/// (SPEC-008 S1), so the bench searches by the unaccented first name, which is
/// what a tool-calling assistant would extract from the question.
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
    public void Question1AnswersWithEmployeesWorkingToday()
    {
        Assert.Equal("Dime cuales empleados ya estan trabajando.", Question1);

        var snapshot = Source.GetPresenceAsync(new PresenceQuery(Today)).Result;
        var working = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.WorkingState)
            .Select(entry => entry.Employee.DisplayName)
            .ToArray();

        Assert.NotEmpty(working);
        Assert.Contains("Juan Pérez", working);
        Assert.Contains("Pedro Rojas", working);
    }

    [Fact]
    public void Question2AnswersWithEmployeesNotArrived()
    {
        Assert.Equal("Dime que empleados no han llegado aun.", Question2);

        var snapshot = Source.GetPresenceAsync(new PresenceQuery(Today)).Result;
        var notArrived = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.NotArrivedState)
            .Select(entry => entry.Employee.DisplayName)
            .ToArray();

        Assert.NotEmpty(notArrived);
        Assert.Contains("Mauricio Cardenas", notArrived);
    }

    [Fact]
    public void Question3AnswersWithEmployeesOnVacation()
    {
        Assert.Equal("Dime que empleados estan de vacaciones.", Question3);

        var snapshot = Source.GetPresenceAsync(new PresenceQuery(Today)).Result;
        var onVacation = snapshot.Entries
            .Count(entry => entry.State == SyntheticWorkforceSource.VacationState);

        Assert.True(onVacation >= 1, $"Expected at least one employee on vacation, found {onVacation}.");
    }

    [Fact]
    public void Question4AnswersJuanPerezVacationDaysFromTheSource()
    {
        Assert.Equal("Dime cuantos dias de vacaciones tiene Juan Perez.", Question4);

        var found = Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan")).Result;
        var juan = Assert.Single(found);

        var requests = Source.GetTimeOffAsync(
            new EmployeeDataQuery(EmployeeId: juan.Id)).Result;
        var vacation = Assert.Single(requests);

        Assert.Equal("Vacación", vacation.Type);
        Assert.Equal("Aprobado", vacation.Status);
        Assert.Equal("3", vacation.Duration);
        Assert.Equal("día laborable", vacation.DurationUnit);

        // Full name with accent works too; only the unaccented form does not fold.
        Assert.Single(Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan Pérez")).Result);
    }

    [Fact]
    public void Question5AnswersPedroRojasReturnDate()
    {
        Assert.Equal("Dime cuando regresa Pedro Rojas.", Question5);

        var found = Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Pedro")).Result;
        var pedro = Assert.Single(found);

        var requests = Source.GetTimeOffAsync(
            new EmployeeDataQuery(EmployeeId: pedro.Id)).Result;
        var absence = Assert.Single(requests);

        Assert.Equal(new DateOnly(2026, 10, 20), absence.EndsBefore);
        Assert.Equal("Aprobado", absence.Status);
    }

    [Fact]
    public void Question6AnswersMauricioCardenasPendingTasks()
    {
        Assert.Equal("Dime que tareas tiene pendiente Mauricio Cardenas.", Question6);

        var found = Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Mauricio")).Result;
        var mauricio = Assert.Single(found);

        var tasks = Source.GetTasksAsync(new EmployeeDataQuery(EmployeeId: mauricio.Id)).Result;
        var pending = tasks.Where(task => task.Status == "Pendiente").ToArray();

        Assert.NotEmpty(pending);
        Assert.Contains(tasks, task => task.Status == "En curso");
        Assert.Contains(tasks, task => task.Status == "Completado");
    }
}
