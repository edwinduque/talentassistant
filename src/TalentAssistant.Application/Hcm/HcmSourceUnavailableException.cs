namespace TalentAssistant.Application.Hcm;

/// <summary>
/// Marker for "the configured HCM source cannot be reached" (SPEC-011 S3
/// degradation): the API maps it to the machine-readable 503 reason
/// <c>SourceUnavailable</c> and the Web layer localizes the copy, exactly like
/// the chat reason contract. Same shape as
/// <see cref="Attendance.AttendanceSourceUnavailableException"/>; the message is
/// always a fixed string that never carries connection-string or person values,
/// and no retry is attempted.
/// </summary>
public sealed class HcmSourceUnavailableException : Exception
{
    public HcmSourceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
