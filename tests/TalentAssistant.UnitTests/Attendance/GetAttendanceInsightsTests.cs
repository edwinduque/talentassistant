using TalentAssistant.Application.Attendance;

namespace TalentAssistant.UnitTests.Attendance;

public sealed class GetAttendanceInsightsTests
{
    [Fact]
    public async Task ReturnsHcmFindingAndOfficeEvidenceWithoutRecalculatingRules()
    {
        var insight = CreateInsight();
        var source = new RecordingHcmAttendanceSource([insight]);
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16),
            "employee-demo-001");
        var useCase = new GetAttendanceInsights(source);

        var result = await useCase.ExecuteAsync(query);

        Assert.Same(query, source.ReceivedQuery);
        Assert.Same(insight, Assert.Single(result));
        Assert.Equal("HCM-SYN-RULE-001", result[0].RuleFinding.RuleReference);
        Assert.Equal("País de ejemplo", result[0].Office.Country);
        Assert.Equal("Ciudad de ejemplo", result[0].Office.City);
        Assert.Contains("HCM synthetic evidence", result[0].RuleFinding.Evidence);
    }

    [Fact]
    public async Task RejectsAnEmptyOrReversedDateRange()
    {
        var source = new RecordingHcmAttendanceSource([]);
        var useCase = new GetAttendanceInsights(source);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            new AttendanceInsightQuery(
                new DateOnly(2026, 10, 16),
                new DateOnly(2026, 10, 16))));

        Assert.Null(source.ReceivedQuery);
    }

    private static HcmAttendanceInsight CreateInsight() => new(
        "incident-demo-001",
        new HcmEmployeeReference(
            "employee-demo-001",
            "Synthetic employee 001",
            ["office-demo-001"]),
        new HcmOfficeReference(
            "office-demo-001",
            "País de ejemplo",
            "Ciudad de ejemplo",
            "Etc/UTC"),
        new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero),
        new HcmWorkSchedule(
            new DateOnly(2026, 10, 15),
            new TimeOnly(9, 0),
            new TimeOnly(17, 0)),
        [new HcmPunch(
            new DateTimeOffset(2026, 10, 15, 9, 2, 0, TimeSpan.Zero),
            "Synthetic attendance event")],
        new HcmRuleFinding(
            "HCM-SYN-RULE-001",
            true,
            "The synthetic HCM reports an attendance finding for review.",
            new DateTimeOffset(2026, 10, 15, 9, 5, 0, TimeSpan.Zero),
            ["HCM synthetic evidence"],
            ["Review the originating HCM record."]),
        new HcmTimeOffContext(
            "timeoff-demo-001",
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16),
            "Related synthetic HCM context"));

    private sealed class RecordingHcmAttendanceSource(
        IReadOnlyList<HcmAttendanceInsight> response) : IHcmAttendanceSource
    {
        public AttendanceInsightQuery? ReceivedQuery { get; private set; }

        public Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
            AttendanceInsightQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;
            return Task.FromResult(response);
        }
    }
}
