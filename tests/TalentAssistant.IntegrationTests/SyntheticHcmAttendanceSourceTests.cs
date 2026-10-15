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
            new DateOnly(2026, 10, 16));

        var result = await source.GetAttendanceInsightsAsync(query);

        Assert.Equal(
            new[] { "incident-demo-madrid-start", "incident-demo-tokyo-start" },
            result.Select(item => item.IncidentId));
        Assert.DoesNotContain(
            result,
            item => item.IncidentId == "incident-demo-madrid-next-day");
    }

    [Fact]
    public async Task FiltersBySyntheticEmployeeIdAndProvidesOfficeGeography()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 16),
            "employee-demo-tokyo");

        var result = await source.GetAttendanceInsightsAsync(query);

        var insight = Assert.Single(result);
        Assert.Equal("Japan", insight.Office.Country);
        Assert.Equal("Tokyo", insight.Office.City);
        Assert.Equal("Asia/Tokyo", insight.Office.IanaTimeZoneId);
        Assert.Contains("office-demo-tokyo", insight.Employee.OfficeIds);
    }
}
