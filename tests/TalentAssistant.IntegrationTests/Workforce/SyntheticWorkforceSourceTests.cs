using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Workforce;

public sealed class SyntheticWorkforceSourceTests
{
    private static readonly SyntheticWorkforceSource Source = new();

    [Fact]
    public async Task FindsDemoEmployeesByNameCaseInsensitively()
    {
        var lower = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "juan"));
        var upper = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "JUAN"));

        var employee = Assert.Single(lower);
        Assert.Equal("employee-demo-juan", employee.Id);
        Assert.Equal("Juan Pérez", employee.DisplayName);
        Assert.Equal(employee.Id, Assert.Single(upper).Id);

        var partial = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "rojas"));
        Assert.Equal("Pedro Rojas", Assert.Single(partial).DisplayName);
    }

    [Fact]
    public async Task EmptySearchReturnsTheWholeDirectoryInBothLanguages()
    {
        var spanish = await Source.SearchEmployeesAsync(new EmployeeSearchQuery());
        var english = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Language: "en"));

        Assert.True(spanish.Count >= 8, $"Expected at least 8 employees, found {spanish.Count}");
        Assert.Equal(spanish.Count, english.Count);
        Assert.Equal(
            spanish.Select(employee => employee.Id).Order(),
            english.Select(employee => employee.Id).Order());

        // Shared ids, translated names for the synthetic employees.
        var syntheticSpanish = spanish.Single(employee => employee.Id == "employee-demo-madrid");
        var syntheticEnglish = english.Single(employee => employee.Id == "employee-demo-madrid");
        Assert.Equal("Empleado sintético 001", syntheticSpanish.DisplayName);
        Assert.Equal("Synthetic employee 001", syntheticEnglish.DisplayName);

        // Demo employees are proper nouns: identical in both languages.
        Assert.Contains(spanish, employee => employee.DisplayName == "Mauricio Cardenas");
        Assert.Contains(english, employee => employee.DisplayName == "Mauricio Cardenas");
    }

    [Fact]
    public async Task MultipleNameMatchesReturnTheListWithoutChoosing()
    {
        var matches = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "employee", Language: "en"));

        Assert.True(matches.Count >= 5, $"Expected several matches, found {matches.Count}");
    }

    [Fact]
    public async Task PresenceForTodayAnswersTheThreeHeadlineQuestions()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var snapshot = await Source.GetPresenceAsync(new PresenceQuery(today));

        Assert.Equal(today, snapshot.Date);
        Assert.NotEmpty(snapshot.Entries);
        Assert.All(snapshot.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Employee.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(entry.State));
        });

        Assert.Contains(snapshot.Entries, entry => entry.State == SyntheticWorkforceSource.WorkingState);
        Assert.Contains(snapshot.Entries, entry => entry.State == SyntheticWorkforceSource.NotArrivedState);
        Assert.Contains(snapshot.Entries, entry => entry.State == SyntheticWorkforceSource.VacationState);

        var working = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.WorkingState)
            .Select(entry => entry.Employee.DisplayName)
            .ToArray();
        Assert.Contains("Juan Pérez", working);
        Assert.Contains("Pedro Rojas", working);

        var onVacation = snapshot.Entries
            .Where(entry => entry.State == SyntheticWorkforceSource.VacationState)
            .Select(entry => entry.Employee.Id)
            .ToArray();
        Assert.NotEmpty(onVacation);
    }

    [Fact]
    public async Task PresenceForUnknownDateIsEmptyInsteadOfInvented()
    {
        var snapshot = await Source.GetPresenceAsync(
            new PresenceQuery(DateOnly.FromDateTime(DateTime.Today).AddDays(30)));

        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public async Task PresenceResolvesEmployeeNamesInTheRequestedLanguage()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var spanish = await Source.GetPresenceAsync(new PresenceQuery(today));
        var english = await Source.GetPresenceAsync(
            new PresenceQuery(today, Language: "en"));

        Assert.Equal(
            spanish.Entries.Select(entry => entry.Employee.Id).Order(),
            english.Entries.Select(entry => entry.Employee.Id).Order());
        Assert.Contains(spanish.Entries,
            entry => entry.Employee.DisplayName.StartsWith("Empleado sintético"));
        Assert.Contains(english.Entries,
            entry => entry.Employee.DisplayName.StartsWith("Synthetic employee"));
    }
}
