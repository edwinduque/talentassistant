using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TalentAssistant.Infrastructure.Persistence;

/// <summary>
/// Design-time factories for the two application-owned contexts. They keep
/// <c>dotnet ef migrations add</c> independent of the API host startup
/// (which demands authentication configuration) and pin the PostgreSQL
/// provider for the generated migrations. The connection string is a
/// placeholder: adding a migration never connects to a database.
/// </summary>
public sealed class TalentAssistantDbContextFactory
    : IDesignTimeDbContextFactory<TalentAssistantDbContext>
{
    public TalentAssistantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TalentAssistantDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time-placeholder");
        return new TalentAssistantDbContext(optionsBuilder.Options);
    }
}

public sealed class TalentAssistantIdentityDbContextFactory
    : IDesignTimeDbContextFactory<TalentAssistantIdentityDbContext>
{
    public TalentAssistantIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TalentAssistantIdentityDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time-placeholder");
        return new TalentAssistantIdentityDbContext(optionsBuilder.Options);
    }
}
