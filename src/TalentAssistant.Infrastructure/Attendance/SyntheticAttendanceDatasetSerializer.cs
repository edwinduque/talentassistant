using System.Text.Json;
using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Infrastructure.Attendance;

public sealed record SyntheticAttendancePayload(
    IReadOnlyList<HcmAttendanceInsight> Es,
    IReadOnlyList<HcmAttendanceInsight> En)
{
    /// <summary>
    /// Returns the variant for a supported language and falls back to Spanish
    /// for unknown values (SPEC-007 §8).
    /// </summary>
    public IReadOnlyList<HcmAttendanceInsight> ForLanguage(string language)
    {
        if (string.Equals(language, SyntheticHcmAttendanceSource.English, StringComparison.Ordinal))
        {
            return En;
        }

        return Es;
    }
}

public static class SyntheticAttendanceDatasetSerializer
{
    public const int CurrentPayloadVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Serialize(
        IReadOnlyList<HcmAttendanceInsight> spanishInsights,
        IReadOnlyList<HcmAttendanceInsight> englishInsights)
    {
        ArgumentNullException.ThrowIfNull(spanishInsights);
        ArgumentNullException.ThrowIfNull(englishInsights);
        return JsonSerializer.Serialize(
            new SyntheticAttendancePayload(spanishInsights, englishInsights),
            JsonOptions);
    }

    public static SyntheticAttendancePayload Deserialize(
        int payloadVersion,
        string payloadJson)
    {
        if (payloadVersion != CurrentPayloadVersion)
        {
            throw new InvalidDataException(
                $"Synthetic attendance payload version '{payloadVersion}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidDataException(
                "The synthetic attendance payload is missing.");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SyntheticAttendancePayload>(
                payloadJson,
                JsonOptions);
            if (payload?.Es is null || payload.En is null
                || payload.Es.Count == 0 || payload.En.Count == 0)
            {
                throw new InvalidDataException(
                    "The synthetic attendance payload is empty.");
            }

            return payload;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The synthetic attendance payload is invalid.",
                exception);
        }
    }
}
