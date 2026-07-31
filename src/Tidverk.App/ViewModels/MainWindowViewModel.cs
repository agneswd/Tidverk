using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.App.Services;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Export;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject {
    private readonly IWorkEntryRepository workEntries;
    private readonly ISettingsRepository settingsRepository;
    private readonly IMonthRepository months;
    private readonly IProjectRepository projects;
    private readonly ISwedishHolidayService holidays;
    private readonly IClock clock;
    private readonly ITaxCalculator taxes;
    private readonly IFileDialogService fileDialogs;
    private readonly ILocalizationService localization;
    private readonly IThemeService themes;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly AppPaths appPaths;
    private readonly DatabaseBackupService backups;
    private readonly IDataFolderService dataFolders;
    private AppSettings settings = AppSettings.Unconfigured;
    private IReadOnlyDictionary<DateOnly, WorkEntry> monthEntries = new Dictionary<DateOnly, WorkEntry>();
    private MonthlySummary? summary;
    private DateOnly selectedMonth;
    private MonthViewPreference viewMode;
    private bool isBusy;
    private bool isEditorOpen;
    private bool isSetupOpen;
    private readonly MonthWorkspacePage monthWorkspacePage;
    private readonly SettingsPage settingsPage;
    private AppPage currentPage;
    private bool isReportOpen;
    private bool isCatchUpOpen;
    private DayItemViewModel? selectedDay;
    private string editorStart = "08:00";
    private string editorEnd = "16:30";
    private int editorLunch = 30;
    private string editorProject = string.Empty;
    private string editorNotes = string.Empty;
    private bool editorIsOff;
    private string errorText = string.Empty;
    private int catchUpIndex;
    private List<DateOnly> catchUpDates = [];
    private int monthlyOpeningBalance;
    private decimal balanceAdjustmentHours;
    private bool isBalanceAdjustmentOpen;
    private bool isRestoreConfirmationOpen;
    private string? pendingRestorePath;
    private string backupStatus = string.Empty;
    private string settingsStatus = string.Empty;
    private bool isSidebarExpanded = true;
    private SettingsSection settingsSection;
    private LanguagePreference selectedLanguage;
    private ExportLanguagePreference selectedExportLanguage;
    private CurrencyPreference selectedCurrency;
    private OvertimeCompensationMode selectedOvertimeMode;
    private int selectedInterfaceScale = 100;
    private bool isCurrencyRatePromptOpen;

    public MainWindowViewModel()
        : this(DesignData.Services) {
        InitializeAsync().GetAwaiter().GetResult();
    }

    private MainWindowViewModel(DesignServiceSet services)
        : this(
            services.WorkEntries,
            services.Settings,
            services.Months,
            services.Projects,
            services.Holidays,
            services.Clock,
            services.Taxes,
            services.FileDialogs,
            services.Localization,
            services.Themes,
            services.Paths,
            services.Backups,
            services.DataFolders,
            services.Logger) {
    }

    public MainWindowViewModel(
        IWorkEntryRepository workEntries,
        ISettingsRepository settingsRepository,
        IMonthRepository months,
        IProjectRepository projects,
        ISwedishHolidayService holidays,
        IClock clock,
        ITaxCalculator taxes,
        IFileDialogService fileDialogs,
        ILocalizationService localization,
        IThemeService themes,
        AppPaths appPaths,
        DatabaseBackupService backups,
        IDataFolderService dataFolders,
        ILogger<MainWindowViewModel> logger) {
        this.workEntries = workEntries;
        this.settingsRepository = settingsRepository;
        this.months = months;
        this.projects = projects;
        this.holidays = holidays;
        this.clock = clock;
        this.taxes = taxes;
        this.fileDialogs = fileDialogs;
        this.localization = localization;
        this.themes = themes;
        this.appPaths = appPaths;
        this.backups = backups;
        this.dataFolders = dataFolders;
        this.logger = logger;
        selectedMonth = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        monthWorkspacePage = new(this);
        settingsPage = new(this);
        currentPage = monthWorkspacePage;

        InitializeCommands();
    }

    public ObservableCollection<DayItemViewModel> Days { get; } = [];

    public ObservableCollection<DayItemViewModel> CalendarDays { get; } = [];

    public ObservableCollection<OvertimeRateBandViewModel> OvertimeRateBands { get; } = [];

    public IReadOnlyList<TaxMode> TaxModes { get; } = Enum.GetValues<TaxMode>();

    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();

    public IReadOnlyList<LanguagePreference> LanguagePreferences { get; } = Enum.GetValues<LanguagePreference>();

    public IReadOnlyList<ExportLanguagePreference> ExportLanguagePreferences { get; } = Enum.GetValues<ExportLanguagePreference>();

    public IReadOnlyList<CurrencyPreference> CurrencyPreferences { get; } = Enum.GetValues<CurrencyPreference>();

    public IReadOnlyList<OvertimeCompensationMode> OvertimeCompensationModes { get; } = Enum.GetValues<OvertimeCompensationMode>();

    public IReadOnlyList<OvertimeDayCategory> OvertimeDayCategories { get; } = Enum.GetValues<OvertimeDayCategory>();

    public IReadOnlyList<int> InterfaceScaleOptions { get; } = [80, 90, 100, 110, 125, 150];

    public IAsyncRelayCommand PreviousMonthCommand { get; private set; } = null!;
    public IAsyncRelayCommand NextMonthCommand { get; private set; } = null!;
    public IAsyncRelayCommand TodayCommand { get; private set; } = null!;
    public IAsyncRelayCommand ShowLedgerCommand { get; private set; } = null!;
    public IAsyncRelayCommand ShowCalendarCommand { get; private set; } = null!;
    public IRelayCommand<DayItemViewModel> OpenEditorCommand { get; private set; } = null!;
    public IRelayCommand CloseEditorCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveEntryCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveAndNextCommand { get; private set; } = null!;
    public IAsyncRelayCommand ResetEntryCommand { get; private set; } = null!;
    public IRelayCommand NormalDayCommand { get; private set; } = null!;
    public IRelayCommand CopyPreviousCommand { get; private set; } = null!;
    public IRelayCommand CopyLastWeekCommand { get; private set; } = null!;
    public IRelayCommand StartCatchUpCommand { get; private set; } = null!;
    public IRelayCommand SkipCatchUpCommand { get; private set; } = null!;
    public IRelayCommand BackCatchUpCommand { get; private set; } = null!;
    public IRelayCommand CloseCatchUpCommand { get; private set; } = null!;
    public IRelayCommand OpenSetupCommand { get; private set; } = null!;
    public IRelayCommand StartMonthCommand { get; private set; } = null!;
    public IRelayCommand CloseSettingsCommand { get; private set; } = null!;
    public IRelayCommand ShowEmploymentSettingsCommand { get; private set; } = null!;
    public IRelayCommand ShowAppearanceSettingsCommand { get; private set; } = null!;
    public IRelayCommand ShowDataSettingsCommand { get; private set; } = null!;
    public IRelayCommand ToggleSidebarCommand { get; private set; } = null!;
    public IRelayCommand AddOvertimeRateBandCommand { get; private set; } = null!;
    public IRelayCommand<OvertimeRateBandViewModel> RemoveOvertimeRateBandCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveSettingsCommand { get; private set; } = null!;
    public IAsyncRelayCommand ConfirmCurrencyRateChangeCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveCurrentCommand { get; private set; } = null!;
    public IRelayCommand OpenReportCommand { get; private set; } = null!;
    public IRelayCommand CloseReportCommand { get; private set; } = null!;
    public IAsyncRelayCommand ExportCommand { get; private set; } = null!;
    public IAsyncRelayCommand BackupCommand { get; private set; } = null!;
    public IAsyncRelayCommand ChooseRestoreCommand { get; private set; } = null!;
    public IAsyncRelayCommand ConfirmRestoreCommand { get; private set; } = null!;
    public IRelayCommand CancelRestoreCommand { get; private set; } = null!;
    public IRelayCommand OpenDataFolderCommand { get; private set; } = null!;
    public IRelayCommand OpenBalanceAdjustmentCommand { get; private set; } = null!;
    public IRelayCommand CloseBalanceAdjustmentCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveBalanceAdjustmentCommand { get; private set; } = null!;
    public IRelayCommand CloseTopCommand { get; private set; } = null!;

    public DateOnly SelectedMonth => selectedMonth;
    public bool IsCurrentMonth => selectedMonth.Year == clock.Today.Year && selectedMonth.Month == clock.Today.Month;
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
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public bool IsEditorOpen { get => isEditorOpen; private set => SetProperty(ref isEditorOpen, value); }
    public bool IsSetupOpen { get => isSetupOpen; private set => SetProperty(ref isSetupOpen, value); }
    public bool IsReportOpen { get => isReportOpen; private set => SetProperty(ref isReportOpen, value); }
    public bool IsCatchUpOpen { get => isCatchUpOpen; private set => SetProperty(ref isCatchUpOpen, value); }
    public bool IsBalanceAdjustmentOpen { get => isBalanceAdjustmentOpen; private set => SetProperty(ref isBalanceAdjustmentOpen, value); }
    public bool IsRestoreConfirmationOpen { get => isRestoreConfirmationOpen; private set => SetProperty(ref isRestoreConfirmationOpen, value); }
    public bool IsCurrencyRatePromptOpen { get => isCurrencyRatePromptOpen; private set => SetProperty(ref isCurrencyRatePromptOpen, value); }
    public DayItemViewModel? SelectedDay { get => selectedDay; private set => SetProperty(ref selectedDay, value); }
    public string EditorStart { get => editorStart; set { SetProperty(ref editorStart, value); OnPropertyChanged(nameof(EditorHours)); } }
    public string EditorEnd { get => editorEnd; set { SetProperty(ref editorEnd, value); OnPropertyChanged(nameof(EditorHours)); } }
    public int EditorLunch { get => editorLunch; set { SetProperty(ref editorLunch, value); OnPropertyChanged(nameof(EditorHours)); } }
    public string EditorProject { get => editorProject; set => SetProperty(ref editorProject, value); }
    public string EditorNotes { get => editorNotes; set => SetProperty(ref editorNotes, value); }
    public bool EditorIsOff { get => editorIsOff; set { SetProperty(ref editorIsOff, value); OnPropertyChanged(nameof(EditorHours)); } }
    public string ErrorText { get => errorText; private set { SetProperty(ref errorText, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
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
    public string TaxText {
        get {
            TaxEstimate estimate = taxes.Calculate(summary?.GrossSalary ?? 0m, settings.TaxSettings);
            return estimate.IsAvailable ? FormatMoney(estimate.EstimatedNetPay ?? 0m) : localization.Get("Unavailable");
        }
    }
    public string PreliminaryTaxText {
        get {
            TaxEstimate estimate = taxes.Calculate(summary?.GrossSalary ?? 0m, settings.TaxSettings);
            return estimate.IsAvailable ? FormatMoney(estimate.PreliminaryTax ?? 0m) : LocalizeTaxError(estimate.UnavailableReason);
        }
    }
    public string EditorTitle => SelectedDay?.Date.ToString("dddd d MMMM", localization.Culture) ?? localization.Get("Day");
    public string EditorHours {
        get {
            if (EditorIsOff || !TimeInput.TryNormalize(EditorStart, out string start) || !TimeInput.TryNormalize(EditorEnd, out string end)) {
                return "0.0 h";
            }

            int minutes = MinuteMath.Worked(TimeInput.Parse(start), TimeInput.Parse(end), new Minutes(Math.Max(0, EditorLunch))).Value;
            return $"{(minutes / 60m).ToString("0.0", localization.Culture)} h";
        }
    }
    public string CatchUpTitle => SelectedDay?.Date.ToString("dddd d MMMM", localization.Culture) ?? string.Empty;
    public string CatchUpProgress => catchUpDates.Count == 0 ? string.Empty : localization.Format("CatchUpProgress", catchUpIndex + 1, catchUpDates.Count);
    public string BalanceAdjustmentDescription => localization.Format("BalanceAdjustDescription", MonthTitle);

    public string EmployeeName { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string DefaultProject { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; } = 202m;
    public decimal ExpectedHoursPerDay { get; set; } = 8m;
    public string DefaultStart { get; set; } = "08:00";
    public string DefaultEnd { get; set; } = "16:30";
    public int DefaultLunch { get; set; } = 30;
    public int OpeningBalanceMinutes { get; set; }
    public TaxMode SelectedTaxMode { get; set; }
    public int TaxYear { get; set; } = 2026;
    public int TaxTableNumber { get; set; } = 33;
    public int TaxColumn { get; set; } = 1;
    public decimal ManualTaxValue { get; set; }
    public ThemePreference SelectedTheme { get; set; }
    public LanguagePreference SelectedLanguage { get => selectedLanguage; set => SetProperty(ref selectedLanguage, value); }
    public ExportLanguagePreference SelectedExportLanguage { get => selectedExportLanguage; set => SetProperty(ref selectedExportLanguage, value); }
    public CurrencyPreference SelectedCurrency { get => selectedCurrency; set => SetProperty(ref selectedCurrency, value); }
    public OvertimeCompensationMode SelectedOvertimeMode {
        get => selectedOvertimeMode;
        set {
            if (SetProperty(ref selectedOvertimeMode, value)) {
                OnPropertyChanged(nameof(IsPaidOvertime));
                OnPropertyChanged(nameof(OvertimeCompensationDescription));
                OnPropertyChanged(nameof(GrossPayDescription));
            }
        }
    }
    public decimal OvertimePremiumPercent { get; set; } = 50m;
    public decimal OvertimeDailyThresholdHours { get; set; } = 8m;
    public bool IsPaidOvertime => SelectedOvertimeMode == OvertimeCompensationMode.Paid;
    public string OvertimeCompensationDescription => IsPaidOvertime
        ? localization.Get("OvertimePaidDescription")
        : localization.Get("OvertimeCompTimeDescription");
    public string CurrencyChangeText => localization.Format("CurrencyChangeSummary", settings.CurrencyPreference, SelectedCurrency);
    public int SelectedInterfaceScale {
        get => selectedInterfaceScale;
        set {
            if (SetProperty(ref selectedInterfaceScale, value)) {
                OnPropertyChanged(nameof(InterfaceScale));
            }
        }
    }
    public double InterfaceScale => SelectedInterfaceScale / 100d;
    public bool WorkMonday { get; set; } = true;
    public bool WorkTuesday { get; set; } = true;
    public bool WorkWednesday { get; set; } = true;
    public bool WorkThursday { get; set; } = true;
    public bool WorkFriday { get; set; } = true;
    public bool WorkSaturday { get; set; }
    public bool WorkSunday { get; set; }
    public int MonthlyOpeningBalance {
        get => monthlyOpeningBalance;
        set {
            if (SetProperty(ref monthlyOpeningBalance, value)) {
                OnPropertyChanged(nameof(MonthlyOpeningBalanceHours));
            }
        }
    }

    public decimal MonthlyOpeningBalanceHours {
        get => MonthlyOpeningBalance / 60m;
        set => MonthlyOpeningBalance = decimal.ToInt32(decimal.Round(value * 60m, MidpointRounding.AwayFromZero));
    }
    public decimal BalanceAdjustmentHours { get => balanceAdjustmentHours; set => SetProperty(ref balanceAdjustmentHours, value); }
    public string DataDirectory => appPaths.DataDirectory;
    public string BackupStatus { get => backupStatus; private set => SetProperty(ref backupStatus, value); }
    public string SettingsStatus {
        get => settingsStatus;
        private set {
            if (SetProperty(ref settingsStatus, value)) {
                OnPropertyChanged(nameof(HasSettingsStatus));
            }
        }
    }
    public bool HasSettingsStatus => !string.IsNullOrWhiteSpace(SettingsStatus);

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
        catch (Exception exception) {
            logger.LogError(exception, "Tidverk startup failed");
            ErrorText = "Tidverk could not load local data. See the local log for details.";
        }
        finally {
            IsBusy = false;
        }
    }

    private void InitializeCommands() {
        PreviousMonthCommand = new AsyncRelayCommand(() => ChangeMonthAsync(-1));
        NextMonthCommand = new AsyncRelayCommand(() => ChangeMonthAsync(1));
        TodayCommand = new AsyncRelayCommand(GoToTodayAsync);
        ShowLedgerCommand = new AsyncRelayCommand(() => SetViewAsync(MonthViewPreference.Ledger));
        ShowCalendarCommand = new AsyncRelayCommand(() => SetViewAsync(MonthViewPreference.Calendar));
        OpenEditorCommand = new RelayCommand<DayItemViewModel>(OpenEditor);
        CloseEditorCommand = new RelayCommand(CloseEditor);
        SaveEntryCommand = new AsyncRelayCommand(SaveEntryAsync);
        SaveAndNextCommand = new AsyncRelayCommand(SaveAndNextAsync);
        ResetEntryCommand = new AsyncRelayCommand(ResetEntryAsync);
        NormalDayCommand = new RelayCommand(SetNormalDay);
        CopyPreviousCommand = new RelayCommand(CopyPrevious);
        CopyLastWeekCommand = new RelayCommand(CopyLastWeek);
        StartCatchUpCommand = new RelayCommand(StartCatchUp);
        SkipCatchUpCommand = new RelayCommand(SkipCatchUp);
        BackCatchUpCommand = new RelayCommand(BackCatchUp);
        CloseCatchUpCommand = new RelayCommand(CloseCatchUp);
        OpenSetupCommand = new RelayCommand(OpenSettings);
        StartMonthCommand = new RelayCommand(StartMonth);
        CloseSettingsCommand = new RelayCommand(CloseSettings);
        ShowEmploymentSettingsCommand = new RelayCommand(() => CurrentSettingsSection = SettingsSection.Employment);
        ShowAppearanceSettingsCommand = new RelayCommand(() => CurrentSettingsSection = SettingsSection.Appearance);
        ShowDataSettingsCommand = new RelayCommand(() => CurrentSettingsSection = SettingsSection.Data);
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        InitializeOvertimeCommands();
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ConfirmCurrencyRateChangeCommand = new AsyncRelayCommand(ConfirmCurrencyRateChangeAsync);
        SaveCurrentCommand = new AsyncRelayCommand(SaveCurrentAsync);
        OpenReportCommand = new RelayCommand(OpenReport);
        CloseReportCommand = new RelayCommand(() => IsReportOpen = false);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        ChooseRestoreCommand = new AsyncRelayCommand(ChooseRestoreAsync);
        ConfirmRestoreCommand = new AsyncRelayCommand(ConfirmRestoreAsync);
        CancelRestoreCommand = new RelayCommand(() => IsRestoreConfirmationOpen = false);
        OpenDataFolderCommand = new RelayCommand(() => dataFolders.Open(appPaths.DataDirectory));
        OpenBalanceAdjustmentCommand = new RelayCommand(OpenBalanceAdjustment);
        CloseBalanceAdjustmentCommand = new RelayCommand(CloseBalanceAdjustment);
        SaveBalanceAdjustmentCommand = new AsyncRelayCommand(SaveBalanceAdjustmentAsync);
        CloseTopCommand = new RelayCommand(CloseTop);
    }

    private void InitializeOvertimeCommands() {
        AddOvertimeRateBandCommand = new RelayCommand(() => OvertimeRateBands.Add(new OvertimeRateBandViewModel {
            Name = localization.Get("OvertimeRateBandDefaultName")
        }));
        RemoveOvertimeRateBandCommand = new RelayCommand<OvertimeRateBandViewModel>(band => {
            if (band is not null) OvertimeRateBands.Remove(band);
        });
    }

    private void CloseTop() {
        if (IsCurrencyRatePromptOpen) IsCurrencyRatePromptOpen = false;
        else if (IsRestoreConfirmationOpen) IsRestoreConfirmationOpen = false;
        else if (IsBalanceAdjustmentOpen) CloseBalanceAdjustment();
        else if (IsReportOpen) IsReportOpen = false;
        else if (IsSettingsPage) CloseSettings();
        else if (IsCatchUpOpen) CloseCatchUp();
        else if (IsEditorOpen) CloseEditor();
    }

    private async Task LoadMonthAsync() {
        IReadOnlyList<WorkEntry> loaded = await workEntries.GetMonthAsync(selectedMonth.Year, selectedMonth.Month);
        monthEntries = loaded.ToDictionary(entry => entry.Date);
        int suggestedOpeningBalance = await GetSuggestedOpeningBalanceAsync();
        MonthRecord month = await months.GetAsync(selectedMonth.Year, selectedMonth.Month, suggestedOpeningBalance);
        MonthlyOpeningBalance = month.OpeningBalanceMinutes;
        summary = MonthlyCalculator.Calculate(month, loaded, settings.ExpectedHours, settings.HourlySalary, clock.Today, holidays, settings.OvertimeCompensation);
        BuildDays();
        RaiseMonthProperties();
    }

    private async Task<int> GetSuggestedOpeningBalanceAsync() {
        var history = new List<(MonthRecord Month, IReadOnlyList<WorkEntry> Entries)>();
        DateOnly cursor = selectedMonth.AddMonths(-1);
        for (int count = 0; count < 120; count++, cursor = cursor.AddMonths(-1)) {
            IReadOnlyList<WorkEntry> entries = await workEntries.GetMonthAsync(cursor.Year, cursor.Month);
            MonthRecord month = await months.GetAsync(cursor.Year, cursor.Month, settings.OpeningBalanceMinutes);
            history.Add((month, entries));
            if (month.OpeningBalanceWasEdited) {
                break;
            }
        }

        int balance = settings.OpeningBalanceMinutes;
        for (int index = history.Count - 1; index >= 0; index--) {
            (MonthRecord month, IReadOnlyList<WorkEntry> entries) = history[index];
            int opening = month.OpeningBalanceWasEdited ? month.OpeningBalanceMinutes : balance;
            if (entries.Any(entry => entry.Status != WorkEntryStatus.Incomplete)) {
                MonthRecord carriedMonth = new(month.Year, month.Month, opening, month.ExpectedMinutesOverride, month.OpeningBalanceWasEdited);
                balance = MonthlyCalculator.Calculate(carriedMonth, entries, settings.ExpectedHours, settings.HourlySalary, clock.Today, holidays, settings.OvertimeCompensation)
                    .ClosingBalanceMinutes;
            }
            else {
                balance = opening;
            }
        }

        return balance;
    }

    private void BuildDays() {
        Days.Clear();
        foreach (DateOnly date in ExpectedHoursCalculator.GetDates(selectedMonth.Year, selectedMonth.Month)) {
            Days.Add(CreateDay(date, true));
        }

        CalendarDays.Clear();
        int offset = ((int)selectedMonth.DayOfWeek + 6) % 7;
        int cellCount = (int)Math.Ceiling((offset + DateTime.DaysInMonth(selectedMonth.Year, selectedMonth.Month)) / 7m) * 7;
        DateOnly firstCell = selectedMonth.AddDays(-offset);
        for (int index = 0; index < cellCount; index++) {
            DateOnly date = firstCell.AddDays(index);
            CalendarDays.Add(CreateDay(date, date.Month == selectedMonth.Month));
        }
    }

    private DayItemViewModel CreateDay(DateOnly date, bool isInMonth) {
        WorkEntry entry = monthEntries.GetValueOrDefault(date) ?? WorkEntry.CreateIncomplete(date);
        string? holiday = LocalizeHoliday(holidays.GetHolidays(date.Year).FirstOrDefault(item => item.Date == date).Name);
        bool isExpectedWorkday = settings.ExpectedHours.IsExpectedWeekday(date) &&
            (!settings.ExpectedHours.ExcludePublicHolidays || !holidays.IsPublicHoliday(date));
        return new(date, entry, isInMonth, holiday, isExpectedWorkday, clock.Today, IsMonthStarted, localization, GetDailyPayText(entry)) {
            IsSelected = isInMonth && SelectedDay?.Date == date
        };
    }

    private async Task ChangeMonthAsync(int delta) {
        selectedMonth = selectedMonth.AddMonths(delta);
        OnPropertyChanged(nameof(SelectedMonth));
        await LoadMonthAsync();
    }

    private async Task GoToTodayAsync() {
        selectedMonth = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
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

    private void OpenEditor(DayItemViewModel? day) {
        if (day is null || !day.IsInMonth) {
            return;
        }

        SelectedDay = day;
        foreach (DayItemViewModel item in Days.Concat(CalendarDays)) {
            item.IsSelected = item.IsInMonth && item.Date == day.Date;
        }

        WorkEntry entry = day.Entry;
        EditorIsOff = entry.Status == WorkEntryStatus.Off;
        EditorStart = entry.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? settings.DefaultStartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorEnd = entry.EndTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? settings.DefaultEndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorLunch = entry.Status == WorkEntryStatus.Worked ? entry.LunchMinutes.Value : settings.DefaultLunchMinutes.Value;
        EditorProject = entry.ProjectName ?? settings.DefaultProject;
        EditorNotes = entry.Notes ?? string.Empty;
        ErrorText = string.Empty;
        IsEditorOpen = true;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorHours));
    }

    private void CloseEditor() {
        IsEditorOpen = false;
        ErrorText = string.Empty;
        ClearSelection();
    }

    private async Task SaveEntryAsync() {
        if (SelectedDay is null) {
            return;
        }

        try {
            WorkEntry entry;
            if (EditorIsOff) {
                entry = WorkEntry.CreateOff(SelectedDay.Date, EditorNotes);
            }
            else if (!WorkEntry.TryCreateWorked(SelectedDay.Date, EditorStart, EditorEnd, EditorLunch, out WorkEntry? worked, out IReadOnlyList<string> errors, EditorProject, EditorNotes)) {
                ErrorText = string.Join(" ", errors);
                return;
            }
            else {
                entry = worked!;
            }

            await workEntries.SaveAsync(entry);
            ErrorText = string.Empty;
            await LoadMonthAsync();
            IsEditorOpen = false;
            ClearSelection();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Saving work entry for {Date} failed", SelectedDay.Date);
            ErrorText = localization.Get("EntryNotSaved");
        }
    }

    private async Task SaveAndNextAsync() {
        await SaveEntryAsync();
        if (!HasError && IsCatchUpOpen) {
            MoveCatchUp(1);
        }
    }

    private async Task ResetEntryAsync() {
        if (SelectedDay is null) {
            return;
        }

        await workEntries.ResetAsync(SelectedDay.Date);
        await LoadMonthAsync();
        IsEditorOpen = false;
        ClearSelection();
    }

    private void SetNormalDay() {
        EditorIsOff = false;
        EditorStart = settings.DefaultStartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorEnd = settings.DefaultEndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorLunch = settings.DefaultLunchMinutes.Value;
        EditorProject = settings.DefaultProject;
        OnPropertyChanged(nameof(EditorHours));
    }

    private void CopyPrevious() {
        WorkEntry? previous = monthEntries.Values.Where(entry => entry.Date < SelectedDay?.Date && entry.Status == WorkEntryStatus.Worked)
            .OrderByDescending(entry => entry.Date).FirstOrDefault();
        CopyEntry(previous);
    }

    private void CopyLastWeek() {
        if (SelectedDay is not null) {
            CopyEntry(monthEntries.GetValueOrDefault(SelectedDay.Date.AddDays(-7)));
        }
    }

    private void CopyEntry(WorkEntry? entry) {
        if (entry?.Status != WorkEntryStatus.Worked) {
            ErrorText = localization.Get("NoCompletedDay");
            return;
        }

        EditorIsOff = false;
        EditorStart = entry.StartTime!.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorEnd = entry.EndTime!.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        EditorLunch = entry.LunchMinutes.Value;
        EditorProject = entry.ProjectName ?? settings.DefaultProject;
        ErrorText = string.Empty;
        OnPropertyChanged(nameof(EditorHours));
    }

    private void StartCatchUp() {
        catchUpDates = summary?.MissingPastDays.ToList() ?? [];
        catchUpIndex = 0;
        IsCatchUpOpen = catchUpDates.Count > 0;
        OpenCatchUpDay();
    }

    private void SkipCatchUp() => MoveCatchUp(1);
    private void BackCatchUp() => MoveCatchUp(-1);
    private void CloseCatchUp() { IsCatchUpOpen = false; IsEditorOpen = false; ClearSelection(); }

    private void MoveCatchUp(int delta) {
        catchUpIndex += delta;
        if (catchUpIndex < 0) {
            catchUpIndex = 0;
        }
        else if (catchUpIndex >= catchUpDates.Count) {
            CloseCatchUp();
            return;
        }

        OpenCatchUpDay();
    }

    private void OpenCatchUpDay() {
        if (!IsCatchUpOpen) {
            return;
        }

        DayItemViewModel? day = Days.FirstOrDefault(item => item.Date == catchUpDates[catchUpIndex]);
        OpenEditor(day);
        IsEditorOpen = false;
        OnPropertyChanged(nameof(CatchUpTitle));
        OnPropertyChanged(nameof(CatchUpProgress));
    }

    private void OpenSettings() {
        CopySettingsToForm();
        SettingsStatus = string.Empty;
        CurrentSettingsSection = SettingsSection.Employment;
        CurrentPage = settingsPage;
    }

    private void CloseSettings() {
        CopySettingsToForm();
        CurrentPage = monthWorkspacePage;
    }

    private void StartMonth() {
        IReadOnlyList<DateOnly> expectedDays = ExpectedHoursCalculator.GetExpectedWorkdays(selectedMonth.Year, selectedMonth.Month, settings.ExpectedHours, holidays);
        DateOnly targetDate = selectedMonth.Year == clock.Today.Year && selectedMonth.Month == clock.Today.Month
            ? clock.Today
            : expectedDays.FirstOrDefault(new DateOnly(selectedMonth.Year, selectedMonth.Month, 1));
        OpenEditor(Days.FirstOrDefault(day => day.Date == targetDate));
    }

    private void OpenReport() {
        if (IsMonthUnstarted) {
            return;
        }

        CurrentPage = monthWorkspacePage;
        IsReportOpen = true;
    }

    private async Task SaveSettingsAsync() {
        if (settings.IsConfigured && SelectedCurrency != settings.CurrencyPreference) {
            OnPropertyChanged(nameof(CurrencyChangeText));
            IsCurrencyRatePromptOpen = true;
            return;
        }

        await PersistSettingsAsync();
    }

    private async Task ConfirmCurrencyRateChangeAsync() {
        IsCurrencyRatePromptOpen = false;
        await PersistSettingsAsync();
    }

    private async Task PersistSettingsAsync() {
        try {
            bool wasSetup = IsSetupOpen;
            settings = CreateSettings(viewMode);
            await settingsRepository.SaveAsync(settings);
            await projects.EnsureDefaultAsync(settings.DefaultProject);
            localization.Apply(settings.LanguagePreference);
            themes.Apply(settings.ThemePreference);
            OnPropertyChanged(nameof(TaxModes));
            OnPropertyChanged(nameof(ThemePreferences));
            OnPropertyChanged(nameof(LanguagePreferences));
            IsSetupOpen = false;
            CurrentPage = wasSetup ? monthWorkspacePage : settingsPage;
            SettingsStatus = wasSetup ? string.Empty : localization.Get("SettingsSaved");
            ErrorText = string.Empty;
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            SettingsStatus = string.Empty;
            logger.LogError(exception, "Saving Tidverk settings failed");
            ErrorText = exception is ArgumentException ? exception.Message : localization.Get("SettingsSaveFailed");
        }
    }

    private void OpenBalanceAdjustment() {
        BalanceAdjustmentHours = MonthlyOpeningBalanceHours;
        ErrorText = string.Empty;
        IsBalanceAdjustmentOpen = true;
    }

    private void CloseBalanceAdjustment() {
        IsBalanceAdjustmentOpen = false;
        ErrorText = string.Empty;
    }

    private async Task SaveBalanceAdjustmentAsync() {
        try {
            int minutes = decimal.ToInt32(decimal.Round(BalanceAdjustmentHours * 60m, MidpointRounding.AwayFromZero));
            int suggestedOpeningBalance = await GetSuggestedOpeningBalanceAsync();
            MonthRecord current = await months.GetAsync(selectedMonth.Year, selectedMonth.Month, suggestedOpeningBalance);
            await months.SaveAsync(new MonthRecord(selectedMonth.Year, selectedMonth.Month, minutes, current.ExpectedMinutesOverride, true));
            IsBalanceAdjustmentOpen = false;
            ErrorText = string.Empty;
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Adjusting the starting balance for {Year}-{Month:D2} failed", selectedMonth.Year, selectedMonth.Month);
            ErrorText = localization.Get("ValidBalanceRequired");
        }
    }

    private Task SaveCurrentAsync() => IsSettingsPage
        ? SaveSettingsAsync()
        : IsEditorOpen
            ? SaveEntryAsync()
            : Task.CompletedTask;

    private AppSettings CreateSettings(MonthViewPreference preference) {
        TimeOnly start = TimeInput.Parse(DefaultStart);
        TimeOnly end = TimeInput.Parse(DefaultEnd);
        TaxSettings tax = SelectedTaxMode switch {
            TaxMode.PrimaryIncomeTaxTable => new(SelectedTaxMode, TaxYear, TaxTableNumber, TaxColumn),
            TaxMode.ManualMonthlyDeduction => new(SelectedTaxMode, manualMonthlyDeduction: ManualTaxValue),
            _ => new(SelectedTaxMode)
        };
        DayOfWeek[] weekdays = GetSelectedWeekdays();
        return new(
            EmployeeName,
            EmployerName,
            DefaultProject,
            new HourlySalary(HourlyRate),
            new ExpectedHoursSettings(ExpectedHoursPerDay, weekdays, true),
            start,
            end,
            new Minutes(DefaultLunch),
            tax,
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
    }

    private void CopySettingsToForm() {
        EmployeeName = settings.EmployeeName;
        EmployerName = settings.EmployerName;
        DefaultProject = settings.DefaultProject;
        HourlyRate = settings.HourlySalary.Amount;
        ExpectedHoursPerDay = settings.ExpectedHours.HoursPerWorkday;
        DefaultStart = settings.DefaultStartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        DefaultEnd = settings.DefaultEndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        DefaultLunch = settings.DefaultLunchMinutes.Value;
        OpeningBalanceMinutes = settings.OpeningBalanceMinutes;
        SelectedTaxMode = settings.TaxSettings.Mode;
        TaxYear = settings.TaxSettings.TaxYear == 0 ? 2026 : settings.TaxSettings.TaxYear;
        TaxTableNumber = settings.TaxSettings.TableNumber == 0 ? 33 : settings.TaxSettings.TableNumber;
        TaxColumn = settings.TaxSettings.Column == 0 ? 1 : settings.TaxSettings.Column;
        ManualTaxValue = settings.TaxSettings.ManualMonthlyDeduction ?? 0m;
        SelectedTheme = settings.ThemePreference;
        SelectedLanguage = settings.LanguagePreference;
        SelectedExportLanguage = settings.ExportLanguagePreference;
        SelectedCurrency = settings.CurrencyPreference;
        SelectedOvertimeMode = settings.OvertimeCompensation.Mode;
        OvertimePremiumPercent = settings.OvertimeCompensation.PremiumPercent;
        OvertimeDailyThresholdHours = settings.OvertimeCompensation.DailyThresholdHours;
        OvertimeRateBands.Clear();
        foreach (OvertimeRateBand band in settings.OvertimeCompensation.RateBands) {
            OvertimeRateBands.Add(OvertimeRateBandViewModel.FromDomain(band));
        }
        SelectedInterfaceScale = settings.InterfaceScalePercent;
        WorkMonday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Monday);
        WorkTuesday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Tuesday);
        WorkWednesday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Wednesday);
        WorkThursday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Thursday);
        WorkFriday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Friday);
        WorkSaturday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Saturday);
        WorkSunday = settings.ExpectedHours.WorkingWeekdays.Contains(DayOfWeek.Sunday);
        OnPropertyChanged(string.Empty);
    }

    private async Task ExportAsync() {
        if (summary is null) {
            return;
        }

        ReportExportRequest request = new(
            selectedMonth.Year,
            selectedMonth.Month,
            settings.EmployeeName,
            settings.EmployerName,
            monthEntries.Values.ToArray(),
            summary,
            settings.ExportLanguagePreference,
            settings.OvertimeCompensation.Mode,
            settings.OvertimeCompensation.DailyThresholdHours);
        string? path = await fileDialogs.ChooseExcelFileAsync(ExportFilename.Create(settings.EmployeeName, selectedMonth.Year, selectedMonth.Month));
        if (path is null) {
            return;
        }

        try {
            await ExcelReportExporter.ExportAsync(request, path);
            IsReportOpen = false;
        }
        catch (Exception exception) {
            logger.LogError(exception, "Excel export failed");
            ErrorText = localization.Get("ExportFailed");
        }
    }

    private async Task BackupAsync() {
        string? path = await backups.CreateAsync("manual");
        BackupStatus = path is null ? localization.Get("NoBackupDatabase") : localization.Format("BackupCreated", Path.GetFileName(path));
    }

    private async Task ChooseRestoreAsync() {
        pendingRestorePath = await fileDialogs.ChooseDatabaseFileAsync();
        IsRestoreConfirmationOpen = pendingRestorePath is not null;
    }

    private async Task ConfirmRestoreAsync() {
        if (pendingRestorePath is null) {
            return;
        }

        await backups.RestoreAsync(pendingRestorePath);
        IsRestoreConfirmationOpen = false;
        BackupStatus = localization.Get("RestoreDone");
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

    private string FormatSignedHours(int minutes) => $"{(minutes >= 0 ? "+" : "-")}{(Math.Abs(minutes) / 60m).ToString("0.0", localization.Culture)} h";

    private string FormatMoney(decimal value) => $"{value.ToString("N0", localization.Culture)} {settings.CurrencyPreference}";

    private string GetDailyPayText(WorkEntry entry) {
        if (entry.Status != WorkEntryStatus.Worked || summary is null) {
            return string.Empty;
        }

        decimal gross = SalaryCalculator.GrossSalary(entry, settings.ExpectedHours, settings.HourlySalary, settings.OvertimeCompensation, holidays);
        TaxEstimate estimate = taxes.Calculate(summary.GrossSalary, settings.TaxSettings);
        if (!estimate.IsAvailable || estimate.EstimatedNetPay is null || summary.GrossSalary <= 0m) {
            return FormatMoney(gross);
        }

        decimal net = estimate.EstimatedNetPay.Value * gross / summary.GrossSalary;
        return $"{FormatMoney(gross)} ({FormatMoney(net)})";
    }

    private string LocalizeTaxError(string? reason) => reason switch {
        "Manual monthly deduction is not configured." => localization.Get("TaxManualNotConfigured"),
        "Tax estimate unavailable for this year." => localization.Get("TaxYearUnavailable"),
        _ => localization.Get("Unavailable")
    };

    private string? LocalizeHoliday(string? name) => name switch {
        "New Year's Day" => localization.Get("HolidayNewYear"),
        "Epiphany" => localization.Get("HolidayEpiphany"),
        "Good Friday" => localization.Get("HolidayGoodFriday"),
        "Easter Sunday" => localization.Get("HolidayEasterSunday"),
        "Easter Monday" => localization.Get("HolidayEasterMonday"),
        "May Day" => localization.Get("HolidayMayDay"),
        "Ascension Day" => localization.Get("HolidayAscension"),
        "Whit Sunday" => localization.Get("HolidayWhitSunday"),
        "National Day" => localization.Get("HolidayNationalDay"),
        "Midsummer Day" => localization.Get("HolidayMidsummer"),
        "All Saints' Day" => localization.Get("HolidayAllSaints"),
        "Christmas Day" => localization.Get("HolidayChristmas"),
        "Boxing Day" => localization.Get("HolidayBoxing"),
        "Sunday" => localization.Get("HolidaySunday"),
        _ => name
    };

    private void ClearSelection() {
        SelectedDay = null;
        foreach (DayItemViewModel item in Days.Concat(CalendarDays)) {
            item.IsSelected = false;
        }
    }

    private DayOfWeek[] GetSelectedWeekdays() {
        List<DayOfWeek> days = [];
        if (WorkMonday) days.Add(DayOfWeek.Monday);
        if (WorkTuesday) days.Add(DayOfWeek.Tuesday);
        if (WorkWednesday) days.Add(DayOfWeek.Wednesday);
        if (WorkThursday) days.Add(DayOfWeek.Thursday);
        if (WorkFriday) days.Add(DayOfWeek.Friday);
        if (WorkSaturday) days.Add(DayOfWeek.Saturday);
        if (WorkSunday) days.Add(DayOfWeek.Sunday);
        return days.ToArray();
    }
}
