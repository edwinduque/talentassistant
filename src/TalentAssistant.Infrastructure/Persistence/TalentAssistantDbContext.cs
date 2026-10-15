using Microsoft.EntityFrameworkCore;

namespace TalentAssistant.Infrastructure.Persistence;

public sealed class TalentAssistantDbContext(
    DbContextOptions<TalentAssistantDbContext> options) : DbContext(options);
