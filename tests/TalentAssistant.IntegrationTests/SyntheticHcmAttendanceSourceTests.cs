using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;

namespace TalentAssistant.IntegrationTests;

public sealed class SyntheticHcmAttendanceSourceTests
{
    [Fact]
    public async Task UsesOfficeLocalDatesWithInclusiveFromAndExclusiveTo()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16),
            Language: "en");

        var result = await source.GetAttendanceInsightsAsync(query);

        Assert.Equal(
            new[] { "incident-demo-madrid-start", "incident-demo-tokyo-start" },
            result.Select(item => item.IncidentId));
        Assert.DoesNotContain(
            result,
            item => item.IncidentId == "incident-demo-madrid-next-day");
        Assert.Equal("Vacation", result[0].RelatedTimeOff?.Type);
        Assert.Equal("Approved", result[0].RelatedTimeOff?.Status);
        Assert.Equal("Birthday", result[1].RelatedTimeOff?.Type);
        Assert.Equal("Pending", result[1].RelatedTimeOff?.Status);
    }

    [Fact]
    public async Task PreservesUnlinkedRejectedAndCancelledTimeOffContexts()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 16),
            new DateOnly(2026, 10, 18),
            Language: "en");

        var result = await source.GetAttendanceInsightsAsync(query);

        Assert.Equal(
            new[]
            {
                "incident-demo-madrid-next-day",
                "incident-demo-madrid-rejected",
                "incident-demo-tokyo-cancelled"
            },
            result.Select(item => item.IncidentId));
        Assert.Null(result[0].RelatedTimeOff);
        Assert.Equal("Calamity", result[1].RelatedTimeOff?.Type);
        Assert.Equal("Rejected", result[1].RelatedTimeOff?.Status);
        Assert.Equal("Maternity leave", result[2].RelatedTimeOff?.Type);
        Assert.Equal("Cancelled", result[2].RelatedTimeOff?.Status);
    }

    [Fact]
    public async Task FiltersBySyntheticEmployeeIdAndProvidesOfficeGeography()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16),
            "employee-demo-tokyo",
            Language: "en");

        var result = await source.GetAttendanceInsightsAsync(query);

        var insight = Assert.Single(result);
        Assert.Equal("Japan", insight.Office.Country);
        Assert.Equal("Tokyo", insight.Office.City);
        Assert.Equal("Asia/Tokyo", insight.Office.IanaTimeZoneId);
        Assert.Contains("office-demo-tokyo", insight.Employee.OfficeIds);
    }

    [Fact]
    public async Task ServesSpanishTextsWhenNoLanguageIsRequested()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16));

        var result = await source.GetAttendanceInsightsAsync(query);

        var first = Assert.Single(
            result,
            item => item.IncidentId == "incident-demo-madrid-start");
        Assert.Equal("Vacación", first.RelatedTimeOff?.Type);
        Assert.Equal("Aprobado", first.RelatedTimeOff?.Status);
        Assert.Equal("España", first.Office.Country);
        Assert.Equal("Empleado sintético 001", first.Employee.DisplayName);

        // Identifiers stay identical to the English variant (SPEC-007 §6).
        var english = await source.GetAttendanceInsightsAsync(query with { Language = "en" });
        Assert.Equal(
            english.Select(item => item.IncidentId),
            result.Select(item => item.IncidentId));
    }
}
