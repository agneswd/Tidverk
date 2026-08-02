using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>
/// The settings form. Its properties are the edit buffer: they are filled from the stored settings
/// when the page opens and only become <see cref="AppSettings"/> when the user saves.
/// </summary>
public sealed partial class MainWindowViewModel {
    /// <summary>Sensible starting points for a user who has not chosen a tax table yet.</summary>
    private const int DefaultTaxYear = 2026;
    private const int DefaultTaxTable = 33;
    private const int DefaultTaxColumn = 1;

    private bool isSetupOpen;
    private bool isBalanceAdjustmentOpen;
    private bool isCurrencyRatePromptOpen;
    private string settingsStatus = string.Empty;
    private bool isLoadingSettingsForm;

    [ObservableProperty]
    private string employeeName = string.Empty;

    [ObservableProperty]
    private string employerName = string.Empty;

    [ObservableProperty]
    private string defaultProject = string.Empty;

    [ObservableProperty]
    private decimal hourlyRate = 202m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHourlySalary))]
    [NotifyPropertyChangedFor(nameof(IsMonthlySalary))]
    [NotifyPropertyChangedFor(nameof(GrossPayNote))]
    [NotifyPropertyChangedFor(nameof(CompensationRateTypes))]
    private SalaryType selectedSalaryType;

    [ObservableProperty]
    private decimal monthlySalary = 25_000m;

    [ObservableProperty]
    private decimal employmentPercent = 100m;

    [ObservableProperty]
    private decimal expectedHoursPerDay = 8m;

    [ObservableProperty]
    private string defaultStart = "08:00";

    [ObservableProperty]
    private string defaultEnd = "16:30";

    [ObservableProperty]
    private int defaultLunch = 30;

    [ObservableProperty]
    private int openingBalanceMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryIncomeTax))]
    [NotifyPropertyChangedFor(nameof(IsManualTax))]
    private TaxMode selectedTaxMode;

    [ObservableProperty]
    private int taxYear = DefaultTaxYear;

    [ObservableProperty]
    private int taxTableNumber = DefaultTaxTable;

    [ObservableProperty]
    private int taxColumn = DefaultTaxColumn;

    [ObservableProperty]
    private decimal manualTaxValue;

    [ObservableProperty]
    private ThemePreference selectedTheme;

    [ObservableProperty]
    private LanguagePreference selectedLanguage;

    [ObservableProperty]
    private ExportLanguagePreference selectedExportLanguage;

    [ObservableProperty]
    private CurrencyPreference selectedCurrency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaidOvertime))]
    [NotifyPropertyChangedFor(nameof(OvertimeCompensationDescription))]
    [NotifyPropertyChangedFor(nameof(GrossPayNote))]
    [NotifyPropertyChangedFor(nameof(TimeBalanceTitle))]
    [NotifyPropertyChangedFor(nameof(TimeBalanceDescription))]
    [NotifyPropertyChangedFor(nameof(ShowsOvertimeRuleActions))]
    [NotifyPropertyChangedFor(nameof(ShowsCompTimeRuleNote))]
    private OvertimeCompensationMode selectedOvertimeMode;

    [ObservableProperty]
    private decimal overtimePremiumPercent = 50m;

    [ObservableProperty]
    private decimal overtimeDailyThresholdHours = 8m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixedOvertimeThreshold))]
    private OvertimeThresholdMode selectedOvertimeThresholdMode;

    [ObservableProperty]
    private CompensationRateType selectedOvertimeDefaultRateType;

    [ObservableProperty]
    private ObOvertimeCombinationMode selectedObOvertimeCombination;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InterfaceScale))]
    private int selectedInterfaceScale = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthlyOpeningBalanceHours))]
    private int monthlyOpeningBalance;

    [ObservableProperty]
    private decimal balanceAdjustmentHours;

    [ObservableProperty]
    private bool workMonday = true;

    [ObservableProperty]
    private bool workTuesday = true;

    [ObservableProperty]
    private bool workWednesday = true;

    [ObservableProperty]
    private bool workThursday = true;

    [ObservableProperty]
    private bool workFriday = true;

    [ObservableProperty]
    private bool workSaturday;

    [ObservableProperty]
    private bool workSunday;

    [ObservableProperty]
    private bool excludePublicHolidays = true;

    public ObservableCollection<OvertimeRateBandViewModel> OvertimeRateBands { get; } = [];

    public IReadOnlyList<TaxMode> TaxModes { get; } = Enum.GetValues<TaxMode>();

    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();

    public IReadOnlyList<LanguagePreference> LanguagePreferences { get; } = Enum.GetValues<LanguagePreference>();

    public IReadOnlyList<ExportLanguagePreference> ExportLanguagePreferences { get; } = [
        ExportLanguagePreference.System,
        ExportLanguagePreference.English,
        ExportLanguagePreference.Swedish
    ];

    public IReadOnlyList<CurrencyPreference> CurrencyPreferences { get; } = Enum.GetValues<CurrencyPreference>();

    public IReadOnlyList<SalaryType> SalaryTypes { get; } = Enum.GetValues<SalaryType>();

    public IReadOnlyList<OvertimeCompensationMode> OvertimeCompensationModes { get; } = Enum.GetValues<OvertimeCompensationMode>();

    public IReadOnlyList<OvertimeDayCategory> OvertimeDayCategories { get; } = Enum.GetValues<OvertimeDayCategory>();

    public IReadOnlyList<OvertimeThresholdMode> OvertimeThresholdModes { get; } = Enum.GetValues<OvertimeThresholdMode>();

    public IReadOnlyList<ObOvertimeCombinationMode> ObOvertimeCombinationModes { get; } = Enum.GetValues<ObOvertimeCombinationMode>();

    public IReadOnlyList<CompensationRuleType> CompensationRuleTypes { get; } = Enum.GetValues<CompensationRuleType>();

    /// <summary>
    /// Only the bases that can be priced with the chosen salary type. A divisor rule needs a monthly
    /// salary and an hourly percentage needs an hourly wage, so offering both would let the user build
    /// a rule that pays nothing.
    /// </summary>
    public IReadOnlyList<CompensationRateType> CompensationRateTypes => SelectedSalaryType == SalaryType.Monthly
        ? [CompensationRateType.FixedHourlyAmount, CompensationRateType.FullTimeMonthlySalaryDivisor]
        : [CompensationRateType.HourlyPremiumPercent, CompensationRateType.FixedHourlyAmount];

    public IReadOnlyList<int> InterfaceScaleOptions { get; } = [80, 90, 100, 110, 125, 150];

    public bool IsSetupOpen { get => isSetupOpen; private set => SetProperty(ref isSetupOpen, value); }

    public bool IsCurrencyRatePromptOpen { get => isCurrencyRatePromptOpen; private set => SetProperty(ref isCurrencyRatePromptOpen, value); }

    public bool IsBalanceAdjustmentOpen { get => isBalanceAdjustmentOpen; private set => SetProperty(ref isBalanceAdjustmentOpen, value); }

    public SettingsSection CurrentSettingsSection {
        get => settingsSection;
        private set {
            if (SetProperty(ref settingsSection, value)) {
                OnPropertyChanged(nameof(IsEmploymentSettings));
                OnPropertyChanged(nameof(IsAppearanceSettings));
                OnPropertyChanged(nameof(IsDataSettings));
            }
        }
    }

    public bool IsEmploymentSettings => CurrentSettingsSection == SettingsSection.Employment;

    public bool IsAppearanceSettings => CurrentSettingsSection == SettingsSection.Appearance;

    public bool IsDataSettings => CurrentSettingsSection == SettingsSection.Data;

    public bool IsPaidOvertime => SelectedOvertimeMode == OvertimeCompensationMode.Paid;

    public bool IsHourlySalary => SelectedSalaryType == SalaryType.Hourly;

    public bool IsMonthlySalary => SelectedSalaryType == SalaryType.Monthly;

    public bool IsFixedOvertimeThreshold => SelectedOvertimeThresholdMode == OvertimeThresholdMode.FixedDailyHours;

    /// <summary>Overtime rules only price anything when overtime is paid; OB rules apply either way.</summary>
    public bool ShowsOvertimeRuleActions => IsPaidOvertime;

    public bool ShowsCompTimeRuleNote => !IsPaidOvertime &&
        OvertimeRateBands.Any(rule => rule.CompensationType == CompensationRuleType.Overtime);

    /// <summary>Table, year and column only mean anything for the Skatteverket table mode.</summary>
    public bool IsPrimaryIncomeTax => SelectedTaxMode == TaxMode.PrimaryIncomeTaxTable;

    public bool IsManualTax => SelectedTaxMode == TaxMode.ManualMonthlyDeduction;

    public string OvertimeCompensationDescription => IsPaidOvertime
        ? localization.Get("OvertimePaidDescription")
        : localization.Get("OvertimeCompTimeDescription");

    public string CurrencyChangeText => localization.Format("CurrencyChangeSummary", settings.CurrencyPreference, SelectedCurrency);

    public double InterfaceScale => SelectedInterfaceScale / 100d;

    public decimal MonthlyOpeningBalanceHours {
        get => MonthlyOpeningBalance / 60m;
        set => MonthlyOpeningBalance = decimal.ToInt32(decimal.Round(value * 60m, MidpointRounding.AwayFromZero));
    }

    public string BalanceAdjustmentDescription => localization.Format("BalanceAdjustDescription", MonthTitle);

    public string SettingsStatus {
        get => settingsStatus;
        private set {
            if (SetProperty(ref settingsStatus, value)) {
                OnPropertyChanged(nameof(HasSettingsStatus));
            }
        }
    }

    public bool HasSettingsStatus => !string.IsNullOrWhiteSpace(SettingsStatus);

    [RelayCommand]
    private void OpenSettings() {
        CopySettingsToForm();
        SettingsStatus = string.Empty;
        CurrentPage = settingsPage;
        CurrentSettingsSection = SettingsSection.Employment;

        // Checking the sidebar's settings item unchecks its siblings, and the section is usually
        // already Employment, so its setter raises nothing. Re-check it explicitly.
        OnPropertyChanged(nameof(IsEmploymentSettings));
    }

    /// <summary>Leaving the page discards unsaved edits by refilling the form from stored settings.</summary>
    [RelayCommand]
    private void CloseSettings() {
        CopySettingsToForm();
        CurrentPage = monthWorkspacePage;
    }

    [RelayCommand]
    private void ShowEmploymentSettings() => CurrentSettingsSection = SettingsSection.Employment;

    [RelayCommand]
    private void ShowAppearanceSettings() => CurrentSettingsSection = SettingsSection.Appearance;

    [RelayCommand]
    private void ShowDataSettings() => CurrentSettingsSection = SettingsSection.Data;

    [RelayCommand]
    private void AddOvertimeRateBand() {
        OvertimeRateBands.Add(new OvertimeRateBandViewModel {
            Name = localization.Get("OvertimeRateBandDefaultName"),
            CompensationType = CompensationRuleType.Overtime,
            RateType = SelectedOvertimeDefaultRateType,
            RateValue = OvertimePremiumPercent
        });
        OnPropertyChanged(nameof(ShowsCompTimeRuleNote));
    }

    [RelayCommand]
    private void AddObRateBand() =>
        OvertimeRateBands.Add(new OvertimeRateBandViewModel {
            Name = localization.Get("ObRateBandDefaultName"),
            CompensationType = CompensationRuleType.Ob,
            RateType = SelectedSalaryType == SalaryType.Monthly
                ? CompensationRateType.FullTimeMonthlySalaryDivisor
                : CompensationRateType.FixedHourlyAmount,
            RateValue = SelectedSalaryType == SalaryType.Monthly ? 400m : 0m
        });

    [RelayCommand]
    private void RemoveOvertimeRateBand(OvertimeRateBandViewModel? band) {
        if (band is not null) {
            OvertimeRateBands.Remove(band);
            OnPropertyChanged(nameof(ShowsCompTimeRuleNote));
        }
    }

    /// <summary>
    /// Changing the salary type can strand rate bases. Keep their values intact and require the user
    /// to choose how to reinterpret them before saving.
    /// </summary>
    partial void OnSelectedSalaryTypeChanged(SalaryType value) {
        // Filling the form from stored settings assigns the salary type before the rules it belongs
        // with, so coercing here would judge the previous month's rules and report a change the user
        // never made.
        if (isLoadingSettingsForm) {
            return;
        }

        IReadOnlyList<CompensationRateType> allowed = CompensationRateTypes;
        bool needsReview = OvertimeRateBands.Any(rule => !allowed.Contains(rule.RateType)) ||
            (IsPaidOvertime && !allowed.Contains(SelectedOvertimeDefaultRateType));
        if (needsReview) {
            SettingsStatus = string.Empty;
            ErrorText = localization.Get("RateBasisReviewRequired");
        }
    }

    /// <summary>A currency change leaves the hourly rate untouched, so the user is asked to confirm it first.</summary>
    [RelayCommand]
    private Task SaveSettingsAsync() {
        if (ValidateForm() is string problem) {
            SettingsStatus = string.Empty;
            ErrorText = localization.Get(problem);
            return Task.CompletedTask;
        }

        if (settings.IsConfigured && SelectedCurrency != settings.CurrencyPreference) {
            OnPropertyChanged(nameof(CurrencyChangeText));
            IsCurrencyRatePromptOpen = true;
            return Task.CompletedTask;
        }

        return PersistSettingsAsync();
    }

    /// <summary>
    /// Checks the form against the domain's rules and returns the resource key describing the first
    /// problem, or null when it is sound. Doing this here keeps the domain's own English exception
    /// messages out of the interface.
    /// </summary>
    private string? ValidateForm() {
        if (ValidateIdentity() is string identityProblem) {
            return identityProblem;
        }

        if (!TimeInput.TryNormalize(DefaultStart, out string start) ||
            !TimeInput.TryNormalize(DefaultEnd, out string end) ||
            string.Equals(start, end, StringComparison.Ordinal)) {
            return "InvalidDefaultTimes";
        }

        if (DefaultLunch < 0 || DefaultLunch >= MinuteMath.Elapsed(TimeInput.Parse(start), TimeInput.Parse(end))) {
            return "ValidLunchRequired";
        }

        if (ExpectedHoursPerDay <= 0m || decimal.Truncate(ExpectedHoursPerDay * 60m) != ExpectedHoursPerDay * 60m) {
            return "ValidHoursPerWorkdayRequired";
        }

        if (SelectedWeekdays().Length == 0) {
            return "WorkWeekRequired";
        }

        if (HourlyRate < 0m) {
            return "ValidHourlyRateRequired";
        }

        if (SelectedSalaryType == SalaryType.Monthly && MonthlySalary <= 0m) {
            return "ValidMonthlySalaryRequired";
        }

        if (EmploymentPercent is <= 0m or > 100m) {
            return "ValidEmploymentPercentRequired";
        }

        if (OvertimeDailyThresholdHours < 0m ||
            decimal.Truncate(OvertimeDailyThresholdHours * 60m) != OvertimeDailyThresholdHours * 60m) {
            return "ValidOvertimeThresholdRequired";
        }

        if (OvertimeRateBands.Any(rule => !rule.HasValidTimes)) {
            return "RuleTimeInvalid";
        }

        if (OvertimeRateBands.Any(rule => !IsValidRateValue(rule.RateType, rule.RateValue)) ||
            (IsPaidOvertime && !IsValidRateValue(SelectedOvertimeDefaultRateType, OvertimePremiumPercent))) {
            return "RuleRateInvalid";
        }

        IReadOnlyList<CompensationRateType> allowed = CompensationRateTypes;
        if (OvertimeRateBands.Any(rule => !allowed.Contains(rule.RateType)) ||
            (IsPaidOvertime && !allowed.Contains(SelectedOvertimeDefaultRateType))) {
            return "RateBasisIncompatible";
        }

        return null;
    }

    private string? ValidateIdentity() {
        if (string.IsNullOrWhiteSpace(EmployeeName)) {
            return "EmployeeRequired";
        }

        if (string.IsNullOrWhiteSpace(EmployerName)) {
            return "EmployerRequired";
        }

        return string.IsNullOrWhiteSpace(DefaultProject) ? "ProjectRequired" : null;
    }

    private static bool IsValidRateValue(CompensationRateType type, decimal value) => type switch {
        CompensationRateType.HourlyPremiumPercent => value is >= 0m and <= 500m,
        CompensationRateType.FixedHourlyAmount => value is >= 0m and <= 100_000m,
        _ => value is > 0m and <= 100_000m
    };

    [RelayCommand]
    private Task ConfirmCurrencyRateChangeAsync() {
        IsCurrencyRatePromptOpen = false;
        return PersistSettingsAsync();
    }

    [RelayCommand]
    private void OpenBalanceAdjustment() {
        BalanceAdjustmentHours = MonthlyOpeningBalanceHours;
        ErrorText = string.Empty;
        IsBalanceAdjustmentOpen = true;
    }

    [RelayCommand]
    private void CloseBalanceAdjustment() {
        IsBalanceAdjustmentOpen = false;
        ErrorText = string.Empty;
    }

    [RelayCommand]
    private async Task SaveBalanceAdjustmentAsync() {
        try {
            int minutes = decimal.ToInt32(decimal.Round(BalanceAdjustmentHours * 60m, MidpointRounding.AwayFromZero));

            // Only the existing override is needed here, so the suggested balance is irrelevant and
            // replaying the month history for it would be wasted work.
            await workspace.SaveOpeningBalanceAsync(selectedMonth, minutes);
            IsBalanceAdjustmentOpen = false;
            ErrorText = string.Empty;
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Adjusting the starting balance for {Year}-{Month:D2} failed", selectedMonth.Year, selectedMonth.Month);
            ErrorText = localization.Get("ValidBalanceRequired");
        }
    }

    private async Task PersistSettingsAsync() {
        try {
            bool wasFirstRunSetup = IsSetupOpen;
            settings = CreateSettings(viewMode);
            await settingsRepository.SaveAsync(settings);
            await projects.EnsureDefaultAsync(settings.DefaultProject);
            localization.Apply(settings.LanguagePreference);
            themes.Apply(settings.ThemePreference);

            // The enum lists render through converters that read the active language.
            OnPropertyChanged(nameof(TaxModes));
            OnPropertyChanged(nameof(ThemePreferences));
            OnPropertyChanged(nameof(LanguagePreferences));
            IsSetupOpen = false;
            CurrentPage = wasFirstRunSetup ? monthWorkspacePage : settingsPage;
            SettingsStatus = wasFirstRunSetup ? string.Empty : localization.Get("SettingsSaved");
            ErrorText = string.Empty;
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            SettingsStatus = string.Empty;
            logger.LogError(exception, "Saving Tidverk settings failed");

            // The domain's messages are English and name parameters, so the form reports its own
            // check instead and falls back to a plain sentence for anything it did not anticipate.
            ErrorText = localization.Get(ValidateForm() ?? "SettingsSaveFailed");
        }
    }

    private AppSettings CreateSettings(MonthViewPreference preference) => new(
        EmployeeName,
        EmployerName,
        DefaultProject,
        new HourlySalary(HourlyRate),
        new ExpectedHoursSettings(ExpectedHoursPerDay, SelectedWeekdays(), ExcludePublicHolidays),
        TimeInput.Parse(DefaultStart),
        TimeInput.Parse(DefaultEnd),
        new Minutes(DefaultLunch),
        CreateTaxSettings(),
        SelectedTheme,
        OpeningBalanceMinutes,
        preference,
        SelectedLanguage,
        SelectedCurrency,
        SelectedInterfaceScale,
        SelectedExportLanguage,
        new OvertimeCompensationSettings(
            SelectedOvertimeMode,
            OvertimePremiumPercent,
            OvertimeDailyThresholdHours,
            OvertimeRateBands.Select(band => band.ToDomain()),
            SelectedOvertimeThresholdMode,
            SelectedOvertimeDefaultRateType,
            SelectedObOvertimeCombination),
        new SalarySettings(
            SelectedSalaryType,
            new HourlySalary(HourlyRate),
            MonthlySalary,
            EmploymentPercent));

    private TaxSettings CreateTaxSettings() => SelectedTaxMode switch {
        TaxMode.PrimaryIncomeTaxTable => new(SelectedTaxMode, TaxYear, TaxTableNumber, TaxColumn),
        TaxMode.ManualMonthlyDeduction => new(SelectedTaxMode, manualMonthlyDeduction: ManualTaxValue),
        _ => new(SelectedTaxMode)
    };

    private void CopySettingsToForm() {
        isLoadingSettingsForm = true;
        try {
            FillFormFromSettings();
        }
        finally {
            isLoadingSettingsForm = false;
        }

        OnPropertyChanged(string.Empty);
    }

    private void FillFormFromSettings() {
        EmployeeName = settings.EmployeeName;
        EmployerName = settings.EmployerName;
        DefaultProject = settings.DefaultProject;
        HourlyRate = settings.HourlySalary.Amount;
        SelectedSalaryType = settings.Salary.Type;
        MonthlySalary = settings.Salary.MonthlySalary > 0m ? settings.Salary.MonthlySalary : 25_000m;
        EmploymentPercent = settings.Salary.EmploymentPercent;
        ExpectedHoursPerDay = settings.ExpectedHours.HoursPerWorkday;
        ExcludePublicHolidays = settings.ExpectedHours.ExcludePublicHolidays;
        DefaultStart = TimeInput.Format(settings.DefaultStartTime);
        DefaultEnd = TimeInput.Format(settings.DefaultEndTime);
        DefaultLunch = settings.DefaultLunchMinutes.Value;
        OpeningBalanceMinutes = settings.OpeningBalanceMinutes;
        SelectedTaxMode = settings.TaxSettings.Mode;

        // Zero means "never chosen", so the form offers a usable starting point instead.
        TaxYear = settings.TaxSettings.TaxYear == 0 ? DefaultTaxYear : settings.TaxSettings.TaxYear;
        TaxTableNumber = settings.TaxSettings.TableNumber == 0 ? DefaultTaxTable : settings.TaxSettings.TableNumber;
        TaxColumn = settings.TaxSettings.Column == 0 ? DefaultTaxColumn : settings.TaxSettings.Column;
        ManualTaxValue = settings.TaxSettings.ManualMonthlyDeduction ?? 0m;
        SelectedTheme = settings.ThemePreference;
        SelectedLanguage = settings.LanguagePreference;
        SelectedExportLanguage = settings.ExportLanguagePreference;
        SelectedCurrency = settings.CurrencyPreference;
        SelectedOvertimeMode = settings.OvertimeCompensation.Mode;
        OvertimePremiumPercent = settings.OvertimeCompensation.PremiumPercent;
        OvertimeDailyThresholdHours = settings.OvertimeCompensation.DailyThresholdHours;
        SelectedOvertimeThresholdMode = settings.OvertimeCompensation.ThresholdMode;
        SelectedOvertimeDefaultRateType = settings.OvertimeCompensation.DefaultRateType;
        SelectedObOvertimeCombination = settings.OvertimeCompensation.ObOvertimeCombination;
        SelectedInterfaceScale = settings.InterfaceScalePercent;
        OvertimeRateBands.Clear();
        foreach (OvertimeRateBand band in settings.OvertimeCompensation.RateBands) {
            OvertimeRateBands.Add(OvertimeRateBandViewModel.FromDomain(band));
        }

        SetWeekdays(settings.ExpectedHours.WorkingWeekdays);
    }

    private DayOfWeek[] SelectedWeekdays() {
        List<DayOfWeek> weekdays = [];
        AddWeekday(weekdays, WorkMonday, DayOfWeek.Monday);
        AddWeekday(weekdays, WorkTuesday, DayOfWeek.Tuesday);
        AddWeekday(weekdays, WorkWednesday, DayOfWeek.Wednesday);
        AddWeekday(weekdays, WorkThursday, DayOfWeek.Thursday);
        AddWeekday(weekdays, WorkFriday, DayOfWeek.Friday);
        AddWeekday(weekdays, WorkSaturday, DayOfWeek.Saturday);
        AddWeekday(weekdays, WorkSunday, DayOfWeek.Sunday);
        return weekdays.ToArray();

        static void AddWeekday(List<DayOfWeek> weekdays, bool isSelected, DayOfWeek weekday) {
            if (isSelected) {
                weekdays.Add(weekday);
            }
        }
    }

    private void SetWeekdays(IReadOnlyCollection<DayOfWeek> weekdays) {
        WorkMonday = weekdays.Contains(DayOfWeek.Monday);
        WorkTuesday = weekdays.Contains(DayOfWeek.Tuesday);
        WorkWednesday = weekdays.Contains(DayOfWeek.Wednesday);
        WorkThursday = weekdays.Contains(DayOfWeek.Thursday);
        WorkFriday = weekdays.Contains(DayOfWeek.Friday);
        WorkSaturday = weekdays.Contains(DayOfWeek.Saturday);
        WorkSunday = weekdays.Contains(DayOfWeek.Sunday);
    }
}
