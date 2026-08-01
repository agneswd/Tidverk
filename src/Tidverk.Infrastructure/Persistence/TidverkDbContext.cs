using Microsoft.EntityFrameworkCore;

namespace Tidverk.Infrastructure.Persistence;

public sealed class TidverkDbContext(DbContextOptions<TidverkDbContext> options) : DbContext(options) {
    internal const int ProjectNameMaxLength = 160;
    internal const int NotesMaxLength = 2_000;

    public DbSet<WorkEntryEntity> WorkEntries => Set<WorkEntryEntity>();

    public DbSet<AppSettingsEntity> Settings => Set<AppSettingsEntity>();

    public DbSet<MonthEntity> Months => Set<MonthEntity>();

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<WorkEntryEntity>(entity => {
            entity.HasKey(item => item.Date);
            entity.Property(item => item.ProjectName).HasMaxLength(ProjectNameMaxLength);
            entity.Property(item => item.Notes).HasMaxLength(NotesMaxLength);
        });
        modelBuilder.Entity<AppSettingsEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<MonthEntity>().HasKey(item => new { item.Year, item.Month });
        modelBuilder.Entity<ProjectEntity>(entity => {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(ProjectNameMaxLength);
        });
    }
}
