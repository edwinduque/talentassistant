using Microsoft.EntityFrameworkCore;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.Infrastructure.Attendance;

public sealed class InitializeLocalSyntheticAttendanceData(
    TalentAssistantDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var payloadJson = SyntheticAttendanceDatasetSerializer.Serialize(
            SyntheticHcmAttendanceSource.FixturesFor(SyntheticHcmAttendanceSource.Spanish),
            SyntheticHcmAttendanceSource.FixturesFor(SyntheticHcmAttendanceSource.English));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var dataset = await dbContext.SyntheticAttendanceDatasets
            .SingleOrDefaultAsync(
                item => item.DatasetId == MySqlHcmAttendanceSource.DatasetId,
                cancellationToken);

        if (dataset is null)
        {
            dbContext.SyntheticAttendanceDatasets.Add(new SyntheticAttendanceDataset
            {
                DatasetId = MySqlHcmAttendanceSource.DatasetId,
                PayloadVersion = SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion,
                PayloadJson = payloadJson
            });
        }
        else
        {
            dataset.PayloadVersion = SyntheticAttendanceDatasetSerializer.CurrentPayloadVersion;
            dataset.PayloadJson = payloadJson;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
