using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-036 for the attendance port. The seeded contract row and the synthetic
/// fixture catalog are disjoint by design (T1 seeded one incident with its own
/// id), so parity is asserted on the shared facts - the related TimeOff record
/// must be byte-identical in both sources - while the incident itself is pinned
/// field by field against the contract seed in both languages.
/// </summary>
public sealed class PostgresAttendanceSourceParityTests : PostgresSourceTestBase
{
    private static readonly SyntheticHcmAttendanceSource Synthetic = new();

    private static readonly SyntheticWorkforceSource SyntheticWorkforce = new();

    private static readonly AttendanceInsightQuery Range =
        new(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 17));

    [Fact]
    public async Task TheSeededIncidentAnswersTheSpanishContract()
    {
        var incident = Assert.Single(
            await Attendance.GetAttendanceInsightsAsync(Range with { Language = "es" }));

        Assert.Equal("incident-2026-10-13-juan-late", incident.IncidentId);
        Assert.Equal("employee-demo-juan", incident.Employee.Id);
        Assert.Equal("Juan Pérez", incident.Employee.DisplayName);
        Assert.Contains("Dirección", incident.Employee.OfficeIds);
        Assert.Equal("Europe/Madrid", incident.Office.IanaTimeZoneId);

        Assert.Equal(new DateOnly(2026, 10, 13), incident.Schedule.WorkDate);
        Assert.Equal(new TimeOnly(9, 0), incident.Schedule.StartsAt);
        Assert.Equal(new TimeOnly(17, 0), incident.Schedule.EndsAt);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 13, 6, 52, 0, TimeSpan.Zero),
            incident.IncidentAtUtc);

        // Punches count and the localized punch description (jsonb items).
        var punch = Assert.Single(incident.Punches);
        Assert.Equal("Entrada", punch.Description);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 13, 6, 52, 0, TimeSpan.Zero),
            punch.OccurredAtUtc);

        // Rule fields as evaluated and localized by the source.
        Assert.Equal("arrival-after-schedule", incident.RuleFinding.RuleReference);
        Assert.True(incident.RuleFinding.RequiresReview);
        Assert.Equal(
            "Llegada después del inicio de jornada.",
            incident.RuleFinding.Summary);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 13, 6, 57, 0, TimeSpan.Zero),
            incident.RuleFinding.EvaluatedAtUtc);
        Assert.Equal(
            new[]
            {
                "Fichaje de entrada a las 06:52 UTC",
                "Horario de jornada 09:00-17:00"
            },
            incident.RuleFinding.Evidence);
        Assert.Equal(
            new[] { "Confirmar motivo de la llegada tardía" },
            incident.RuleFinding.ReviewItems);

        var related = Assert.IsType<HcmTimeOffContext>(incident.RelatedTimeOff);
        Assert.Equal("timeoff-juan-2026-10", related.Reference);
        Assert.Equal(new DateOnly(2026, 10, 13), related.StartsOn);
        Assert.Equal(new DateOnly(2026, 10, 16), related.EndsBefore);
        Assert.Equal(
            "El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.",
            related.Summary);
        Assert.Equal("Vacación", related.Type);
        Assert.Equal("Aprobado", related.Status);
        Assert.Equal("3", related.Duration);
        Assert.Equal("día laborable", related.DurationUnit);
    }

    [Fact]
    public async Task TheSeededIncidentAnswersTheEnglishContract()
    {
        var incident = Assert.Single(
            await Attendance.GetAttendanceInsightsAsync(Range with { Language = "en" }));

        Assert.Equal("incident-2026-10-13-juan-late", incident.IncidentId);
        Assert.Equal("Clock-in", Assert.Single(incident.Punches).Description);
        Assert.Equal(
            "Arrival after the shift start.",
            incident.RuleFinding.Summary);
        Assert.Equal(
            new[]
            {
                "Clock-in at 06:52 UTC",
                "Work schedule 09:00-17:00"
            },
            incident.RuleFinding.Evidence);
        Assert.Equal(
            new[] { "Confirm the reason for the late arrival" },
            incident.RuleFinding.ReviewItems);

        var related = Assert.IsType<HcmTimeOffContext>(incident.RelatedTimeOff);
        Assert.Equal(
            "The synthetic HCM reports an approved vacation absence for Juan Pérez.",
            related.Summary);
        Assert.Equal("Vacation", related.Type);
        Assert.Equal("Approved", related.Status);
        Assert.Equal("workday", related.DurationUnit);
    }

    [Fact]
    public async Task TheRelatedTimeOffAgreesWithTheSyntheticTimeOffRecord()
    {
        var incident = Assert.Single(await Attendance.GetAttendanceInsightsAsync(Range));
        var related = Assert.IsType<HcmTimeOffContext>(incident.RelatedTimeOff);

        var syntheticRequest = Assert.Single(
            await SyntheticWorkforce.GetTimeOffAsync(
                new EmployeeDataQuery(EmployeeId: "employee-demo-juan")));

        // Same fact served by both sources: reference, dates, bilingual text
        // picks, type, status, duration and unit must be equal.
        Assert.Equal(syntheticRequest.Reference, related.Reference);
        Assert.Equal(syntheticRequest.StartsOn, related.StartsOn);
        Assert.Equal(syntheticRequest.EndsBefore, related.EndsBefore);
        Assert.Equal(syntheticRequest.Summary, related.Summary);
        Assert.Equal(syntheticRequest.Type, related.Type);
        Assert.Equal(syntheticRequest.Status, related.Status);
        Assert.Equal(syntheticRequest.Duration, related.Duration);
        Assert.Equal(syntheticRequest.DurationUnit, related.DurationUnit);
    }

    [Fact]
    public async Task TheSyntheticSourceKeepsServingItsOwnFixturesForTheSameRange()
    {
        var synthetic = await Synthetic.GetAttendanceInsightsAsync(Range);
        var postgres = await Attendance.GetAttendanceInsightsAsync(Range);

        // Synthetic office-local dates 15/15/16/16 Oct fall inside [13, 17);
        // the seeded incident sits on 13 Oct. The modes never mix: each source
        // serves its own catalog (AC-036) and their incident ids are disjoint.
        Assert.Equal(4, synthetic.Count);
        Assert.Single(postgres);
        Assert.Equal("incident-2026-10-13-juan-late", postgres[0].IncidentId);
        Assert.DoesNotContain(
            postgres,
            insight => insight.IncidentId.StartsWith("incident-demo-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEmployeeFilterSelectsOnlyThatEmployeesIncidents()
    {
        var juan = await Attendance.GetAttendanceInsightsAsync(
            Range with { EmployeeId = "employee-demo-juan" });
        Assert.Equal("employee-demo-juan", Assert.Single(juan).Employee.Id);

        var pedro = await Attendance.GetAttendanceInsightsAsync(
            Range with { EmployeeId = "employee-demo-pedro" });
        Assert.Empty(pedro);
    }

    [Fact]
    public async Task AnInvertedDateRangeIsRejectedBeforeTouchingTheDatabase()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Attendance.GetAttendanceInsightsAsync(
                new AttendanceInsightQuery(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 13))));
    }
}
