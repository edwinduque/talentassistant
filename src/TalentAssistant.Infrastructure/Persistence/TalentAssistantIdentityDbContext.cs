using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TalentAssistant.Infrastructure.Persistence;

public sealed class TalentAssistantIdentityDbContext(
    DbContextOptions<TalentAssistantIdentityDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            // A database default keeps the migration applicable to populated tables.
            entity.Property(user => user.FullName).HasMaxLength(100);
            entity.Property(user => user.PreferredLanguage)
                .HasMaxLength(5)
                .HasDefaultValue(ApplicationUser.DefaultPreferredLanguage);
        });
    }
}
