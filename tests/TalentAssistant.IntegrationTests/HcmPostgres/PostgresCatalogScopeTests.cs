using System.Text.RegularExpressions;
using TalentAssistant.Application.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// SPEC-012 amendment 2026-10-25: the contract serves every active employee,
/// not only the nine fixtures. These assertions pin the widened catalog itself
/// - scope, unique ids and references, and the projection of the generated
/// absences from their origin data - while the nine fixtures keep exact parity
/// with the synthetic source in <see cref="PostgresWorkforceSourceParityTests"/>.
/// </summary>
public sealed class PostgresCatalogScopeTests : PostgresSourceTestBase
{
    private static readonly Regex GeneratedReference =
        new(@"^timeoff-\d+$", RegexOptions.Compiled);

    private static readonly Regex GeneratedEmployeeId =
        new(@"^employee-\d+$", RegexOptions.Compiled);

    private static readonly string[] SpanishStatuses =
        ["Pendiente", "Aprobado", "Rechazado"];

    private static readonly string[] EnglishStatuses =
        ["Pending", "Approved", "Rejected"];

    private static readonly string[] PresenceStates =
        ["vacation", "working", "absent", "not-arrived"];

    [Fact]
    public async Task DirectoryServesEveryActiveEmployeeBeyondTheNineFixtures()
    {
        var directory = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());

        Assert.True(
            directory.Count > 9,
            $"Expected a catalog wider than the nine fixtures, got {directory.Count}.");
        Assert.Contains(directory, employee => GeneratedEmployeeId.IsMatch(employee.Id));
        Assert.Contains(
            directory,
            employee => employee.OfficeIds.Any(
                office => !office.StartsWith("office-demo-", StringComparison.Ordinal)));
        Assert.Equal(
            directory.Count,
            directory.Select(employee => employee.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            directory,
            employee => Assert.All(
                employee.OfficeIds,
                office => Assert.False(string.IsNullOrWhiteSpace(office))));
    }

    [Fact]
    public async Task PresenceScopeMatchesTheDirectoryScope()
    {
        var databaseToday = await ReadDatabaseCurrentDateAsync();
        var directory = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());
        var presence = await Workforce.GetPresenceAsync(new PresenceQuery(databaseToday));

        // Both views derive their scope from the same active-employee rule, so
        // one presence row per served employee is the invariant that ties them.
        Assert.Equal(directory.Count, presence.Entries.Count);
        Assert.Contains(
            presence.Entries,
            entry => GeneratedEmployeeId.IsMatch(entry.Employee.Id));
        Assert.All(
            presence.Entries,
            entry => Assert.Contains(entry.State, PresenceStates));
    }

    [Fact]
    public async Task GeneratedAbsencesProjectTheirOriginTypeStatusAndSummary()
    {
        var spanish = await Workforce.GetTimeOffAsync(new EmployeeDataQuery());
        var english = await Workforce.GetTimeOffAsync(
            new EmployeeDataQuery(Language: "en"));

        Assert.Equal(spanish.Count, english.Count);
        Assert.Equal(
            spanish.Count,
            spanish.Select(item => item.Reference).Distinct(StringComparer.Ordinal).Count());

        var generated = spanish
            .Where(item => GeneratedReference.IsMatch(item.Reference))
            .ToArray();
        var generatedEnglish = english
            .Where(item => GeneratedReference.IsMatch(item.Reference))
            .ToArray();

        Assert.NotEmpty(generated);
        Assert.Equal(generated.Length, generatedEnglish.Length);
        Assert.All(generated, item => Assert.False(string.IsNullOrWhiteSpace(item.Type)));
        Assert.All(generated, item => Assert.Contains(item.Status, SpanishStatuses));
        Assert.All(generatedEnglish, item => Assert.Contains(item.Status, EnglishStatuses));

        // The seed carries every status and open-ended absences, so the widened
        // view must expose them rather than silently narrowing to approved ones.
        Assert.Contains(generated, item => item.Status == "Pendiente");
        Assert.Contains(generated, item => item.Status == "Rechazado");
        Assert.Contains(generated, item => item.EndsBefore is null && item.Duration is null);

        // Product decision 2026-10-25: a real absence carries the same source
        // text in both languages, so summary_es and summary_en project note.
        foreach (var record in generated)
        {
            var counterpart = generatedEnglish
                .Single(item => item.Reference == record.Reference);
            Assert.Equal(record.Summary, counterpart.Summary);
            Assert.Equal(record.StartsOn, counterpart.StartsOn);
            Assert.Equal(record.EndsBefore, counterpart.EndsBefore);
        }
    }

    [Fact]
    public async Task FixtureAbsencesKeepTheirBilingualParityTexts()
    {
        var spanish = await Workforce.GetTimeOffAsync(new EmployeeDataQuery());
        var english = await Workforce.GetTimeOffAsync(
            new EmployeeDataQuery(Name: "Juan", Language: "en"));

        var fixture = spanish.Single(item => item.Reference == "timeoff-juan-2026-10");
        var fixtureInEnglish = english.Single(item => item.Reference == "timeoff-juan-2026-10");

        Assert.NotEqual(fixture.Summary, fixtureInEnglish.Summary);
        Assert.Contains("vacaciones", fixture.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vacation", fixtureInEnglish.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
