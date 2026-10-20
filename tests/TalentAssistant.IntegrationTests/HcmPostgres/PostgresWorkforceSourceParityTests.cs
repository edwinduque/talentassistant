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
/// Office ids match exactly: the seed was corrected to the fixtures'
/// 'office-demo-*' convention during the T2 reconciliation.
/// </summary>
public sealed class PostgresWorkforceSourceParityTests : PostgresSourceTestBase
{
    private static readonly SyntheticWorkforceSource Synthetic = new();

    [Fact]
    public async Task DirectoryParityOverIdsNamesAndOffices()
    {
        var synthetic = await Synthetic.SearchEmployeesAsync(new EmployeeSearchQuery());
        var postgres = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());

        Assert.Equal(9, synthetic.Count);
        Assert.Equal(9, postgres.Count);
        Assert.Equal(
            synthetic.Select(employee => employee.Id).Order(),
            postgres.Select(employee => employee.Id).Order());

        var syntheticById = synthetic.ToDictionary(employee => employee.Id, StringComparer.Ordinal);
        foreach (var entry in postgres)
        {
            var expected = syntheticById[entry.Id];
            Assert.Equal(expected.DisplayName, entry.DisplayName);
            Assert.Single(entry.OfficeIds);
            Assert.Equal(expected.OfficeIds, entry.OfficeIds);
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

        Assert.Equal(
            synthetic.Select(employee => employee.Id).Order(),
            postgres.Select(employee => employee.Id).Order());

        // Proper nouns are data and identical in both languages (SPEC-007 §6).
        Assert.Equal(
            synthetic.Single(employee => employee.Id == "employee-demo-juan").DisplayName,
            postgres.Single(employee => employee.Id == "employee-demo-juan").DisplayName);

        // The contract view exposes a single display_name column: Postgres
        // declares that one name in every language (the HCM owns its payload),
        // while the synthetic source translates its own catalog - documented
        // difference of the two sources, not a parity failure.
        Assert.Equal(
            "Empleado sintético 001",
            postgres.Single(employee => employee.Id == "employee-demo-madrid").DisplayName);
        Assert.Equal(
            language == "en" ? "Synthetic employee 001" : "Empleado sintético 001",
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

        Assert.Equal(9, postgres.Entries.Count);
        Assert.Equal(StateMap(synthetic), StateMap(postgres));

        var states = StateMap(postgres);

        // SPEC-012 T3 (default3 aprobado): la ausencia fixture de Pedro terminó
        // el19 (inclusivo); desde el20 ambos lados derivan not-arrived (el
        // fixture sintético de presencia se corrigió a la par en esta tarea).
        Assert.Equal("not-arrived", states["employee-demo-pedro"]);
        Assert.Equal("working", states["employee-demo-juan"]);
        Assert.Equal("not-arrived", states["employee-demo-mauricio"]);

        foreach (var entry in postgres.Entries)
        {
            var expected = synthetic.Entries.Single(
                candidate => candidate.Employee.Id == entry.Employee.Id);
            Assert.Equal(expected.Employee.DisplayName, entry.Employee.DisplayName);
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
        Assert.Equal(3, postgres.Count);
        Assert.Equal(ByReference(synthetic), ByReference(postgres));
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

        Assert.Equal(4, synthetic.Count);
        Assert.Equal(4, postgres.Count);
        Assert.Equal(ByReference(synthetic), ByReference(postgres));
    }

    private static IReadOnlyList<HcmTimeOffRequest> ByReference(
        IReadOnlyList<HcmTimeOffRequest> requests) =>
        requests.OrderBy(request => request.Reference, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<HcmTask> ByReference(IReadOnlyList<HcmTask> tasks) =>
        tasks.OrderBy(task => task.Reference, StringComparer.Ordinal).ToArray();

    private static Dictionary<string, string> StateMap(HcmPresenceSnapshot snapshot) =>
        snapshot.Entries.ToDictionary(
            entry => entry.Employee.Id,
            entry => entry.State,
            StringComparer.Ordinal);
}
