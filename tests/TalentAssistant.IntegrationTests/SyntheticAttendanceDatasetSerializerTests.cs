using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;

namespace TalentAssistant.IntegrationTests;

public sealed class SyntheticAttendanceDatasetSerializerTests
{
    [Fact]
    public async Task RoundTripsSyntheticHcmAttendanceResponsesWithoutChangingSourceValues()
    {
        var source = new SyntheticHcmAttendanceSource();
        var query = new AttendanceInsightQuery(
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 10, 18));
        var original = await source.GetAttendanceInsightsAsync(query);

        var payload = SyntheticAttendanceDatasetSerializer.Serialize(original);
        var restored = SyntheticAttendanceDatasetSerializer.Deserialize(
            SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion,
            payload);

        Assert.Equal(
            original.Select(insight => insight.IncidentId),
            restored.Select(insight => insight.IncidentId));
        Assert.Equal(
            original.Select(insight => insight.RuleFinding.Summary),
            restored.Select(insight => insight.RuleFinding.Summary));
        Assert.Equal("Vacation", restored[0].RelatedTimeOff?.Type);
        Assert.Equal("Approved", restored[0].RelatedTimeOff?.Status);
        Assert.Equal("Birthday", restored[1].RelatedTimeOff?.Type);
        Assert.Equal("Pending", restored[1].RelatedTimeOff?.Status);
        Assert.Null(restored.Single(
            insight => insight.IncidentId == "incident-demo-madrid-next-day").RelatedTimeOff);
    }

    [Fact]
    public void RejectsAnUnsupportedPayloadVersion()
    {
        Assert.Throws<InvalidDataException>(() =>
            SyntheticAttendanceDatasetSerializer.Deserialize(
                SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion + 1,
                "[]"));
    }

    [Fact]
    public void RejectsMalformedPayload()
    {
        Assert.Throws<InvalidDataException>(() =>
            SyntheticAttendanceDatasetSerializer.Deserialize(
                SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion,
                "{"));
    }

}
