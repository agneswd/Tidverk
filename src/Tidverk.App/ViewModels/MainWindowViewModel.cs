using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>
/// The window shell. It owns the selected month, the day models both month views project, and the
/// open/closed state of every overlay. Behaviour is split across partial files by concern:
/// this file holds construction, month state and navigation, <c>.Editor</c> the day editor and
/// catch-up flow, <c>.Settings</c> the settings form, and <c>.Data</c> export, backup and restore.
/// </summary>
/// <remarks>
/// Two property styles are used deliberately: state the user edits is generated with
/// <c>[ObservableProperty]</c>, while state only the shell may change keeps a private setter.
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject {
    private readonly MonthlyWorkspaceService workspace;
    private readonly ISettingsRepository settingsRepository;
    private readonly IProjectRepository projects;
    private readonly ILocalizationService localization;
    private readonly IThemeService themes;
    private readonly DataOperations dataOperations;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly MonthWorkspacePage monthWorkspacePage;
    private readonly SettingsPage settingsPage;

    private AppSettings settings = AppSettings.Unconfigured;
    private IReadOnlyDictionary<DateOnly, WorkEntry> monthEntries = new Dictionary<DateOnly, WorkEntry>();
    private MonthlySummary? summary;
    private TaxEstimate monthTaxEstimate = TaxEstimate.Available(0m, 0m);
    private DateOnly selectedMonth;
    private MonthViewPreference viewMode;
    private AppPage currentPage;
    private SettingsSection settingsSection;
    private bool isBusy;
    private bool isSidebarExpanded = true;

    /// <summary>Design-time constructor. Avalonia's previewer instantiates the view model directly.</summary>
    public MainWindowViewModel()
        : this(
            DesignData.Workspace,
            DesignData.Settings,
            DesignData.Projects,
            DesignData.Localization,
            DesignData.Themes,
            DesignData.DataOperations,
            DesignData.Logger,
            UpdateService.Unavailable) {
        InitializeAsync().GetAwaiter().GetResult();
    }

    public MainWindowViewModel(
        MonthlyWorkspaceService workspace,
        ISettingsRepository settingsRepository,
        IProjectRepository projects,
        ILocalizationService localization,
        IThemeService themes,
        DataOperations dataOperations,
        ILogger<MainWindowViewModel> logger,
        UpdateService? updates = null) {
        this.workspace = workspace;
        this.settingsRepository = settingsRepository;
        this.projects = projects;
        this.localization = localization;
        this.themes = themes;
        this.dataOperations = dataOperations;
        this.logger = logger;
        Updates = updates ?? UpdateService.Unavailable;
        selectedMonth = FirstOfMonth(workspace.Today);
        monthWorkspacePage = new(this);
        settingsPage = new(this);
        currentPage = monthWorkspacePage;
    }

    public ObservableCollection<DayItemViewModel> Days { get; } = [];

    public ObservableCollection<DayItemViewModel> CalendarDays { get; } = [];

    public UpdateService Updates { get; }

    public DateOnly SelectedMonth => selectedMonth;

    public bool IsCurrentMonth => selectedMonth == FirstOfMonth(workspace.Today);

    public string MonthTitle => localization.Culture.TextInfo.ToTitleCase(selectedMonth.ToString("MMMM yyyy", localization.Culture));

    public AppPage CurrentPage {
        get => currentPage;
        private set {
            if (SetProperty(ref currentPage, value)) {
                OnPropertyChanged(nameof(IsMonthWorkspace));
                OnPropertyChanged(nameof(IsSettingsPage));
                OnPropertyChanged(nameof(IsLedger));
                OnPropertyChanged(nameof(IsCalendar));
            }
        }
    }

    public bool IsMonthWorkspace => CurrentPage is MonthWorkspacePage;

    public bool IsSettingsPage => CurrentPage is SettingsPage;

    public bool IsLedger => IsMonthWorkspace && viewMode == MonthViewPreference.Ledger;

    public bool IsCalendar => IsMonthWorkspace && viewMode == MonthViewPreference.Calendar;

    public bool IsSidebarExpanded { get => isSidebarExpanded; set => SetProperty(ref isSidebarExpanded, value); }

    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }

    public bool IsMonthUnstarted => monthEntries.Values.All(entry => entry.Status == WorkEntryStatus.Incomplete);

    public bool IsMonthStarted => !IsMonthUnstarted;

    public bool HasMissingDays => IsMonthStarted && (summary?.MissingPastDayCount ?? 0) > 0;

    public string MissingNotice => !IsMonthStarted ? string.Empty : summary?.MissingPastDayCount switch {
        1 => localization.Get("MissingOne"),
        > 1 => localization.Format("MissingMany", summary.MissingPastDayCount),
        _ => string.Empty
    };

    public string WorkedText => $"{(summary?.WorkedHours ?? 0m).ToString("0.0", localization.Culture)} h";

    public string WorkedBreakdownText => localization.Format("WorkedBreakdown", summary?.RegularHours ?? 0m, summary?.OvertimeHours ?? 0m);

    public string GrossPayDescription => SelectedOvertimeMode switch {
        OvertimeCompensationMode.Paid when OvertimeRateBands.Count > 0 => localization.Get("OvertimePaidConfiguredRates"),
        OvertimeCompensationMode.Paid => localization.Format("OvertimePaidPremium", OvertimePremiumPercent),
        _ => localization.Get("OvertimeExcluded")
    };

    public string BalanceText => FormatSignedHours(IsMonthUnstarted ? MonthlyOpeningBalance : summary?.ClosingBalanceMinutes ?? 0);

    public string GrossText => FormatMoney(summary?.GrossSalary ?? 0m);

    public string TaxText => monthTaxEstimate.IsAvailable
        ? FormatMoney(monthTaxEstimate.EstimatedNetPay ?? 0m)
        : localization.Get("Unavailable");

    public string PreliminaryTaxText => monthTaxEstimate.IsAvailable
        ? FormatMoney(monthTaxEstimate.PreliminaryTax ?? 0m)
        : ResourceKeys.TaxUnavailable(localization, monthTaxEstimate.UnavailableReason);

    public async Task InitializeAsync() {
        IsBusy = true;
        try {
            settings = await settingsRepository.GetAsync();
            viewMode = settings.MonthViewPreference;
            localization.Apply(settings.LanguagePreference);
            themes.Apply(settings.ThemePreference);
            CopySettingsToForm();
            IsSetupOpen = !settings.IsConfigured;
            await LoadMonthAsync();
        }
        finally {
            IsBusy = false;
        }
    }

    public void ShowStartupFailure() => ErrorText = localization.Get("StartupFailed");

    [RelayCommand]
    private Task PreviousMonthAsync() => GoToMonthAsync(selectedMonth.AddMonths(-1));

    [RelayCommand]
    private Task NextMonthAsync() => GoToMonthAsync(selectedMonth.AddMonths(1));

    [RelayCommand]
    private Task TodayAsync() => GoToMonthAsync(FirstOfMonth(workspace.Today));

    [RelayCommand]
    private Task ShowLedgerAsync() => SetViewAsync(MonthViewPreference.Ledger);

    [RelayCommand]
    private Task ShowCalendarAsync() => SetViewAsync(MonthViewPreference.Calendar);

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    /// <summary>Escape closes whichever surface is on top, innermost first.</summary>
    [RelayCommand]
    private void CloseTop() {
        if (IsCurrencyRatePromptOpen) {
            IsCurrencyRatePromptOpen = false;
        }
        else if (IsRestoreConfirmationOpen) {
            IsRestoreConfirmationOpen = false;
        }
        else if (IsBalanceAdjustmentOpen) {
            CloseBalanceAdjustment();
        }
        else if (IsReportOpen) {
            CloseReport();
        }
        else if (IsSettingsPage) {
            CloseSettings();
        }
        else if (IsCatchUpOpen) {
            CloseCatchUp();
        }
        else if (IsEditorOpen) {
            CloseEditor();
        }
    }

    private async Task GoToMonthAsync(DateOnly month) {
        selectedMonth = month;
        OnPropertyChanged(nameof(SelectedMonth));
        await LoadMonthAsync();
    }

    private async Task SetViewAsync(MonthViewPreference preference) {
        CurrentPage = monthWorkspacePage;
        viewMode = preference;
        OnPropertyChanged(nameof(IsLedger));
        OnPropertyChanged(nameof(IsCalendar));
        settings = CreateSettings(preference);
        if (settings.IsConfigured) {
            await settingsRepository.SaveAsync(settings);
        }
    }

    private async Task LoadMonthAsync() {
        MonthlyWorkspace loaded = await workspace.LoadAsync(selectedMonth, settings);
        monthEntries = loaded.Entries.ToDictionary(entry => entry.Date);
        MonthlyOpeningBalance = loaded.Month.OpeningBalanceMinutes;
        summary = loaded.Summary;
        monthTaxEstimate = loaded.TaxEstimate;
        BuildDays();
        RaiseMonthProperties();
    }

    /// <summary>
    /// Rebuilds both projections of the month. The calendar pads to whole Monday-start weeks, so it
    /// also shows the surrounding days, which are visible but not editable.
    /// </summary>
    private void BuildDays() {
        Days.Clear();
        foreach (DateOnly date in ExpectedHoursCalculator.GetDates(selectedMonth.Year, selectedMonth.Month)) {
            Days.Add(CreateDay(date, isInMonth: true));
        }

        CalendarDays.Clear();
        int leadingDays = ((int)selectedMonth.DayOfWeek + 6) % 7;
        int dayCount = DateTime.DaysInMonth(selectedMonth.Year, selectedMonth.Month);
        int cellCount = (int)Math.Ceiling((leadingDays + dayCount) / 7m) * 7;
        DateOnly firstCell = selectedMonth.AddDays(-leadingDays);
        for (int index = 0; index < cellCount; index++) {
            DateOnly date = firstCell.AddDays(index);
            CalendarDays.Add(CreateDay(date, date.Month == selectedMonth.Month));
        }
    }

    private DayItemViewModel CreateDay(DateOnly date, bool isInMonth) {
        WorkEntry entry = monthEntries.GetValueOrDefault(date) ?? WorkEntry.CreateIncomplete(date);
        return new(
            date,
            entry,
            isInMonth,
            ResourceKeys.HolidayName(localization, workspace.GetHolidayName(date)),
            workspace.IsScheduledWorkday(date, settings),
            workspace.Today,
            IsMonthStarted,
            localization,
            GetDailyPayText(entry)) {
            IsSelected = isInMonth && SelectedDay?.Date == date
        };
    }

    /// <summary>Net pay is apportioned from the month's estimate, because withholding is monthly.</summary>
    private string GetDailyPayText(WorkEntry entry) {
        if (entry.Status != WorkEntryStatus.Worked || summary is null) {
            return string.Empty;
        }

        decimal gross = workspace.GrossSalary(entry, settings);
        if (!monthTaxEstimate.IsAvailable || monthTaxEstimate.EstimatedNetPay is null || summary.GrossSalary <= 0m) {
            return FormatMoney(gross);
        }

        decimal net = monthTaxEstimate.EstimatedNetPay.Value * gross / summary.GrossSalary;
        return $"{FormatMoney(gross)} ({FormatMoney(net)})";
    }

    private void RaiseMonthProperties() {
        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(IsCurrentMonth));
        OnPropertyChanged(nameof(IsMonthUnstarted));
        OnPropertyChanged(nameof(IsMonthStarted));
        OnPropertyChanged(nameof(HasMissingDays));
        OnPropertyChanged(nameof(MissingNotice));
        OnPropertyChanged(nameof(WorkedText));
        OnPropertyChanged(nameof(WorkedBreakdownText));
        OnPropertyChanged(nameof(GrossPayDescription));
        OnPropertyChanged(nameof(BalanceText));
        OnPropertyChanged(nameof(GrossText));
        OnPropertyChanged(nameof(TaxText));
        OnPropertyChanged(nameof(PreliminaryTaxText));
        OnPropertyChanged(nameof(BalanceAdjustmentDescription));
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private string FormatSignedHours(int minutes) =>
        $"{(minutes >= 0 ? "+" : "-")}{(Math.Abs(minutes) / 60m).ToString("0.0", localization.Culture)} h";

    private string FormatMoney(decimal value) => $"{value.ToString("N0", localization.Culture)} {settings.CurrencyPreference}";
}
