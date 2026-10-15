namespace TalentAssistant.Application.Attendance;

public sealed class GetAttendanceInsights(IHcmAttendanceSource attendanceSource)
{
    public async Task<IReadOnlyList<HcmAttendanceInsight>> ExecuteAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FromInclusive >= query.ToExclusive)
        {
            throw new ArgumentException(
                "The start date must be earlier than the exclusive end date.",
                nameof(query));
        }

        return await attendanceSource.GetAttendanceInsightsAsync(query, cancellationToken);
    }
}
