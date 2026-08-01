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

    [ObservableProperty]
    private string employeeName = string.Empty;

    [ObservableProperty]
    private string employerName = string.Empty;

    [ObservableProperty]
    private string defaultProject = string.Empty;

    [ObservableProperty]
    private decimal hourlyRate = 202m;

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
    [NotifyPropertyChangedFor(nameof(GrossPayDescription))]
    private OvertimeCompensationMode selectedOvertimeMode;

    [ObservableProperty]
    private decimal overtimePremiumPercent = 50m;

    [ObservableProperty]
    private decimal overtimeDailyThresholdHours = 8m;

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

    public ObservableCollection<OvertimeRateBandViewModel> OvertimeRateBands { get; } = [];

    public IReadOnlyList<TaxMode> TaxModes { get; } = Enum.GetValues<TaxMode>();

    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();

    public IReadOnlyList<LanguagePreference> LanguagePreferences { get; } = Enum.GetValues<LanguagePreference>();

    public IReadOnlyList<ExportLanguagePreference> ExportLanguagePreferences { get; } = Enum.GetValues<ExportLanguagePreference>();

    public IReadOnlyList<CurrencyPreference> CurrencyPreferences { get; } = Enum.GetValues<CurrencyPreference>();

    public IReadOnlyList<OvertimeCompensationMode> OvertimeCompensationModes { get; } = Enum.GetValues<OvertimeCompensationMode>();

    public IReadOnlyList<OvertimeDayCategory> OvertimeDayCategories { get; } = Enum.GetValues<OvertimeDayCategory>();

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
    private void AddOvertimeRateBand() =>
        OvertimeRateBands.Add(new OvertimeRateBandViewModel { Name = localization.Get("OvertimeRateBandDefaultName") });

    [RelayCommand]
    private void RemoveOvertimeRateBand(OvertimeRateBandViewModel? band) {
        if (band is not null) {
            OvertimeRateBands.Remove(band);
        }
    }

    /// <summary>A currency change leaves the hourly rate untouched, so the user is asked to confirm it first.</summary>
    [RelayCommand]
    private Task SaveSettingsAsync() {
        if (settings.IsConfigured && SelectedCurrency != settings.CurrencyPreference) {
            OnPropertyChanged(nameof(CurrencyChangeText));
            IsCurrencyRatePromptOpen = true;
            return Task.CompletedTask;
        }

        return PersistSettingsAsync();
    }

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

            // Domain validation messages name the offending field, so they are worth showing verbatim.
            ErrorText = exception is ArgumentException ? exception.Message : localization.Get("SettingsSaveFailed");
        }
    }

    private AppSettings CreateSettings(MonthViewPreference preference) => new(
        EmployeeName,
        EmployerName,
        DefaultProject,
        new HourlySalary(HourlyRate),
        new ExpectedHoursSettings(ExpectedHoursPerDay, SelectedWeekdays(), excludePublicHolidays: true),
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
            OvertimeRateBands.Select(band => band.ToDomain())));

    private TaxSettings CreateTaxSettings() => SelectedTaxMode switch {
        TaxMode.PrimaryIncomeTaxTable => new(SelectedTaxMode, TaxYear, TaxTableNumber, TaxColumn),
        TaxMode.ManualMonthlyDeduction => new(SelectedTaxMode, manualMonthlyDeduction: ManualTaxValue),
        _ => new(SelectedTaxMode)
    };

    private void CopySettingsToForm() {
        EmployeeName = settings.EmployeeName;
        EmployerName = settings.EmployerName;
        DefaultProject = settings.DefaultProject;
        HourlyRate = settings.HourlySalary.Amount;
        ExpectedHoursPerDay = settings.ExpectedHours.HoursPerWorkday;
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
        SelectedInterfaceScale = settings.InterfaceScalePercent;
        OvertimeRateBands.Clear();
        foreach (OvertimeRateBand band in settings.OvertimeCompensation.RateBands) {
            OvertimeRateBands.Add(OvertimeRateBandViewModel.FromDomain(band));
        }

        SetWeekdays(settings.ExpectedHours.WorkingWeekdays);
        OnPropertyChanged(string.Empty);
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
