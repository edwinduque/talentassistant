namespace TalentAssistant.Infrastructure.Persistence;

public sealed class SyntheticAttendanceDataset
{
    public string DatasetId { get; set; } = string.Empty;

    public int PayloadVersion { get; set; }

    public string PayloadJson { get; set; } = string.Empty;
}
