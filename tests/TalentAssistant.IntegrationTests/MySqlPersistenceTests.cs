using Microsoft.EntityFrameworkCore;

namespace TalentAssistant.IntegrationTests;

public sealed class MySqlPersistenceTests
{
    [Fact]
    public async Task AppliesMigrationsAndRoundTripsSyntheticDataOnMySql8411()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");

        var options = new DbContextOptionsBuilder<PersistenceProbeContext>()
            .UseMySQL(connectionString!)
            .Options;

        await using var context = new PersistenceProbeContext(options);
        await context.Database.MigrateAsync();

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT VERSION()";
        var serverVersion = Convert.ToString(await versionCommand.ExecuteScalarAsync());

        Assert.StartsWith("8.4.11", serverVersion, StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        await using var transaction = await context.Database.BeginTransactionAsync();
        var key = $"integration-{Guid.NewGuid():N}";
        var record = new PersistenceProbe { Key = key };
        context.Records.Add(record);
        await context.SaveChangesAsync();

        var persisted = await context.Records
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Key == key);

        Assert.True(persisted.Id > 0);
        Assert.Equal(key, persisted.Key);
        await transaction.RollbackAsync();
    }
}
