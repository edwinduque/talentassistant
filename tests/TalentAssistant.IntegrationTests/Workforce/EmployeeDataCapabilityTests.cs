using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Workforce;

public sealed class EmployeeDataCapabilityTests
{
    private static readonly SyntheticWorkforceSource Source = new();

    [Fact]
    public async Task JuanPerezVacationAnswersTheDaysQuestion()
    {
        var requests = await Source.GetTimeOffAsync(new EmployeeDataQuery(Name: "juan"));

        var request = Assert.Single(requests);
        Assert.Equal("employee-demo-juan", request.EmployeeId);
        Assert.Equal("timeoff-juan-2026-10", request.Reference);
        Assert.Equal("Vacación", request.Type);
        Assert.Equal("Aprobado", request.Status);
        Assert.Equal("3", request.Duration);
        Assert.Equal("día laborable", request.DurationUnit);
        Assert.Equal(new DateOnly(2026, 10, 16), request.EndsBefore);
        Assert.Contains("Juan Pérez", request.Summary);
    }

    [Fact]
    public async Task PedroRojasAnswersTheReturnDateQuestion()
    {
        var requests = await Source.GetTimeOffAsync(new EmployeeDataQuery(Name: "pedro"));

        var request = Assert.Single(requests);
        Assert.Equal("employee-demo-pedro", request.EmployeeId);
        Assert.Equal("Aprobado", request.Status);
        Assert.Equal(new DateOnly(2026, 10, 15), request.StartsOn);
        Assert.Equal(new DateOnly(2026, 10, 20), request.EndsBefore);
    }

    [Fact]
    public async Task MauricioCardenasTasksAnswerThePendingTaskQuestion()
    {
        var tasks = await Source.GetTasksAsync(new EmployeeDataQuery(Name: "mauricio"));

        Assert.Equal(3, tasks.Count);
        Assert.All(tasks, task => Assert.Equal("employee-demo-mauricio", task.EmployeeId));
        Assert.Contains(tasks, task => task.Status == "Pendiente");
        Assert.Contains(tasks, task => task.Status == "En curso");
        Assert.Contains(tasks, task => task.Status == "Completado");
        var payroll = tasks.Single(task => task.Reference == "task-mauricio-payroll");
        Assert.Equal("Preparar el informe de nómina de octubre", payroll.Title);
    }

    [Fact]
    public async Task FiltersByIdAndReturnsEverythingWithoutAFilter()
    {
        var byId = await Source.GetTimeOffAsync(
            new EmployeeDataQuery(EmployeeId: "employee-demo-juan"));
        Assert.Single(byId);

        var allTimeOff = await Source.GetTimeOffAsync(new EmployeeDataQuery());
        Assert.Equal(3, allTimeOff.Count);

        var allTasks = await Source.GetTasksAsync(new EmployeeDataQuery());
        Assert.Equal(4, allTasks.Count);
    }

    [Fact]
    public async Task UnknownNameReturnsAnEmptyListWithoutSilentSelection()
    {
        Assert.Empty(await Source.GetTimeOffAsync(new EmployeeDataQuery(Name: "nobody")));
        Assert.Empty(await Source.GetTasksAsync(new EmployeeDataQuery(Name: "nobody")));
    }

    [Fact]
    public async Task EnglishVariantKeepsReferencesAndTranslatesValues()
    {
        var spanish = await Source.GetTimeOffAsync(new EmployeeDataQuery(Name: "juan"));
        var english = await Source.GetTimeOffAsync(
            new EmployeeDataQuery(Name: "JUAN", Language: "en"));

        var request = Assert.Single(english);
        Assert.Equal(spanish.Single().Reference, request.Reference);
        Assert.Equal("Vacation", request.Type);
        Assert.Equal("Approved", request.Status);
        Assert.Equal("workday", request.DurationUnit);

        var tasks = await Source.GetTasksAsync(
            new EmployeeDataQuery(Name: "MAURICIO", Language: "en"));
        Assert.Contains(tasks, task => task.Title == "Prepare the October payroll report");
        Assert.Contains(tasks, task => task.Status == "Pending");
    }
}
