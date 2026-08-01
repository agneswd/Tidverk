using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

/// <summary>
/// The stored shape of the four SQLite tables. These types exist only to carry rows: the domain
/// rules live in <see cref="Tidverk.Core"/>, and the repositories translate between the two.
/// Property names and defaults match the migrations and must not drift from them.
/// </summary>
public sealed class WorkEntryEntity {
    public DateOnly Date { get; set; }

    public WorkEntryStatus Status { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int LunchMinutes { get; set; }

    public string? ProjectName { get; set; }

    public string? Notes { get; set; }

    public int? ScheduledMinutesOverride { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A single row holding every preference, keyed by <see cref="Id"/> 1.</summary>
public sealed class AppSettingsEntity {
    public int Id { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string EmployerName { get; set; } = string.Empty;

    public string DefaultProject { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public SalaryType SalaryType { get; set; }

    public decimal MonthlySalary { get; set; }

    public decimal EmploymentPercent { get; set; } = 100m;

    public decimal ExpectedHoursPerWorkday { get; set; } = 8m;

    /// <summary>Comma-separated <see cref="DayOfWeek"/> ordinals, for example "1,2,3,4,5".</summary>
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

    public OvertimeThresholdMode OvertimeThresholdMode { get; set; }

    public CompensationRateType OvertimeDefaultRateType { get; set; }

    /// <summary>A serialized <c>OvertimeRateBand[]</c>; rate bands vary in count and have no table of their own.</summary>
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
