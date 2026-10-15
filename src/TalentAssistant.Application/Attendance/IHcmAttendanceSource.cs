namespace TalentAssistant.Application.Attendance;

public interface IHcmAttendanceSource
{
    Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default);
}
