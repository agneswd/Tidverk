namespace Tidverk.Core;

public enum ThemePreference {
    System,
    Light,
    Dark
}

public enum MonthViewPreference {
    Ledger,
    Calendar
}

public enum LanguagePreference {
    System,
    English,
    Swedish
}

public enum CurrencyPreference {
    SEK,
    EUR,
    USD,
    GBP,
    NOK,
    DKK
}

public enum ExportLanguagePreference {
    Swedish = 0,
    English = 1,
    System = 2
}

public enum OvertimeCompensationMode {
    CompTime,
    Paid
}

public enum OvertimeDayCategory {
    AllDays,
    ScheduledWorkdays,
    NonWorkdays,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
    PublicHolidays
}

public sealed record OvertimeRateBand {
    public OvertimeRateBand(string name, OvertimeDayCategory dayCategory, TimeOnly startTime, TimeOnly endTime, decimal premiumPercent) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Rate band name is required.", nameof(name));
        }

        if (premiumPercent is < 0m or > 500m) {
            throw new ArgumentOutOfRangeException(nameof(premiumPercent), "Overtime premium must be between 0% and 500%.");
        }

        Name = name.Trim();
        DayCategory = dayCategory;
        StartTime = startTime;
        EndTime = endTime;
        PremiumPercent = premiumPercent;
    }

    public string Name { get; }

    public OvertimeDayCategory DayCategory { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    public decimal PremiumPercent { get; }

    public bool Matches(DateOnly date, TimeOnly time, bool isScheduledWorkday, bool isPublicHoliday) {
        bool dayMatches = DayCategory switch {
            OvertimeDayCategory.ScheduledWorkdays => isScheduledWorkday,
            OvertimeDayCategory.NonWorkdays => !isScheduledWorkday,
            OvertimeDayCategory.PublicHolidays => isPublicHoliday,
            OvertimeDayCategory.Monday => date.DayOfWeek == DayOfWeek.Monday,
            OvertimeDayCategory.Tuesday => date.DayOfWeek == DayOfWeek.Tuesday,
            OvertimeDayCategory.Wednesday => date.DayOfWeek == DayOfWeek.Wednesday,
            OvertimeDayCategory.Thursday => date.DayOfWeek == DayOfWeek.Thursday,
            OvertimeDayCategory.Friday => date.DayOfWeek == DayOfWeek.Friday,
            OvertimeDayCategory.Saturday => date.DayOfWeek == DayOfWeek.Saturday,
            OvertimeDayCategory.Sunday => date.DayOfWeek == DayOfWeek.Sunday,
            _ => true
        };
        bool timeMatches = StartTime == EndTime ||
            (StartTime < EndTime ? time >= StartTime && time < EndTime : time >= StartTime || time < EndTime);
        return dayMatches && timeMatches;
    }
}

public sealed record OvertimeCompensationSettings {
    public OvertimeCompensationSettings(
        OvertimeCompensationMode mode,
        decimal premiumPercent = 50m,
        decimal dailyThresholdHours = 8m,
        IEnumerable<OvertimeRateBand>? rateBands = null) {
        if (premiumPercent is < 0m or > 500m) {
            throw new ArgumentOutOfRangeException(nameof(premiumPercent), "Overtime premium must be between 0% and 500%.");
        }

        if (dailyThresholdHours <= 0m || decimal.Truncate(dailyThresholdHours * 60m) != dailyThresholdHours * 60m) {
            throw new ArgumentOutOfRangeException(nameof(dailyThresholdHours), "Daily overtime threshold must be positive and resolve to whole minutes.");
        }

        Mode = mode;
        PremiumPercent = premiumPercent;
        DailyThresholdHours = dailyThresholdHours;
        DailyThresholdMinutes = new((int)(dailyThresholdHours * 60m));
        RateBands = rateBands?.ToArray() ?? [];
    }

    public OvertimeCompensationMode Mode { get; }

    public decimal PremiumPercent { get; }

    public decimal DailyThresholdHours { get; }

    public Minutes DailyThresholdMinutes { get; }

    public IReadOnlyList<OvertimeRateBand> RateBands { get; }

    public decimal PremiumAt(DateOnly date, TimeOnly time, bool isScheduledWorkday, bool isPublicHoliday) =>
        RateBands.Where(band => band.Matches(date, time, isScheduledWorkday, isPublicHoliday))
            .Select(band => band.PremiumPercent)
            .DefaultIfEmpty(PremiumPercent)
            .Max();

    public static OvertimeCompensationSettings CompTime { get; } = new(OvertimeCompensationMode.CompTime);
}

public sealed record ExpectedHoursSettings {
    public ExpectedHoursSettings(
        decimal hoursPerWorkday,
        IEnumerable<DayOfWeek> workingWeekdays,
        bool excludePublicHolidays) {
        if (hoursPerWorkday <= 0 || decimal.Truncate(hoursPerWorkday * 60m) != hoursPerWorkday * 60m) {
            throw new ArgumentOutOfRangeException(nameof(hoursPerWorkday), "Expected hours must be positive and resolve to whole minutes.");
        }

        var weekdays = workingWeekdays?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(workingWeekdays));
        if (weekdays.Length == 0) {
            throw new ArgumentException("At least one working weekday is required.", nameof(workingWeekdays));
        }

        HoursPerWorkday = hoursPerWorkday;
        DailyMinutes = new((int)(hoursPerWorkday * 60m));
        WorkingWeekdays = weekdays;
        ExcludePublicHolidays = excludePublicHolidays;
    }

    public decimal HoursPerWorkday { get; }

    public Minutes DailyMinutes { get; }

    public IReadOnlyCollection<DayOfWeek> WorkingWeekdays { get; }

    public bool ExcludePublicHolidays { get; }

    public bool IsExpectedWeekday(DateOnly date) => WorkingWeekdays.Contains(date.DayOfWeek);

    public static ExpectedHoursSettings Standard { get; } = new(
        8m,
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        true);
}

public sealed record MonthRecord {
    public MonthRecord(int year, int month, int openingBalanceMinutes = 0, int? expectedMinutesOverride = null, bool openingBalanceWasEdited = false) {
        _ = new DateOnly(year, month, 1);
        if (expectedMinutesOverride < 0) {
            throw new ArgumentOutOfRangeException(nameof(expectedMinutesOverride));
        }

        Year = year;
        Month = month;
        OpeningBalanceMinutes = openingBalanceMinutes;
        ExpectedMinutesOverride = expectedMinutesOverride;
        OpeningBalanceWasEdited = openingBalanceWasEdited;
    }

    public int Year { get; }

    public int Month { get; }

    public int OpeningBalanceMinutes { get; }

    public int? ExpectedMinutesOverride { get; }

    public bool OpeningBalanceWasEdited { get; }
}

public sealed record Project {
    public Project(Guid id, string name, bool isActive = true, bool isDefault = false) {
        if (id == Guid.Empty) {
            throw new ArgumentException("Project id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        IsActive = isActive;
        IsDefault = isDefault;
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsActive { get; }

    public bool IsDefault { get; }
}

public sealed record AppSettings {
    public AppSettings(
        string employeeName,
        string employerName,
        string defaultProject,
        HourlySalary hourlySalary,
        ExpectedHoursSettings expectedHours,
        TimeOnly defaultStartTime,
        TimeOnly defaultEndTime,
        Minutes defaultLunchMinutes,
        TaxSettings taxSettings,
        ThemePreference themePreference = ThemePreference.System,
        int openingBalanceMinutes = 0,
        MonthViewPreference monthViewPreference = MonthViewPreference.Ledger,
        LanguagePreference languagePreference = LanguagePreference.System,
        CurrencyPreference currencyPreference = CurrencyPreference.SEK,
        int interfaceScalePercent = 100,
        ExportLanguagePreference exportLanguagePreference = ExportLanguagePreference.Swedish,
        OvertimeCompensationSettings? overtimeCompensation = null) {
        EmployeeName = employeeName?.Trim() ?? throw new ArgumentNullException(nameof(employeeName));
        EmployerName = employerName?.Trim() ?? throw new ArgumentNullException(nameof(employerName));
        DefaultProject = defaultProject?.Trim() ?? throw new ArgumentNullException(nameof(defaultProject));
        HourlySalary = hourlySalary;
        ExpectedHours = expectedHours ?? throw new ArgumentNullException(nameof(expectedHours));
        if (defaultEndTime <= defaultStartTime) {
            throw new ArgumentException("Default end time must be later than default start time.", nameof(defaultEndTime));
        }

        DefaultStartTime = defaultStartTime;
        DefaultEndTime = defaultEndTime;
        DefaultLunchMinutes = defaultLunchMinutes;
        TaxSettings = taxSettings ?? throw new ArgumentNullException(nameof(taxSettings));
        ThemePreference = themePreference;
        OpeningBalanceMinutes = openingBalanceMinutes;
        MonthViewPreference = monthViewPreference;
        LanguagePreference = languagePreference;
        CurrencyPreference = currencyPreference;
        ExportLanguagePreference = exportLanguagePreference;
        OvertimeCompensation = overtimeCompensation ?? OvertimeCompensationSettings.CompTime;
        if (interfaceScalePercent is < 80 or > 150) {
            throw new ArgumentOutOfRangeException(nameof(interfaceScalePercent), "Interface scale must be between 80% and 150%.");
        }

        InterfaceScalePercent = interfaceScalePercent;
    }

    public string EmployeeName { get; }

    public string EmployerName { get; }

    public string DefaultProject { get; }

    public HourlySalary HourlySalary { get; }

    public decimal HourlyRate => HourlySalary.Amount;

    public ExpectedHoursSettings ExpectedHours { get; }

    public TimeOnly DefaultStartTime { get; }

    public TimeOnly DefaultEndTime { get; }

    public Minutes DefaultLunchMinutes { get; }

    public TaxSettings TaxSettings { get; }

    public ThemePreference ThemePreference { get; }

    public int OpeningBalanceMinutes { get; }

    public MonthViewPreference MonthViewPreference { get; }

    public LanguagePreference LanguagePreference { get; }

    public CurrencyPreference CurrencyPreference { get; }

    public ExportLanguagePreference ExportLanguagePreference { get; }

    public OvertimeCompensationSettings OvertimeCompensation { get; }

    public int InterfaceScalePercent { get; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(EmployeeName) &&
        !string.IsNullOrWhiteSpace(EmployerName) &&
        !string.IsNullOrWhiteSpace(DefaultProject);

    public static AppSettings Unconfigured { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        new HourlySalary(0m),
        ExpectedHoursSettings.Standard,
        new TimeOnly(8, 0),
        new TimeOnly(16, 30),
        new Minutes(30),
        TaxSettings.Disabled);
}
