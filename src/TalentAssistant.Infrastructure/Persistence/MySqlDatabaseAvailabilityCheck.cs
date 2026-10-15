using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TalentAssistant.Application.Diagnostics;

namespace TalentAssistant.Infrastructure.Persistence;

public sealed class MySqlDatabaseAvailabilityCheck(
    TalentAssistantDbContext context,
    ILogger<MySqlDatabaseAvailabilityCheck> logger) : IDatabaseAvailabilityCheck
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection.State == ConnectionState.Open;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Database readiness check failed ({FailureType}).",
                exception.GetType().Name);
            return false;
        }
        finally
        {
            if (connection.State != ConnectionState.Closed)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        "Database readiness cleanup failed ({FailureType}).",
                        exception.GetType().Name);
                }
            }
        }
    }
}
