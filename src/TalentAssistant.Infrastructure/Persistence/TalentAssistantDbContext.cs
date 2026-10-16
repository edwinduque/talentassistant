using Microsoft.EntityFrameworkCore;

namespace TalentAssistant.Infrastructure.Persistence;

public sealed class TalentAssistantDbContext(
    DbContextOptions<TalentAssistantDbContext> options) : DbContext(options)
{
    public DbSet<SyntheticAttendanceDataset> SyntheticAttendanceDatasets =>
        Set<SyntheticAttendanceDataset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SyntheticAttendanceDataset>(entity =>
        {
            entity.ToTable("SyntheticAttendanceDatasets");
            entity.HasKey(dataset => dataset.DatasetId);
            entity.Property(dataset => dataset.DatasetId)
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(dataset => dataset.PayloadVersion)
                .IsRequired();
            entity.Property(dataset => dataset.PayloadJson)
                .HasColumnType("longtext")
                .IsRequired();
        });
    }
}
