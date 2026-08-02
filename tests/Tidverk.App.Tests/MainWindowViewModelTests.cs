using Tidverk.App.Services;
using Tidverk.App.ViewModels;
using Tidverk.Core;

namespace Tidverk.App.Tests;

public sealed class MainWindowViewModelTests {
    [Fact]
    public void Startup_failure_is_visible_to_the_user() {
        MainWindowViewModel viewModel = new ShellFixture().CreateViewModel();

        viewModel.ShowStartupFailure();

        Assert.True(viewModel.HasError);
        Assert.Equal("Tidverk could not start. See the local log for details.", viewModel.ErrorText);
    }

    [Fact]
    public async Task Ledger_is_default_and_calendar_switch_preserves_month() {
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(6, viewModel.SelectedMonth.Month);
        Assert.Equal(30, viewModel.Days.Count);
    }

    [Fact]
    public async Task A_late_month_load_cannot_replace_the_current_month() {
        ShellFixture fixture = new();
        DateOnly juneDate = new(2026, 6, 1);
        DateOnly julyDate = new(2026, 7, 1);
        fixture.Entries.Items[juneDate] = WorkEntry.CreateWorked(juneDate, new TimeOnly(8, 0), new TimeOnly(12, 0), 0);
        fixture.Entries.Items[julyDate] = WorkEntry.CreateWorked(julyDate, new TimeOnly(8, 0), new TimeOnly(16, 0), 0);
        DelayedWorkEntries delayed = new(fixture.Entries);
        MainWindowViewModel viewModel = fixture.CreateViewModel(workEntries: delayed);
        await viewModel.InitializeAsync();
        delayed.DelayNextQueryFor(2026, 6);

        Task previous = viewModel.PreviousMonthCommand.ExecuteAsync(null);
        await delayed.WaitUntilDelayedAsync();
        await viewModel.NextMonthCommand.ExecuteAsync(null);
        delayed.Release();
        await previous;

        Assert.Equal(new DateOnly(2026, 7, 1), viewModel.SelectedMonth);
        Assert.All(viewModel.Days, day => Assert.Equal(7, day.Date.Month));
        Assert.Equal(julyDate, viewModel.Days.Single(day => day.Entry.Status == WorkEntryStatus.Worked).Date);
    }

    [Fact]
    public async Task Empty_month_is_unstarted_until_the_first_entry_is_opened() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsMonthUnstarted);
        Assert.False(viewModel.HasMissingDays);
        Assert.Empty(viewModel.PayLines);
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
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        DayItemViewModel day = viewModel.Days[0];
        viewModel.OpenEditorCommand.Execute(day);
        viewModel.EditorStart = "08:00";
        viewModel.EditorEnd = "08:00";

        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Empty(fixture.Entries.Items);

        // An end before the start is a shift that runs past midnight, not a mistake.
        viewModel.EditorStart = "22:00";
        viewModel.EditorEnd = "06:00";
        viewModel.EditorLunch = 30;
        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.Equal(450, fixture.Entries.Items[day.Date].WorkedMinutes.Value);

        viewModel.OpenEditorCommand.Execute(day);
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
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
        fixture.Settings.Value = new AppSettings(
            "Alex", "Employer", "Route A", new HourlySalary(202m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), TaxSettings.Disabled,
            languagePreference: LanguagePreference.Swedish,
            currencyPreference: CurrencyPreference.EUR,
            interfaceScalePercent: 125,
            exportLanguagePreference: ExportLanguagePreference.English,
            overtimeCompensation: new OvertimeCompensationSettings(OvertimeCompensationMode.Paid, 75m));
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Route A");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("sv-SE", fixture.Localization.Culture.Name);
        Assert.Equal(CurrencyPreference.EUR, viewModel.SelectedCurrency);
        Assert.Equal(ExportLanguagePreference.English, viewModel.SelectedExportLanguage);
        Assert.Equal(
            new[] {
                ExportLanguagePreference.System,
                ExportLanguagePreference.English,
                ExportLanguagePreference.Swedish
            },
            viewModel.ExportLanguagePreferences);
        Assert.Equal(OvertimeCompensationMode.Paid, viewModel.SelectedOvertimeMode);
        Assert.Equal(75m, viewModel.OvertimePremiumPercent);
        Assert.Equal(1.25, viewModel.InterfaceScale);
        Assert.Equal("Redigera", viewModel.Days[0].ActionText);
        Assert.Contains("EUR", viewModel.Days[0].PayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Daily_pay_caps_paid_hours_at_the_normal_workday() {
        ShellFixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(17, 30), 30, "Route A");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("9.0", viewModel.Days[0].HoursText);
        Assert.Equal("1,616 SEK (1,616 SEK)", viewModel.Days[0].PayText);
    }

    [Fact]
    public async Task Daily_pay_includes_paid_overtime_premium() {
        ShellFixture fixture = new();
        fixture.Settings.Value = new AppSettings(
            "Alex", "Employer", "Route A", new HourlySalary(200m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), TaxSettings.Disabled,
            overtimeCompensation: new OvertimeCompensationSettings(OvertimeCompensationMode.Paid, 50m));
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(18, 30), 30, "Route A");
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal("2,200 SEK (2,200 SEK)", viewModel.Days[0].PayText);
        Assert.Equal(
            [("Ordinary", "1,600 SEK"), ("Overtime", "600 SEK")],
            viewModel.PayLines.Select(line => (line.Label, line.Amount)));
        Assert.Equal("Based on registered entries only.", viewModel.GrossPayNote);
        Assert.Equal("ORDINARY BALANCE", viewModel.TimeBalanceTitle);
        Assert.Equal("Paid overtime excluded", viewModel.TimeBalanceDescription);
    }

    [Fact]
    public async Task Monthly_salary_shows_base_pay_with_divisor_overtime_and_ob_breakdown() {
        ShellFixture fixture = new();
        ExpectedHoursSettings schedule = new(4m, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], true);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 72m,
            rateBands: [
                new(
                    "Simple overtime",
                    OvertimeDayCategory.ScheduledWeekdays,
                    new TimeOnly(6, 0),
                    new TimeOnly(20, 0),
                    0m,
                    rateType: CompensationRateType.FullTimeMonthlySalaryDivisor,
                    rateValue: 94m)
            ],
            thresholdMode: OvertimeThresholdMode.ScheduledHours,
            defaultRateType: CompensationRateType.FullTimeMonthlySalaryDivisor);
        fixture.Settings.Value = new AppSettings(
            "Alex",
            "Employer",
            "Route A",
            new HourlySalary(0m),
            schedule,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            Minutes.Zero,
            TaxSettings.Disabled,
            overtimeCompensation: compensation,
            salarySettings: new SalarySettings(SalaryType.Monthly, new HourlySalary(0m), 12_123m, 50m));
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(14, 0), 0, "Route A");
        fixture.Months.Items[(2026, 7)] = new MonthRecord(2026, 7, expectedMinutesOverride: 4 * 60);
        MainWindowViewModel viewModel = fixture.CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal(SalaryType.Monthly, viewModel.SelectedSalaryType);
        Assert.Equal("12,639 SEK", viewModel.GrossText);

        // Only the parts that earned something are listed, so no empty "OB 0 SEK" line appears.
        Assert.Equal(
            [("Monthly", "12,123 SEK"), ("Overtime", "516 SEK")],
            viewModel.PayLines.Select(line => (line.Label, line.Amount)));
        Assert.Equal("Full contracted month. Overtime and OB follow your entries.", viewModel.GrossPayNote);
        Assert.Equal("516 SEK", viewModel.Days[0].PayText);
    }

    [Fact]
    public async Task Manual_month_balance_carries_across_an_empty_month() {
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.SelectedTheme = ThemePreference.Dark;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(ThemePreference.Dark, fixture.Theme.Applied);
        Assert.Equal(ThemePreference.Dark, fixture.Settings.Value.ThemePreference);
    }

    [Fact]
    public async Task Saving_settings_preserves_a_schedule_that_includes_public_holidays() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.WorkMonday = false;
        viewModel.WorkTuesday = false;
        viewModel.WorkWednesday = false;
        viewModel.WorkThursday = false;
        viewModel.WorkFriday = false;
        viewModel.WorkSunday = true;
        viewModel.ExcludePublicHolidays = false;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.False(fixture.Settings.Value.ExpectedHours.ExcludePublicHolidays);
        Assert.Equal([DayOfWeek.Sunday], fixture.Settings.Value.ExpectedHours.WorkingWeekdays);
        Assert.All(
            viewModel.Days.Where(day => day.Date.DayOfWeek == DayOfWeek.Sunday),
            day => Assert.True(day.IsExpectedWorkday));
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("employer")]
    [InlineData("project")]
    public async Task Required_setup_identity_fields_are_checked_before_any_write(string missingField) {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        AppSettings original = fixture.Settings.Value;
        switch (missingField) {
            case "employee": viewModel.EmployeeName = " "; break;
            case "employer": viewModel.EmployerName = " "; break;
            case "project": viewModel.DefaultProject = " "; break;
        }

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Same(original, fixture.Settings.Value);
        Assert.Empty(fixture.Projects.DefaultsSet);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Default_break_must_be_shorter_than_the_default_shift_before_any_write() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        AppSettings original = fixture.Settings.Value;
        viewModel.DefaultLunch = 510;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Same(original, fixture.Settings.Value);
        Assert.Empty(fixture.Projects.DefaultsSet);
        Assert.Equal(
            "Enter a lunch duration from zero up to, but not including, the full default shift.",
            viewModel.ErrorText);
    }

    [Fact]
    public async Task Invalid_settings_do_not_expose_exception_details() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.HourlyRate = -1m;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal("Enter an hourly rate of zero or more.", viewModel.ErrorText);
    }

    [Fact]
    public async Task Switching_salary_type_preserves_incompatible_rules_until_the_user_reviews_them() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenSettingsCommand.Execute(null);

        // Loading stored settings must not report a change the user never made.
        Assert.Empty(viewModel.SettingsStatus);
        Assert.Equal(
            [CompensationRateType.HourlyPremiumPercent, CompensationRateType.FixedHourlyAmount],
            viewModel.CompensationRateTypes);

        viewModel.SelectedOvertimeMode = OvertimeCompensationMode.Paid;
        viewModel.AddOvertimeRateBandCommand.Execute(null);
        OvertimeRateBandViewModel rule = Assert.Single(viewModel.OvertimeRateBands);
        rule.RateType = CompensationRateType.HourlyPremiumPercent;

        viewModel.SelectedSalaryType = SalaryType.Monthly;

        // A percentage of an hourly rate cannot be paid from a monthly salary. Preserve its meaning
        // and value until the user chooses a replacement.
        Assert.Equal(
            [CompensationRateType.FixedHourlyAmount, CompensationRateType.FullTimeMonthlySalaryDivisor],
            viewModel.CompensationRateTypes);
        Assert.Equal(CompensationRateType.HourlyPremiumPercent, rule.RateType);
        Assert.Equal(50m, rule.RateValue);
        Assert.NotEmpty(viewModel.ErrorText);

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Equal(SalaryType.Hourly, fixture.Settings.Value.Salary.Type);

        rule.RateType = CompensationRateType.FixedHourlyAmount;
        rule.RateValue = 100m;
        viewModel.SelectedOvertimeDefaultRateType = CompensationRateType.FixedHourlyAmount;
        viewModel.OvertimePremiumPercent = 100m;
        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.Equal(SalaryType.Monthly, fixture.Settings.Value.Salary.Type);
    }

    [Fact]
    public async Task Changing_currency_prompts_for_hourly_rate_before_saving() {
        ShellFixture fixture = new();
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
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.OpenSettingsCommand.Execute(null);

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
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
        viewModel.EditorStart = "08:00";
        viewModel.EditorEnd = "16:30";

        await viewModel.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Days[0].Date, fixture.Entries.Items.Keys);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.SelectedTheme = ThemePreference.Dark;
        await viewModel.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Equal(ThemePreference.Dark, fixture.Settings.Value.ThemePreference);
    }

    [Fact]
    public async Task Escape_command_closes_the_topmost_dialog() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenReportCommand.Execute(null);

        viewModel.CloseTopCommand.Execute(null);

        Assert.False(viewModel.IsReportOpen);
    }

    [Fact]
    public async Task Tax_detail_fields_follow_the_selected_mode() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        List<string> changed = [];
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        viewModel.SelectedTaxMode = TaxMode.PrimaryIncomeTaxTable;

        Assert.True(viewModel.IsPrimaryIncomeTax);
        Assert.False(viewModel.IsManualTax);
        Assert.Contains(nameof(MainWindowViewModel.IsPrimaryIncomeTax), changed, StringComparer.Ordinal);

        viewModel.SelectedTaxMode = TaxMode.ManualMonthlyDeduction;

        Assert.False(viewModel.IsPrimaryIncomeTax);
        Assert.True(viewModel.IsManualTax);

        viewModel.SelectedTaxMode = TaxMode.SecondaryIncomeThirtyPercent;

        Assert.False(viewModel.IsPrimaryIncomeTax);
        Assert.False(viewModel.IsManualTax);
    }

    private static DayItemViewModel CreateDay(DateOnly date, DateOnly today, bool monthStarted) =>
        new(date, WorkEntry.CreateIncomplete(date), true, null, true, today, monthStarted, EnglishLocalization());

    private static LocalizationService EnglishLocalization() => ShellFixture.EnglishLocalization();

    private sealed class DelayedWorkEntries(IWorkEntryRepository inner) : IWorkEntryRepository {
        private TaskCompletionSource delayReached = NewSignal();
        private TaskCompletionSource release = NewSignal();
        private (int Year, int Month)? delayedMonth;

        public void DelayNextQueryFor(int year, int month) => delayedMonth = (year, month);

        public Task WaitUntilDelayedAsync() => delayReached.Task;

        public void Release() => release.TrySetResult();

        public async Task<IReadOnlyList<WorkEntry>> GetMonthAsync(
            int year,
            int month,
            CancellationToken cancellationToken = default) {
            if (delayedMonth == (year, month)) {
                delayedMonth = null;
                delayReached.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return await inner.GetMonthAsync(year, month, cancellationToken).ConfigureAwait(false);
        }

        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            inner.GetAsync(date, cancellationToken);

        public Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) =>
            inner.SaveAsync(entry, cancellationToken);

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            inner.ResetAsync(date, cancellationToken);

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

}
