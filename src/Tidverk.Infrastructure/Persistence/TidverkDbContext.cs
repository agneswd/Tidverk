using Microsoft.EntityFrameworkCore;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

public sealed class TidverkDbContext(DbContextOptions<TidverkDbContext> options) : DbContext(options) {
    public DbSet<WorkEntryEntity> WorkEntries => Set<WorkEntryEntity>();

    public DbSet<AppSettingsEntity> Settings => Set<AppSettingsEntity>();

    public DbSet<MonthEntity> Months => Set<MonthEntity>();

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<WorkEntryEntity>(entity => {
            entity.HasKey(item => item.Date);
            entity.Property(item => item.ProjectName).HasMaxLength(160);
            entity.Property(item => item.Notes).HasMaxLength(2_000);
        });
        modelBuilder.Entity<AppSettingsEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<MonthEntity>().HasKey(item => new { item.Year, item.Month });
        modelBuilder.Entity<ProjectEntity>(entity => {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160);
        });
    }
}

public sealed class WorkEntryEntity {
    public DateOnly Date { get; set; }

    public WorkEntryStatus Status { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int LunchMinutes { get; set; }

    public string? ProjectName { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AppSettingsEntity {
    public int Id { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string EmployerName { get; set; } = string.Empty;

    public string DefaultProject { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public decimal ExpectedHoursPerWorkday { get; set; } = 8m;

    public string ExpectedWorkingWeekdays { get; set; } = "1,2,3,4,5";

    public bool ExcludePublicHolidays { get; set; } = true;

    public TimeOnly DefaultStartTime { get; set; } = new(8, 0);

    public TimeOnly DefaultEndTime { get; set; } = new(16, 30);

    public int DefaultLunchMinutes { get; set; } = 30;

    public ThemePreference ThemePreference { get; set; }

    public MonthViewPreference MonthViewPreference { get; set; }

    public TaxMode TaxMode { get; set; }

    public int TaxYear { get; set; }

    public int TaxTableNumber { get; set; }

    public int TaxColumn { get; set; }

    public decimal? ManualTaxValue { get; set; }

    public int OpeningBalanceMinutes { get; set; }

    public LanguagePreference LanguagePreference { get; set; }

    public CurrencyPreference CurrencyPreference { get; set; }

    public int InterfaceScalePercent { get; set; } = 100;

    public ExportLanguagePreference ExportLanguagePreference { get; set; }

    public OvertimeCompensationMode OvertimeCompensationMode { get; set; }

    public decimal OvertimePremiumPercent { get; set; } = 50m;

    public decimal OvertimeDailyThresholdHours { get; set; } = 8m;

    public string OvertimeRateBandsJson { get; set; } = "[]";
}

public sealed class MonthEntity {
    public int Year { get; set; }

    public int Month { get; set; }

    public int OpeningBalanceMinutes { get; set; }

    public int? ExpectedMinutesOverride { get; set; }

    public bool OpeningBalanceWasEdited { get; set; }
}

public sealed class ProjectEntity {
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }
}
