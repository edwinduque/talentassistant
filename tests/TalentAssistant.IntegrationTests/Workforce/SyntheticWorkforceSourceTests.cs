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

        // Amended SPEC-008 S1 (AC-026): the shared first name matches both
        // Juans (Pérez and Herrera) without silent selection.
        Assert.Equal(
            new[] { "employee-demo-herrera", "employee-demo-juan" },
            lower.Select(employee => employee.Id).Order());
        Assert.Equal(
            lower.Select(employee => employee.Id).Order(),
            upper.Select(employee => employee.Id).Order());

        var partial = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "rojas"));
        Assert.Equal("Pedro Rojas", Assert.Single(partial).DisplayName);
    }

    [Fact]
    public async Task SurnameFragmentSearchFindsTheMatchingSurnameCaseInsensitively()
    {
        var lower = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(SurnameContains: "z"));
        var upper = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(SurnameContains: "Z"));

        var employee = Assert.Single(lower);
        Assert.Equal("employee-demo-juan", employee.Id);
        Assert.Equal("Pérez", employee.LastName);
        Assert.Equal(lower.Select(entry => entry.Id), upper.Select(entry => entry.Id));
    }

    [Fact]
    public async Task EmployeeDirectoryReportsTheSourceHireDate()
    {
        var matches = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Mauricio"));

        var mauricio = Assert.Single(matches);
        Assert.Equal(new DateOnly(2018, 9, 17), mauricio.HireDate);
    }

    [Fact]
    public async Task UnaccentedNameFoldsToTheAccentedDisplayName()
    {
        // SPEC-008 S1 as amended 2026-10-18 (AC-026): «Juan Perez» without the
        // accent must locate «Juan Pérez» because both sides of the name match
        // are normalized before comparing.
        var matches = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Juan Perez"));

        var employee = Assert.Single(matches);
        Assert.Equal("Juan Pérez", employee.DisplayName);
    }

    [Fact]
    public async Task SharedFirstNameReturnsBothJuansWithoutChoosing()
    {
        // SPEC-008 S1 as amended 2026-10-18 (AC-026): a shared short name
        // returns every match so the assistant can disambiguate instead of
        // picking silently.
        var matches = await Source.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan"));

        Assert.Equal(2, matches.Count);
        var names = matches.Select(employee => employee.DisplayName).ToArray();
        Assert.Contains("Juan Pérez", names);
        Assert.Contains("Juan Herrera", names);
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
    public async Task UpcomingBirthdayReturnsTheNearestCalendarOccurrenceFromTheSource()
    {
        var birthdays = await Source.GetUpcomingBirthdaysAsync(
            new UpcomingBirthdayQuery(new DateOnly(2026, 10, 23)));

        var next = Assert.Single(birthdays);
        Assert.Equal("employee-demo-mauricio", next.EmployeeId);
        Assert.Equal("Mauricio Cardenas", next.DisplayName);
        Assert.Equal(new DateOnly(2026, 11, 30), next.NextBirthdayOn);
        Assert.Equal(38, next.AgeOnNextBirthday); // 2026-11-30 minus birth year 1988.

        var birthdayToday = await Source.GetUpcomingBirthdaysAsync(
            new UpcomingBirthdayQuery(new DateOnly(2026, 11, 30)));
        var today = Assert.Single(birthdayToday);
        Assert.Equal("employee-demo-mauricio", today.EmployeeId);
        Assert.Equal(new DateOnly(2026, 11, 30), today.NextBirthdayOn);
        Assert.Equal(38, today.AgeOnNextBirthday);
    }

    [Theory]
    [InlineData(2026, 9, 1, 2026, 9, 27, "employee-demo-madrid-004", 30)]
    [InlineData(2027, 12, 1, 2027, 12, 2, "employee-demo-tokyo", 30)]
    public async Task UpcomingBirthdayAgeAnchorsToTheQueryDateNotTheWallClock(
        int year,
        int month,
        int day,
        int nextYear,
        int nextMonth,
        int nextDay,
        string expectedId,
        int expectedAge)
    {
        // Fixed query dates pin both the winner and the derived age no matter
        // when the test runs: 004 (born 1996) turns 30 on 2026-09-27 and 002
        // (born 1997) turns 30 on 2027-12-02. An age computed from the wall
        // clock instead of the query date fails both rows (L4/L6).
        var birthdays = await Source.GetUpcomingBirthdaysAsync(
            new UpcomingBirthdayQuery(new DateOnly(year, month, day)));

        var next = Assert.Single(birthdays);
        Assert.Equal(expectedId, next.EmployeeId);
        Assert.Equal(new DateOnly(nextYear, nextMonth, nextDay), next.NextBirthdayOn);
        Assert.Equal(expectedAge, next.AgeOnNextBirthday);
    }

    [Fact]
    public async Task DirectoryReportsTheDerivedAgeAndNullWithoutABirthday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nextNovember30 = new DateOnly(today.Year, 11, 30);
        if (nextNovember30 < today)
        {
            nextNovember30 = nextNovember30.AddYears(1);
        }

        var mauricio = Assert.Single(await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Mauricio")));
        Assert.Equal(nextNovember30.Year - 1988, mauricio.AgeOnNextBirthday);

        // Employee 001 has no birthday in the source: the derived age is null,
        // never an inferred value.
        var withoutBirthday = Assert.Single(await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Empleado sintético 001")));
        Assert.Null(withoutBirthday.AgeOnNextBirthday);
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
        Assert.Contains("Juan Herrera", working);

        // Pedro Rojas is on an approved leave covering today (15->20 Oct): his
        // presence row must agree with his timeoff request (fixture-consistency
        // fix, 2026-10-18), so he is not working right now.
        Assert.DoesNotContain("Pedro Rojas", working);

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

    [Fact]
    public async Task OfficeIdMatchingStaysExactAndUnfolded()
    {
        // SPEC-009 T4 scope: only the two name-matching predicates fold
        // accents; office ids keep exact matching (verified live: a folded
        // office id must match nothing).
        var tokyo = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(OfficeId: "office-demo-tokyo"));

        var names = tokyo.Select(employee => employee.DisplayName).ToArray();
        Assert.Contains("Pedro Rojas", names);
        Assert.Contains("Juan Herrera", names);
        Assert.DoesNotContain("Juan Pérez", names);

        var folded = await Source.SearchEmployeesAsync(
            new EmployeeSearchQuery(OfficeId: "office-demo-tókyo"));
        Assert.Empty(folded);
    }

    [Fact]
    public async Task AccentFoldingFlowsThroughTimeOffAndTasksByIdResolution()
    {
        // The same helper backs ResolveEmployeeIds, so the unaccented name
        // must resolve the employee for timeoff/tasks too (old behavior
        // returned zero matches).
        var timeOff = await Source.GetTimeOffAsync(
            new EmployeeDataQuery(Name: "Juan Perez"));

        var request = Assert.Single(timeOff);
        Assert.Equal("employee-demo-juan", request.EmployeeId);
        Assert.Equal("timeoff-juan-2026-10", request.Reference);
        Assert.Equal(new DateOnly(2026, 10, 13), request.StartsOn);
        Assert.Equal(new DateOnly(2026, 10, 16), request.EndsBefore);
        Assert.Equal("3", request.Duration);

        var tasks = await Source.GetTasksAsync(
            new EmployeeDataQuery(Name: "Juan Perez"));
        Assert.Equal("task-juan-timesheet", Assert.Single(tasks).Reference);

        // Ambiguous "Juan" unions both matched ids without silent selection;
        // Juan Herrera contributes no timeoff and no tasks (exactly one
        // extra demo employee).
        var both = await Source.GetTimeOffAsync(
            new EmployeeDataQuery(Name: "Juan"));
        Assert.Equal(
            new[] { "timeoff-juan-2026-10" },
            both.Select(item => item.Reference).Order());

        Assert.Empty(await Source.GetTimeOffAsync(
            new EmployeeDataQuery(Name: "Juan Herrera")));
        Assert.Empty(await Source.GetTasksAsync(
            new EmployeeDataQuery(Name: "Juan Herrera")));
    }
}
