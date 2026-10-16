using System.Text.Json;
using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Infrastructure.Attendance;

public static class SyntheticAttendanceDatasetSerializer
{
    public const int CurrentPayloadVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<HcmAttendanceInsight> insights)
    {
        ArgumentNullException.ThrowIfNull(insights);
        return JsonSerializer.Serialize(insights, JsonOptions);
    }

    public static IReadOnlyList<HcmAttendanceInsight> Deserialize(
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
            var insights = JsonSerializer.Deserialize<List<HcmAttendanceInsight>>(
                payloadJson,
                JsonOptions);
            if (insights is null)
            {
                throw new InvalidDataException(
                    "The synthetic attendance payload is empty.");
            }

            return Array.AsReadOnly(insights.ToArray());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The synthetic attendance payload is invalid.",
                exception);
        }
    }
}
