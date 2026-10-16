using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.Infrastructure.Attendance;

public sealed class MySqlHcmAttendanceSource(TalentAssistantDbContext dbContext)
    : IHcmAttendanceSource
{
    public const string DatasetId = "attendance-demo";

    public async Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (query.FromInclusive >= query.ToExclusive)
        {
            throw new ArgumentException(
                "The start date must be earlier than the exclusive end date.",
                nameof(query));
        }

        SyntheticAttendanceDataset? dataset;
        try
        {
            dataset = await dbContext.SyntheticAttendanceDatasets
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.DatasetId == DatasetId,
                    cancellationToken);
        }
        catch (DbException exception)
        {
            throw new AttendanceSourceUnavailableException(
                "The synthetic attendance source is unavailable.",
                exception);
        }

        if (dataset is null)
        {
            throw new AttendanceSourceUnavailableException(
                "The synthetic attendance dataset has not been initialized.");
        }

        IReadOnlyList<HcmAttendanceInsight> insights;
        try
        {
            insights = SyntheticAttendanceDatasetSerializer.Deserialize(
                dataset.PayloadVersion,
                dataset.PayloadJson);
        }
        catch (InvalidDataException exception)
        {
            throw new AttendanceSourceUnavailableException(
                "The synthetic attendance dataset could not be read.",
                exception);
        }

        return SyntheticHcmAttendanceSource.Filter(insights, query);
    }
}
