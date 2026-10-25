using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-036 parity: the same queries against <see cref="SyntheticWorkforceSource"/>
/// and <see cref="PostgresWorkforceSource"/> must return equal results - ids,
/// display names, states and the full bilingual records - because both read the
/// same seeded facts (the local contract seed mirrors the synthetic fixtures).
/// Known difference asserted explicitly instead of hidden (reported with this
/// task):
/// * "today": each source anchors presence to its own clock (synthetic fixtures
///   to the host's local date, the seed to the database's CURRENT_DATE/UTC day).
/// * "age": the derived AgeOnNextBirthday follows the same per-source anchor
///   (synthetic: the host's local date; the contract view: CURRENT_DATE), while
///   upcoming-birthday ages must match exactly for an explicit query date.
/// Office ids match exactly: the seed was corrected to the fixtures'
/// 'office-demo-*' convention during the T2 reconciliation.
/// Scope (SPEC-012 amendment 2026-10-25): the contract view serves every
/// active employee while <see cref="SyntheticWorkforceSource"/> serves only
/// the nine fixtures, so parity runs over the fixture subset and the widened
/// catalog itself is pinned by PostgresCatalogScopeTests.
/// </summary>
public sealed class PostgresWorkforceSourceParityTests : PostgresSourceTestBase
{
    private static readonly SyntheticWorkforceSource Synthetic = new();

    [Fact]
    public async Task DirectoryParityOverIdsNamesAndOffices()
    {
        var syntheticToday = DateOnly.FromDateTime(DateTime.Today);
        var databaseToday = await ReadDatabaseCurrentDateAsync();

        var synthetic = await Synthetic.SearchEmployeesAsync(new EmployeeSearchQuery());
        var postgres = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());

        // The synthetic source serves only the nine fixtures; the contract view
        // serves every active employee (SPEC-012 amendment 2026-10-25), so the
        // parity runs over the fixture subset and the widened catalog itself is
        // pinned by PostgresCatalogScopeTests.
        Assert.Equal(9, synthetic.Count);
        Assert.True(
            postgres.Count > synthetic.Count,
            $"Expected a catalog wider than the nine fixtures, got {postgres.Count}.");

        var postgresById = postgres.ToDictionary(employee => employee.Id, StringComparer.Ordinal);
        Assert.Equal(postgres.Count, postgresById.Count);
        foreach (var expected in synthetic)
        {
            var matches = postgres
                .Where(candidate => candidate.Id == expected.Id)
                .ToArray();
            Assert.True(
                matches.Length == 1,
                $"The contract view must serve fixture '{expected.Id}' exactly once.");
            var entry = matches[0];
            Assert.Equal(expected.DisplayName, entry.DisplayName);
            Assert.Single(entry.OfficeIds);
            Assert.Equal(expected.OfficeIds, entry.OfficeIds);
            Assert.Equal(expected.JobTitle, entry.JobTitle);
            Assert.Equal(expected.LastName, entry.LastName);
            Assert.Equal(expected.HireDate, entry.HireDate);
        }

        // Derived age (approved 2026-10-24) per source anchor, like presence
        // "today": Mauricio (born 1988) and employee 001 without a birthday.
        Assert.Equal(
            NextBirthdayOn(syntheticToday, 11, 30).Year - 1988,
            synthetic.Single(employee => employee.Id == "employee-demo-mauricio")
                .AgeOnNextBirthday);
        Assert.Equal(
            NextBirthdayOn(databaseToday, 11, 30).Year - 1988,
            postgres.Single(employee => employee.Id == "employee-demo-mauricio")
                .AgeOnNextBirthday);
        Assert.Null(
            synthetic.Single(employee => employee.Id == "employee-demo-madrid")
                .AgeOnNextBirthday);
        Assert.Null(
            postgres.Single(employee => employee.Id == "employee-demo-madrid")
                .AgeOnNextBirthday);
    }

    [Theory]
    [InlineData(2026, 9, 1)]
    [InlineData(2026, 10, 23)]
    [InlineData(2027, 12, 1)]
    public async Task NextBirthdayFollowsTheContractRuleForTheWholeCatalog(
        int year,
        int month,
        int day)
    {
        // Killer dates for a CURRENT_DATE-anchored age (L4/L6): a past query
        // date and a future one must both pair next_birthday_on with the age
        // turned at that occurrence. The synthetic source only holds the nine
        // fixtures, so the widened catalog is checked against the contract's
        // own rule instead of against the synthetic answer.
        var asOfDate = new DateOnly(year, month, day);
        var query = new UpcomingBirthdayQuery(asOfDate);
        var databaseToday = await ReadDatabaseCurrentDateAsync();
        var rows = await ReadContractEmployeesAsync();

        var postgres = await Workforce.GetUpcomingBirthdaysAsync(query);
        Assert.Equal(
            ExpectedUpcomingBirthdays(rows, asOfDate, databaseToday),
            postgres
                .OrderBy(birthday => birthday.EmployeeId, StringComparer.Ordinal)
                .ToArray());

        // The synthetic side keeps its own fixture parity: the same rule over
        // the nine fixture rows of the contract view must reproduce it exactly.
        var synthetic = await Synthetic.GetUpcomingBirthdaysAsync(query);
        var fixtures = rows
            .Where(row => row.Id.StartsWith("employee-demo-", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            ExpectedUpcomingBirthdays(fixtures, asOfDate, databaseToday),
            synthetic
                .OrderBy(birthday => birthday.EmployeeId, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task SurnameFragmentSearchMatchesAcrossSources()
    {
        var query = new EmployeeSearchQuery(SurnameContains: "z");

        var synthetic = await Synthetic.SearchEmployeesAsync(query);
        var postgres = await Workforce.SearchEmployeesAsync(query);

        // The widened catalog also matches generated surnames containing "z"
        // (for example "Vázquez"); fixture parity is checked by stable id.
        // Compare fields rather than records because OfficeIds is a list.
        foreach (var expected in synthetic)
        {
            var employee = postgres.Single(candidate => candidate.Id == expected.Id);
            Assert.Equal(expected.DisplayName, employee.DisplayName);
            Assert.Equal(expected.LastName, employee.LastName);
            Assert.Equal(expected.HireDate, employee.HireDate);
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task DirectoryIdParityHoldsInEveryLanguage(string language)
    {
        var synthetic = await Synthetic.SearchEmployeesAsync(
            new EmployeeSearchQuery(Language: language));
        var postgres = await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(Language: language));

        var postgresIds = postgres
            .Select(employee => employee.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(synthetic, employee => Assert.Contains(employee.Id, postgresIds));

        // Proper nouns are data and identical in both languages (SPEC-007 §6).
        Assert.Equal(
            synthetic.Single(employee => employee.Id == "employee-demo-juan").DisplayName,
            postgres.Single(employee => employee.Id == "employee-demo-juan").DisplayName);

        // The contract view exposes a single display_name column: Postgres
        // declares that one name in every language (the HCM owns its payload),
        // while the synthetic source translates its own catalog - documented
        // difference of the two sources, not a parity failure.
        Assert.Equal(
            "Aitana Vidal",
            postgres.Single(employee => employee.Id == "employee-demo-madrid").DisplayName);
        Assert.Equal(
            "Aitana Vidal",
            synthetic.Single(employee => employee.Id == "employee-demo-madrid").DisplayName);
    }

    [Theory]
    [InlineData("juan")]
    [InlineData("JUAN")]
    [InlineData("Juan Perez")]
    [InlineData("rojas")]
    [InlineData("mauricio")]
    [InlineData("Empleado")]
    [InlineData("nobody")]
    public async Task NameSearchParityProvesBothSourcesShareOneMatcher(string name)
    {
        var synthetic = await Synthetic.SearchEmployeesAsync(new EmployeeSearchQuery(Name: name));
        var postgres = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery(Name: name));

        Assert.Equal(
            synthetic.Select(employee => employee.Id).Order(StringComparer.Ordinal),
            postgres.Select(employee => employee.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SharedFirstNameStillReturnsBothJuansWithoutChoosing()
    {
        var matches = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery(Name: "Juan"));

        Assert.Equal(
            new[] { "employee-demo-herrera", "employee-demo-juan" },
            matches.Select(employee => employee.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task OfficeFilterSelectsTheSameEmployeesInBothSources()
    {
        var synthetic = await Synthetic.SearchEmployeesAsync(
            new EmployeeSearchQuery(OfficeId: "office-demo-tokyo"));
        var postgres = await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(OfficeId: "office-demo-tokyo"));

        Assert.Equal(
            synthetic.Select(employee => employee.Id).Order(StringComparer.Ordinal),
            postgres.Select(employee => employee.Id).Order(StringComparer.Ordinal));
        Assert.Equal(4, postgres.Count);
        Assert.Contains("employee-demo-pedro", postgres.Select(employee => employee.Id));
        Assert.DoesNotContain("employee-demo-juan", postgres.Select(employee => employee.Id));
    }

    [Fact]
    public async Task TodaysPresenceStatesMatchTheSyntheticSource()
    {
        // Each source anchors "today" to its own clock (see class docs): the
        // dates are read per source and the id -> state maps are what must agree.
        var syntheticToday = DateOnly.FromDateTime(DateTime.Today);
        var databaseToday = await ReadDatabaseCurrentDateAsync();

        var synthetic = await Synthetic.GetPresenceAsync(new PresenceQuery(syntheticToday));
        var postgres = await Workforce.GetPresenceAsync(new PresenceQuery(databaseToday));

        Assert.Equal(9, synthetic.Entries.Count);
        if (postgres.Entries.Count == 0)
        {
            Assert.Fail(
                "The local contract seed has no presence rows for the database's current date. "
                + "The seeded volume pins presence to the day it was initialized; refresh the "
                + "hcm_postgres_data volume (db/ and compose are T1-owned) before treating this "
                + "as a parity failure.");
        }

        // The contract view derives presence for every active employee, the
        // synthetic source only for the nine fixtures: parity runs over the
        // fixture subset (SPEC-012 amendment 2026-10-25).
        Assert.True(
            postgres.Entries.Count >= synthetic.Entries.Count,
            $"Expected at least the nine fixtures, got {postgres.Entries.Count}.");

        var states = StateMap(postgres);
        foreach (var entry in synthetic.Entries)
        {
            Assert.True(
                states.ContainsKey(entry.Employee.Id),
                $"The contract presence dropped fixture '{entry.Employee.Id}'.");
            Assert.Equal(entry.State, states[entry.Employee.Id]);
        }
        Assert.Equal(postgres.Entries.Count, states.Count);

        // SPEC-012 T3 (default3 aprobado): la ausencia fixture de Pedro terminó
        // el19 (inclusivo); desde el20 ambos lados derivan not-arrived (el
        // fixture sintético de presencia se corrigió a la par en esta tarea).
        Assert.Equal("not-arrived", states["employee-demo-pedro"]);
        Assert.Equal("working", states["employee-demo-juan"]);
        Assert.Equal("not-arrived", states["employee-demo-mauricio"]);

        foreach (var entry in synthetic.Entries)
        {
            var actual = postgres.Entries.Single(
                candidate => candidate.Employee.Id == entry.Employee.Id);
            Assert.Equal(entry.Employee.DisplayName, actual.Employee.DisplayName);
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task TimeOffParityOverEveryFieldAndLanguage(string language)
    {
        var query = new EmployeeDataQuery(Language: language);
        var synthetic = await Synthetic.GetTimeOffAsync(query);
        var postgres = await Workforce.GetTimeOffAsync(query);

        Assert.Equal(3, synthetic.Count);
        Assert.True(
            postgres.Count > synthetic.Count,
            $"Expected absences beyond the three fixtures, got {postgres.Count}.");
        foreach (var fixture in synthetic)
        {
            Assert.Contains(fixture, postgres);
        }
    }

    [Theory]
    [InlineData("employee-demo-juan")]
    [InlineData("employee-demo-pedro")]
    [InlineData("employee-demo-mauricio")]
    [InlineData("employee-demo-herrera")]
    public async Task TimeOffParityForEveryReference(string employeeId)
    {
        var query = new EmployeeDataQuery(EmployeeId: employeeId);
        var synthetic = await Synthetic.GetTimeOffAsync(query);
        var postgres = await Workforce.GetTimeOffAsync(query);

        Assert.Equal(ByReference(synthetic), ByReference(postgres));
    }

    [Theory]
    [InlineData("Juan Perez")]
    [InlineData("Juan")]
    [InlineData("juan")]
    [InlineData("nobody")]
    public async Task NameResolvedTimeOffAndTasksParity(string name)
    {
        var query = new EmployeeDataQuery(Name: name);

        Assert.Equal(
            ByReference(await Synthetic.GetTimeOffAsync(query)),
            ByReference(await Workforce.GetTimeOffAsync(query)));
        Assert.Equal(
            ByReference(await Synthetic.GetTasksAsync(query)),
            ByReference(await Workforce.GetTasksAsync(query)));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task TaskParityOverEveryFieldAndLanguage(string language)
    {
        var query = new EmployeeDataQuery(Language: language);
        var synthetic = await Synthetic.GetTasksAsync(query);
        var postgres = await Workforce.GetTasksAsync(query);
        var fixtureReferences = synthetic
            .Select(task => task.Reference)
            .ToHashSet(StringComparer.Ordinal);
        var postgresFixtures = postgres
            .Where(task => fixtureReferences.Contains(task.Reference))
            .ToArray();

        Assert.Equal(4, synthetic.Count);
        Assert.True(postgres.Count >= 2_000, $"Expected at least 2,000 tasks, got {postgres.Count}.");
        Assert.Equal(ByReference(synthetic), ByReference(postgresFixtures));
    }

    private static IReadOnlyList<HcmTimeOffRequest> ByReference(
        IReadOnlyList<HcmTimeOffRequest> requests) =>
        requests.OrderBy(request => request.Reference, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<HcmTask> ByReference(IReadOnlyList<HcmTask> tasks) =>
        tasks.OrderBy(task => task.Reference, StringComparer.Ordinal).ToArray();

    // Independent mirror of the contract's next-birthday rule (SPEC-008 §3):
    // for every contract row the next occurrence on or after the query date;
    // the birth year recovered as year(next on CURRENT_DATE) minus the view's
    // own derived age; the age turned at that occurrence; the earliest date
    // wins and every employee sharing it is returned - exactly what the
    // adapter's SQL does, so the widened catalog can be checked without read
    // access to the physical tables.
    private static IReadOnlyList<HcmUpcomingBirthday> ExpectedUpcomingBirthdays(
        IReadOnlyList<ContractEmployee> rows,
        DateOnly asOfDate,
        DateOnly databaseToday)
    {
        var candidates = new List<HcmUpcomingBirthday>();
        foreach (var row in rows)
        {
            if (row.BirthdayMonth is not int month
                || row.BirthdayDay is not int day
                || row.AgeOnNextBirthday is not int ageToday)
            {
                continue;
            }

            var nextOnQueryDate = NextBirthdayOn(asOfDate, month, day);
            var birthYear = NextBirthdayOn(databaseToday, month, day).Year - ageToday;
            candidates.Add(new HcmUpcomingBirthday(
                row.Id,
                row.DisplayName,
                nextOnQueryDate,
                nextOnQueryDate.Year - birthYear));
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "The contract view served no employee with a birthday.");
        }

        var earliest = candidates.Min(candidate => candidate.NextBirthdayOn);
        return candidates
            .Where(candidate => candidate.NextBirthdayOn == earliest)
            .OrderBy(candidate => candidate.EmployeeId, StringComparer.Ordinal)
            .ToArray();
    }

    // Mirror of the sources' next-occurrence rule (Feb 29 only on Feb 29) so
    // expected ages derive from each source's own anchor date.
    private static DateOnly NextBirthdayOn(DateOnly asOfDate, int month, int day)
    {
        for (var year = asOfDate.Year; year <= asOfDate.Year + 8; year++)
        {
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

        throw new InvalidOperationException("No upcoming birthday found for the test anchor.");
    }

    private static Dictionary<string, string> StateMap(HcmPresenceSnapshot snapshot) =>
        snapshot.Entries.ToDictionary(
            entry => entry.Employee.Id,
            entry => entry.State,
            StringComparer.Ordinal);
}
