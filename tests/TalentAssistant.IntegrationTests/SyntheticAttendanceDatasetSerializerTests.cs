using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;

namespace TalentAssistant.IntegrationTests;

public sealed class SyntheticAttendanceDatasetSerializerTests
{
    private static IReadOnlyList<HcmAttendanceInsight> Fixtures(string language) =>
        SyntheticHcmAttendanceSource.FixturesFor(language);

    private static SyntheticAttendancePayload RoundTrip(
        IReadOnlyList<HcmAttendanceInsight> spanish,
        IReadOnlyList<HcmAttendanceInsight> english)
    {
        var payloadJson = SyntheticAttendanceDatasetSerializer.Serialize(spanish, english);
        return SyntheticAttendanceDatasetSerializer.Deserialize(
            SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion,
            payloadJson);
    }

    [Fact]
    public void RoundTripsBothLanguageVariantsWithoutChangingSourceValues()
    {
        var spanish = Fixtures("es");
        var english = Fixtures("en");

        var restored = RoundTrip(spanish, english);

        // Shared identifiers are byte-identical across variants and after the round trip.
        Assert.Equal(spanish.Select(i => i.IncidentId), restored.Es.Select(i => i.IncidentId));
        Assert.Equal(english.Select(i => i.IncidentId), restored.En.Select(i => i.IncidentId));
        Assert.Equal(spanish.Select(i => i.IncidentId), english.Select(i => i.IncidentId));
        Assert.Equal(
            spanish.Select(i => i.RuleFinding.Summary),
            restored.Es.Select(i => i.RuleFinding.Summary));
        Assert.Equal(
            english.Select(i => i.RuleFinding.Summary),
            restored.En.Select(i => i.RuleFinding.Summary));

        // Presentation values are translated per variant.
        Assert.Equal("Vacación", restored.Es[0].RelatedTimeOff?.Type);
        Assert.Equal("Aprobado", restored.Es[0].RelatedTimeOff?.Status);
        Assert.Equal("Vacation", restored.En[0].RelatedTimeOff?.Type);
        Assert.Equal("Approved", restored.En[0].RelatedTimeOff?.Status);
        Assert.Equal("Cumpleaños", restored.Es[1].RelatedTimeOff?.Type);
        Assert.Equal("Pendiente", restored.Es[1].RelatedTimeOff?.Status);
        Assert.Null(restored.Es.Single(
            insight => insight.IncidentId == "incident-demo-madrid-next-day").RelatedTimeOff);
        Assert.Null(restored.En.Single(
            insight => insight.IncidentId == "incident-demo-madrid-next-day").RelatedTimeOff);

        // The variants actually differ where translation applies.
        Assert.NotEqual(
            restored.Es[0].RuleFinding.Summary,
            restored.En[0].RuleFinding.Summary);
        Assert.Equal("España", restored.Es[0].Office.Country);
        Assert.Equal("Spain", restored.En[0].Office.Country);
    }

    [Fact]
    public void SelectsTheVariantForTheRequestedLanguageWithSpanishFallback()
    {
        var payload = RoundTrip(Fixtures("es"), Fixtures("en"));

        Assert.Equal(Fixtures("es").Select(i => i.IncidentId),
            payload.ForLanguage("es").Select(i => i.IncidentId));
        Assert.Equal(Fixtures("en").Select(i => i.IncidentId),
            payload.ForLanguage("en").Select(i => i.IncidentId));
        Assert.Equal("Aprobado", payload.ForLanguage("es")[0].RelatedTimeOff?.Status);
        Assert.Equal("Approved", payload.ForLanguage("en")[0].RelatedTimeOff?.Status);

        // Unknown language falls back to the Spanish variant (SPEC-007 §8).
        Assert.Same(payload.ForLanguage("es"), payload.ForLanguage("fr"));
    }

    [Fact]
    public void RejectsAnUnsupportedPayloadVersion()
    {
        Assert.Throws<InvalidDataException>(() =>
            SyntheticAttendanceDatasetSerializer.Deserialize(
                SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion + 1,
                "{}"));
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
