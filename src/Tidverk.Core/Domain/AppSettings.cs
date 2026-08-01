namespace Tidverk.Core;

/// <summary>Every user preference the application persists, validated as a whole.</summary>
public sealed record AppSettings {
    private const int MinimumInterfaceScalePercent = 80;
    private const int MaximumInterfaceScalePercent = 150;

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
        ExportLanguagePreference exportLanguagePreference = ExportLanguagePreference.System,
        OvertimeCompensationSettings? overtimeCompensation = null,
        SalarySettings? salarySettings = null) {
        ArgumentNullException.ThrowIfNull(employeeName);
        ArgumentNullException.ThrowIfNull(employerName);
        ArgumentNullException.ThrowIfNull(defaultProject);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ArgumentNullException.ThrowIfNull(taxSettings);
        if (defaultEndTime <= defaultStartTime) {
            throw new ArgumentException("Default end time must be later than default start time.", nameof(defaultEndTime));
        }

        if (interfaceScalePercent is < MinimumInterfaceScalePercent or > MaximumInterfaceScalePercent) {
            throw new ArgumentOutOfRangeException(
                nameof(interfaceScalePercent),
                $"Interface scale must be between {MinimumInterfaceScalePercent}% and {MaximumInterfaceScalePercent}%.");
        }

        EmployeeName = employeeName.Trim();
        EmployerName = employerName.Trim();
        DefaultProject = defaultProject.Trim();
        Salary = salarySettings ?? SalarySettings.Hourly(hourlySalary);
        ExpectedHours = expectedHours;
        DefaultStartTime = defaultStartTime;
        DefaultEndTime = defaultEndTime;
        DefaultLunchMinutes = defaultLunchMinutes;
        TaxSettings = taxSettings;
        ThemePreference = themePreference;
        OpeningBalanceMinutes = openingBalanceMinutes;
        MonthViewPreference = monthViewPreference;
        LanguagePreference = languagePreference;
        CurrencyPreference = currencyPreference;
        InterfaceScalePercent = interfaceScalePercent;
        ExportLanguagePreference = exportLanguagePreference;
        OvertimeCompensation = overtimeCompensation ?? OvertimeCompensationSettings.CompTime;
        ValidateCompensationRates(Salary, OvertimeCompensation);
    }

    public string EmployeeName { get; }

    public string EmployerName { get; }

    public string DefaultProject { get; }

    public SalarySettings Salary { get; }

    public HourlySalary HourlySalary => Salary.HourlySalary;

    public decimal HourlyRate => HourlySalary.Amount;

    public ExpectedHoursSettings ExpectedHours { get; }

    public TimeOnly DefaultStartTime { get; }

    public TimeOnly DefaultEndTime { get; }

    public Minutes DefaultLunchMinutes { get; }

    public TaxSettings TaxSettings { get; }

    public ThemePreference ThemePreference { get; }

    /// <summary>The time balance the user brought into the application before the first tracked month.</summary>
    public int OpeningBalanceMinutes { get; }

    public MonthViewPreference MonthViewPreference { get; }

    public LanguagePreference LanguagePreference { get; }

    public CurrencyPreference CurrencyPreference { get; }

    public ExportLanguagePreference ExportLanguagePreference { get; }

    public OvertimeCompensationSettings OvertimeCompensation { get; }

    public int InterfaceScalePercent { get; }

    /// <summary>False until first-run setup has been completed, which is what opens the setup dialog.</summary>
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

    private static void ValidateCompensationRates(SalarySettings salary, OvertimeCompensationSettings compensation) {
        IEnumerable<CompensationRateType> rateTypes = compensation.RateBands.Select(rule => rule.RateType);
        if (compensation.Mode == OvertimeCompensationMode.Paid) {
            rateTypes = rateTypes.Append(compensation.DefaultRateType);
        }

        if (salary.Type == SalaryType.Hourly && rateTypes.Contains(CompensationRateType.FullTimeMonthlySalaryDivisor)) {
            throw new ArgumentException("Full-time monthly-salary divisor rules require monthly salary.", nameof(compensation));
        }

        if (salary.Type == SalaryType.Monthly && rateTypes.Contains(CompensationRateType.HourlyPremiumPercent)) {
            throw new ArgumentException("Hourly percentage rules require hourly wage; use a fixed amount or monthly-salary divisor.", nameof(compensation));
        }
    }
}
