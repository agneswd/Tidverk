using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.App.Services;
using Tidverk.App.ViewModels;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.Tests;

public sealed class MainWindowViewModelTests {
    [Fact]
    public async Task Ledger_is_default_and_calendar_switch_preserves_month() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        DateOnly month = viewModel.SelectedMonth;

        Assert.True(viewModel.IsLedger);
        Assert.Equal(31, viewModel.Days.Count);

        await viewModel.ShowCalendarCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCalendar);
        Assert.Equal(month, viewModel.SelectedMonth);
        Assert.Equal(35, viewModel.CalendarDays.Count);
    }

    [Fact]
    public async Task Month_navigation_uses_actual_month_lengths() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(6, viewModel.SelectedMonth.Month);
        Assert.Equal(30, viewModel.Days.Count);
    }

    [Fact]
    public async Task Empty_month_is_unstarted_until_the_first_entry_is_opened() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsMonthUnstarted);
        Assert.False(viewModel.HasMissingDays);
        Assert.Equal("+0.0 h", viewModel.BalanceText);

        viewModel.StartMonthCommand.Execute(null);

        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal(new DateOnly(2026, 7, 31), viewModel.SelectedDay?.Date);
    }

    [Fact]
    public void Day_state_distinguishes_unstarted_today_future_and_missing() {
        DateOnly today = new(2026, 7, 3);
        DayItemViewModel unstarted = CreateDay(today.AddDays(-1), today, monthStarted: false);
        DayItemViewModel current = CreateDay(today, today, monthStarted: true);
        DayItemViewModel future = CreateDay(today.AddDays(1), today, monthStarted: true);
        DayItemViewModel missing = CreateDay(today.AddDays(-1), today, monthStarted: true);
        DayItemViewModel weekend = new(today.AddDays(1), WorkEntry.CreateIncomplete(today.AddDays(1)), true, null, false, today, true, EnglishLocalization());
        DayItemViewModel adjacentMonth = new(today.AddDays(-1), WorkEntry.CreateIncomplete(today.AddDays(-1)), false, null, true, today, true, EnglishLocalization());

        Assert.True(unstarted.IsUnstarted);
        Assert.Empty(unstarted.CalendarTimeText);
        Assert.True(current.IsToday);
        Assert.Equal("Today", current.CalendarTimeText);
        Assert.False(current.IsMissing);
        Assert.True(future.IsFuture);
        Assert.Equal("Upcoming", future.CalendarTimeText);
        Assert.False(future.IsMissing);
        Assert.True(missing.IsMissing);
        Assert.Equal("Missing entry", missing.CalendarTimeText);
        Assert.False(weekend.IsMissing);
        Assert.Empty(weekend.CalendarTimeText);
        Assert.False(adjacentMonth.IsMissing);
        Assert.Empty(adjacentMonth.CalendarTimeText);
    }

    [Fact]
    public async Task New_month_suggests_previous_month_closing_balance() {
        Fixture fixture = new();
        fixture.Entries.Items[new DateOnly(2026, 6, 1)] = WorkEntry.CreateWorked(
            new DateOnly(2026, 6, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        MonthRecord june = new(2026, 6, fixture.Settings.Value.OpeningBalanceMinutes);
        MonthlySummary expected = MonthlyCalculator.Calculate(
            june,
            fixture.Entries.Items.Values.Where(entry => entry.Date.Month == 6).ToArray(),
            fixture.Settings.Value.ExpectedHours,
            fixture.Settings.Value.HourlySalary,
            new DateOnly(2026, 7, 31),
            new SwedishHolidayService());
        Assert.Equal(expected.ClosingBalanceMinutes, viewModel.MonthlyOpeningBalance);
    }

    [Fact]
    public void Monthly_opening_balance_edits_hours_and_stores_minutes() {
        MainWindowViewModel viewModel = new();
        viewModel.MonthlyOpeningBalance = 300;

        Assert.Equal(5m, viewModel.MonthlyOpeningBalanceHours);

        viewModel.MonthlyOpeningBalanceHours = -1.25m;

        Assert.Equal(-75, viewModel.MonthlyOpeningBalance);
    }

    [Fact]
    public async Task Editor_rejects_invalid_shift_and_saves_valid_shift() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        DayItemViewModel day = viewModel.Days[0];
        viewModel.OpenEditorCommand.Execute(day);
        viewModel.EditorStart = "16:00";
        viewModel.EditorEnd = "08:00";

        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Empty(fixture.Entries.Items);

        viewModel.EditorStart = "8";
        viewModel.EditorEnd = "1630";
        viewModel.EditorLunch = 30;
        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.Equal(480, fixture.Entries.Items[day.Date].WorkedMinutes.Value);
        Assert.Equal("8.0 h", viewModel.WorkedText);
    }

    [Fact]
    public async Task Opening_editor_marks_the_same_day_selected_in_both_views() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        DayItemViewModel day = viewModel.Days[7];

        viewModel.OpenEditorCommand.Execute(day);

        Assert.True(day.IsSelected);
        Assert.True(viewModel.CalendarDays.Single(item => item.Date == day.Date).IsSelected);
        Assert.Single(viewModel.Days, item => item.IsSelected);
        Assert.Single(viewModel.CalendarDays, item => item.IsSelected);
    }

    [Fact]
    public async Task Closing_editor_clears_selection_in_both_views() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenEditorCommand.Execute(viewModel.Days[7]);

        viewModel.CloseEditorCommand.Execute(null);

        Assert.Null(viewModel.SelectedDay);
        Assert.DoesNotContain(viewModel.Days, item => item.IsSelected);
        Assert.DoesNotContain(viewModel.CalendarDays, item => item.IsSelected);
    }

    [Fact]
    public async Task Current_month_action_is_disabled_only_on_the_current_month() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsCurrentMonth);

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsCurrentMonth);

        await viewModel.TodayCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsCurrentMonth);
    }

    [Fact]
    public async Task Preferences_apply_swedish_currency_and_scale() {
        Fixture fixture = new();
        fixture.Settings.Value = new AppSettings(
            "Elias", "Employer", "Rungard", new HourlySalary(202m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), TaxSettings.Disabled,
            languagePreference: LanguagePreference.Swedish,
            currencyPreference: CurrencyPreference.EUR,
            interfaceScalePercent: 125,
            exportLanguagePreference: ExportLanguagePreference.English,
            overtimeCompensation: new OvertimeCompensationSettings(OvertimeCompensationMode.Paid, 75m));
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Rungard");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("sv-SE", fixture.Localization.Culture.Name);
        Assert.Equal(CurrencyPreference.EUR, viewModel.SelectedCurrency);
        Assert.Equal(ExportLanguagePreference.English, viewModel.SelectedExportLanguage);
        Assert.Equal(OvertimeCompensationMode.Paid, viewModel.SelectedOvertimeMode);
        Assert.Equal(75m, viewModel.OvertimePremiumPercent);
        Assert.Equal(1.25, viewModel.InterfaceScale);
        Assert.Equal("Redigera", viewModel.Days[0].ActionText);
        Assert.Contains("EUR", viewModel.Days[0].PayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Daily_pay_caps_paid_hours_at_the_normal_workday() {
        Fixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(17, 30), 30, "Rungard");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("9.0", viewModel.Days[0].HoursText);
        Assert.Equal("1,616 SEK (1,616 SEK)", viewModel.Days[0].PayText);
    }

    [Fact]
    public async Task Daily_pay_includes_paid_overtime_premium() {
        Fixture fixture = new();
        fixture.Settings.Value = new AppSettings(
            "Elias", "Employer", "Rungard", new HourlySalary(200m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), TaxSettings.Disabled,
            overtimeCompensation: new OvertimeCompensationSettings(OvertimeCompensationMode.Paid, 50m));
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(18, 30), 30, "Rungard");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("2,200 SEK (2,200 SEK)", viewModel.Days[0].PayText);
        Assert.Equal("Overtime paid with 50% premium", viewModel.GrossPayDescription);
    }

    [Fact]
    public async Task Manual_month_balance_carries_across_an_empty_month() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenBalanceAdjustmentCommand.Execute(null);
        viewModel.BalanceAdjustmentHours = 5m;

        await viewModel.SaveBalanceAdjustmentCommand.ExecuteAsync(null);
        await viewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.True(fixture.Months.Items[(2026, 7)].OpeningBalanceWasEdited);
        Assert.Equal(300, viewModel.MonthlyOpeningBalance);
    }

    [Fact]
    public async Task Catch_up_contains_past_expected_days_but_not_future_days() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.StartCatchUpCommand.Execute(null);

        Assert.True(viewModel.IsCatchUpOpen);
        Assert.False(viewModel.IsEditorOpen);
        Assert.Contains("1 of", viewModel.CatchUpProgress, StringComparison.Ordinal);
        Assert.DoesNotContain("31", viewModel.CatchUpTitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_settings_applies_and_persists_theme() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.SelectedTheme = ThemePreference.Dark;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(ThemePreference.Dark, fixture.Theme.Applied);
        Assert.Equal(ThemePreference.Dark, fixture.Settings.Value.ThemePreference);
    }

    [Fact]
    public async Task Changing_currency_prompts_for_hourly_rate_before_saving() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.SelectedCurrency = CurrencyPreference.EUR;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCurrencyRatePromptOpen);
        Assert.Equal(CurrencyPreference.SEK, fixture.Settings.Value.CurrencyPreference);

        viewModel.HourlyRate = 20m;
        await viewModel.ConfirmCurrencyRateChangeCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsCurrencyRatePromptOpen);
        Assert.Equal(CurrencyPreference.EUR, fixture.Settings.Value.CurrencyPreference);
        Assert.Equal(20m, fixture.Settings.Value.HourlyRate);
    }

    [Fact]
    public async Task Settings_is_a_page_and_workspace_navigation_leaves_it() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.OpenSetupCommand.Execute(null);

        Assert.True(viewModel.IsSettingsPage);
        Assert.False(viewModel.IsMonthWorkspace);
        Assert.False(viewModel.IsLedger);
        Assert.True(viewModel.IsEmploymentSettings);

        viewModel.ShowAppearanceSettingsCommand.Execute(null);
        Assert.True(viewModel.IsAppearanceSettings);

        viewModel.ShowDataSettingsCommand.Execute(null);
        Assert.True(viewModel.IsDataSettings);

        await viewModel.ShowCalendarCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSettingsPage);
        Assert.True(viewModel.IsMonthWorkspace);
        Assert.True(viewModel.IsCalendar);
    }

    [Fact]
    public async Task Save_current_uses_the_active_surface() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
        viewModel.EditorStart = "08:00";
        viewModel.EditorEnd = "16:30";

        await viewModel.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Days[0].Date, fixture.Entries.Items.Keys);

        viewModel.OpenSetupCommand.Execute(null);
        viewModel.SelectedTheme = ThemePreference.Dark;
        await viewModel.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Equal(ThemePreference.Dark, fixture.Settings.Value.ThemePreference);
    }

    [Fact]
    public async Task Escape_command_closes_the_topmost_dialog() {
        Fixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenReportCommand.Execute(null);

        viewModel.CloseTopCommand.Execute(null);

        Assert.False(viewModel.IsReportOpen);
    }

    private static DayItemViewModel CreateDay(DateOnly date, DateOnly today, bool monthStarted) =>
        new(date, WorkEntry.CreateIncomplete(date), true, null, true, today, monthStarted, EnglishLocalization());

    private static LocalizationService EnglishLocalization() {
        LocalizationService localization = new();
        localization.Apply(LanguagePreference.English);
        return localization;
    }

    private sealed class Fixture {
        public InMemoryWorkEntries Entries { get; } = new();
        public InMemorySettings Settings { get; } = new();
        public InMemoryMonths Months { get; } = new();
        public RecordingTheme Theme { get; } = new();
        public LocalizationService Localization { get; } = EnglishLocalization();

        public MainWindowViewModel CreateViewModel() => new(
            Entries,
            Settings,
            Months,
            new InMemoryProjects(),
            new SwedishHolidayService(),
            new FixedClock(),
            new TaxCalculator(),
            new NoFileDialog(),
            Localization,
            Theme,
            new AppPaths(Path.Combine(Path.GetTempPath(), $"tidverk-app-tests-{Guid.NewGuid():N}")),
            new DatabaseBackupService(new AppPaths(Path.Combine(Path.GetTempPath(), $"tidverk-app-tests-{Guid.NewGuid():N}"))),
            new NoDataFolder(),
            NullLogger<MainWindowViewModel>.Instance);
    }

    private sealed class FixedClock : IClock {
        public DateOnly Today => new(2026, 7, 31);
        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryWorkEntries : IWorkEntryRepository {
        public Dictionary<DateOnly, WorkEntry> Items { get; } = [];
        public Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkEntry>>(Items.Values.Where(item => item.Date.Year == year && item.Date.Month == month).ToArray());
        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(Items.GetValueOrDefault(date));
        public Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) { Items[entry.Date] = entry; return Task.CompletedTask; }
        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) { Items[date] = WorkEntry.CreateIncomplete(date); return Task.CompletedTask; }
    }

    private sealed class InMemorySettings : ISettingsRepository {
        public AppSettings Value { get; set; } = new(
            "Elias", "Employer", "Rungard", new HourlySalary(202m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), TaxSettings.Disabled);
        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) { Value = settings; return Task.CompletedTask; }
    }

    private sealed class InMemoryMonths : IMonthRepository {
        public Dictionary<(int Year, int Month), MonthRecord> Items { get; } = [];

        public Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault((year, month)) ?? new MonthRecord(year, month, suggestedOpeningBalance));

        public Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default) {
            Items[(month.Year, month.Month)] = month;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProjects : IProjectRepository {
        public Task<IReadOnlyList<Project>> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(new Project(Guid.NewGuid(), name, true, true));
    }

    private sealed class RecordingTheme : IThemeService {
        public ThemePreference Applied { get; private set; }
        public void Apply(ThemePreference preference) => Applied = preference;
    }

    private sealed class NoFileDialog : IFileDialogService {
        public Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class NoDataFolder : IDataFolderService {
        public void Open(string path) { }
    }
}
