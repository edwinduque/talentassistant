using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TalentAssistant.IntegrationTests;

public sealed class PersistenceProbe
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;
}

public sealed class PersistenceProbeContext(
    DbContextOptions<PersistenceProbeContext> options) : DbContext(options)
{
    public DbSet<PersistenceProbe> Records => Set<PersistenceProbe>();
}

public sealed class PersistenceProbeContextFactory
    : IDesignTimeDbContextFactory<PersistenceProbeContext>
{
    public PersistenceProbeContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__TalentAssistantTests before creating or applying test migrations.");
        }

        var options = new DbContextOptionsBuilder<PersistenceProbeContext>()
            .UseMySQL(connectionString)
            .Options;

        return new PersistenceProbeContext(options);
    }
}
