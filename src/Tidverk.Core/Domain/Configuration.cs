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
        int interfaceScalePercent = 100) {
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
