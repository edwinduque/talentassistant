namespace TalentAssistant.Application.Attendance;

public sealed class AttendanceSourceUnavailableException : Exception
{
    public AttendanceSourceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
