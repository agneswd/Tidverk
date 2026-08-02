using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tidverk.App.Services;
using Tidverk.App.ViewModels;
using Tidverk.Core;

namespace Tidverk.App.Tests;

public sealed class HeadlessWindowTests {
    [AvaloniaFact]
    public void Main_window_loads_with_shadui_shell_and_all_workspaces() {
        MainWindow window = new(new MainWindowViewModel());
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ShadUI.Sidebar shellSidebar = window.FindControl<ShadUI.Sidebar>("ShellSidebar")!;
        Button sidebarToggle = window.FindControl<Button>("SidebarToggle")!;
        MainWindowViewModel viewModel = (MainWindowViewModel)window.DataContext!;
        Assert.NotNull(shellSidebar);
        Assert.Contains(window.GetLogicalDescendants(), control => control is Tidverk.App.Views.MonthWorkspaceView);
        Assert.NotNull(window.FindControl<ShadUI.Card>("DayEditor"));
        Assert.NotNull(window.Icon);
        Assert.IsAssignableFrom<ShadUI.Window>(window);
        Assert.True(window.GetLogicalDescendants().OfType<ShadUI.Card>().Count() >= 10);
        Assert.True(window.GetLogicalDescendants().OfType<ShadUI.SidebarItem>().Count() >= 3);

        sidebarToggle.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        AssertSidebarState(shellSidebar, viewModel, false, 64);

        viewModel.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Assert.Contains(window.GetLogicalDescendants(), control => control is Tidverk.App.Views.SettingsView);
        Assert.False(window.FindControl<StackPanel>("WorkspaceSidebarContent")!.IsVisible);
        Assert.True(window.FindControl<StackPanel>("SettingsSidebarContent")!.IsVisible);
        Assert.False(window.FindControl<Separator>("SettingsBackSeparator")!.IsVisible);
        Assert.True(window.GetLogicalDescendants().OfType<ShadUI.SidebarItem>()
            .Single(item => string.Equals(item.Route, "employment", StringComparison.Ordinal)).IsChecked);
        AssertSidebarState(shellSidebar, viewModel, false, 64);

        sidebarToggle.Command.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        AssertSidebarState(shellSidebar, viewModel, true, 232);
        Assert.True(window.FindControl<Separator>("SettingsBackSeparator")!.IsVisible);

        viewModel.CloseSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.True(window.FindControl<StackPanel>("WorkspaceSidebarContent")!.IsVisible);
        AssertSidebarState(shellSidebar, viewModel, true, 232);
        window.Close();
    }

    [AvaloniaFact]
    public void Invalid_numeric_input_shows_a_human_readable_error() {
        MainWindowViewModel viewModel = new ShellFixture().CreateViewModel();
        MainWindow window = new(viewModel);
        window.Show();
        viewModel.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        TextBox hourlyRate = window.GetVisualDescendants().OfType<TextBox>()
            .Single(control => string.Equals(control.Name, "HourlyRateBox", StringComparison.Ordinal));
        hourlyRate.Focus();
        window.KeyTextInput("s");
        Dispatcher.UIThread.RunJobs();

        object error = Assert.Single(DataValidationErrors.GetErrors(hourlyRate)!);
        Assert.Equal("Enter a valid value.", error);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Gross_pay_card_itemises_overtime_and_ob_and_stays_quiet_without_them() {
        ThemeVariant? originalTheme = Application.Current?.RequestedThemeVariant;
        try {
            ShellFixture fixture = new();
            MainWindowViewModel plain = fixture.CreateViewModel();
            await plain.InitializeAsync();

            // Ordinary hourly work is the whole of gross pay, so a one-line breakdown is suppressed.
            Assert.Empty(plain.PayLines);
            Assert.False(plain.HasPayLines);

            ShellFixture paid = PaidOvertimeWithObFixture();
            MainWindowViewModel viewModel = paid.CreateViewModel();
            MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
            window.Show();
            await viewModel.InitializeAsync();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            Assert.Equal(
                ["Ordinary", "Overtime", "OB"],
                viewModel.PayLines.Select(line => line.Label));
            Assert.True(viewModel.HasPayLines);
            Assert.True(viewModel.HasWorkedBreakdown);

            ItemsControl lines = window.GetVisualDescendants().OfType<ItemsControl>()
                .Single(control => string.Equals(control.Name, "PayBreakdownLines", StringComparison.Ordinal));
            Assert.True(lines.IsVisible);
            Assert.Equal(3, lines.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text?.Contains("SEK", StringComparison.Ordinal) == true));

            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            SaveOptionalSnapshot(window, "gross-pay-breakdown-light.png");
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            SaveOptionalSnapshot(window, "gross-pay-breakdown-dark.png");

            // The window's own minimum width must still lay the four metric cards out without clipping.
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            window.Width = window.MinWidth;
            SaveOptionalSnapshot(window, "gross-pay-breakdown-narrow-light.png");
            Assert.All(
                lines.GetVisualDescendants().OfType<TextBlock>(),
                text => Assert.True(text.Bounds.Width > 0, "A breakdown line collapsed to zero width."));
            window.Close();
        }
        finally {
            if (Application.Current is not null) {
                Application.Current.RequestedThemeVariant = originalTheme;
            }
        }
    }

    /// <summary>A paid-overtime month with an evening OB rule and one long day that triggers both.</summary>
    private static ShellFixture PaidOvertimeWithObFixture() {
        ShellFixture fixture = new();
        Tidverk.Core.OvertimeCompensationSettings compensation = new(
            Tidverk.Core.OvertimeCompensationMode.Paid,
            premiumPercent: 50m,
            rateBands: [
                new(
                    "Evening OB",
                    Tidverk.Core.OvertimeDayCategory.AllDays,
                    new TimeOnly(18, 0),
                    new TimeOnly(22, 0),
                    premiumPercent: 0m,
                    compensationType: Tidverk.Core.CompensationRuleType.Ob,
                    rateType: Tidverk.Core.CompensationRateType.FixedHourlyAmount,
                    rateValue: 45m)
            ],
            obOvertimeCombination: Tidverk.Core.ObOvertimeCombinationMode.IncludeOb);
        fixture.Settings.Value = new Tidverk.Core.AppSettings(
            "Alex", "Employer", "Route A",
            new Tidverk.Core.HourlySalary(200m),
            Tidverk.Core.ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Tidverk.Core.Minutes(30),
            Tidverk.Core.TaxSettings.Disabled,
            overtimeCompensation: compensation);
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = Tidverk.Core.WorkEntry.CreateWorked(
            date, new TimeOnly(8, 0), new TimeOnly(20, 0), 30, "Route A");
        return fixture;
    }

    [AvaloniaFact]
    public void Compensation_validation_keeps_sibling_inputs_compact_and_uses_swedish_text() {
        ShellFixture fixture = new();
        fixture.Localization.Apply(Tidverk.Core.LanguagePreference.Swedish);
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        MainWindow window = new(viewModel) { Width = 1200, Height = 1000 };
        window.Show();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.SelectedOvertimeMode = Tidverk.Core.OvertimeCompensationMode.Paid;

        // A divisor rule is only offered once the salary type can actually pay it.
        viewModel.SelectedSalaryType = Tidverk.Core.SalaryType.Monthly;
        viewModel.AddOvertimeRateBandCommand.Execute(null);
        OvertimeRateBandViewModel rule = Assert.Single(viewModel.OvertimeRateBands);
        rule.RateType = Tidverk.Core.CompensationRateType.FullTimeMonthlySalaryDivisor;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Control[] ruleEditors = [.. window.GetVisualDescendants().OfType<Control>()
            .Where(control => ReferenceEquals(control.DataContext, rule))];
        TextBox end = ruleEditors.OfType<TextBox>()
            .Single(control => Grid.GetRow(control) == 1 && Grid.GetColumn(control) == 1);
        ComboBox rateType = ruleEditors.OfType<ComboBox>()
            .Single(control => Grid.GetRow(control) == 1 && Grid.GetColumn(control) == 2);
        ComboBox ruleType = ruleEditors.OfType<ComboBox>()
            .Single(control => Grid.GetRow(control) == 0 && Grid.GetColumn(control) == 0);
        TextBox rateValue = ruleEditors.OfType<TextBox>()
            .Single(control => Grid.GetRow(control) == 1 && Grid.GetColumn(control) == 3);
        double endHeight = end.Bounds.Height;
        double rateTypeHeight = rateType.Bounds.Height;

        rateValue.Focus();
        rateValue.SelectAll();
        window.KeyTextInput("s");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Assert.Equal("Ange ett giltigt värde.", Assert.Single(DataValidationErrors.GetErrors(rateValue)!));
        Assert.Equal(endHeight, end.Bounds.Height);
        Assert.Equal(rateTypeHeight, rateType.Bounds.Height);
        Assert.Contains(rateType.GetVisualDescendants().OfType<TextBlock>(),
            text => string.Equals(text.Text, "Heltidslön / delningstal", StringComparison.Ordinal));
        Assert.Contains(ruleType.GetVisualDescendants().OfType<TextBlock>(),
            text => string.Equals(text.Text, "Övertid", StringComparison.Ordinal));

        // The value field names its own unit, so "94" cannot be misread as kronor.
        Assert.Contains(ruleEditors.OfType<TextBlock>().Concat(window.GetVisualDescendants().OfType<TextBlock>()),
            text => string.Equals(text.Text, "Delningstal", StringComparison.Ordinal));
        SaveOptionalSnapshot(window, "compensation-validation-swedish.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Update_progress_appears_above_settings_and_ready_state_shows_the_restart_notice() {
        UpdateService updates = new();
        SetPrivateProperty(updates, nameof(UpdateService.Status), UpdateStatus.Downloading);
        SetPrivateProperty(updates, nameof(UpdateService.DownloadProgress), 42);
        SetPrivateProperty(updates, nameof(UpdateService.AvailableVersion), "0.2.1");
        MainWindowViewModel viewModel = new ShellFixture().CreateViewModel(updates);
        MainWindow window = new(viewModel);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Grid updatePill = window.FindControl<Grid>("UpdateSidebarPill")!;
        Border downloadingPill = window.FindControl<Border>("UpdateDownloadingPill")!;
        Border progressTrack = window.FindControl<Border>("UpdateProgressTrack")!;
        ShadUI.SidebarItem settings = window.GetVisualDescendants().OfType<ShadUI.SidebarItem>()
            .Single(item => string.Equals(item.Route, "settings", StringComparison.Ordinal));
        Assert.True(updatePill.IsVisible);
        Assert.True(downloadingPill.ClipToBounds);
        Assert.True(progressTrack.ClipToBounds);
        Assert.Equal(new CornerRadius(2), progressTrack.CornerRadius);
        Assert.True(updatePill.Bounds.Top < settings.Bounds.Top);
        Assert.Equal(42, window.GetVisualDescendants().OfType<ProgressBar>().Single().Value);
        SaveOptionalSnapshot(window, "update-downloading-light.png");
        ThemeVariant? originalTheme = Application.Current?.RequestedThemeVariant;
        try {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            SaveOptionalSnapshot(window, "update-downloading-dark.png");
        }
        finally {
            Application.Current!.RequestedThemeVariant = originalTheme;
        }

        SetPrivateProperty(updates, nameof(UpdateService.Status), UpdateStatus.Ready);
        SetPrivateProperty(updates, nameof(UpdateService.IsReadyNotificationVisible), true);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Assert.True(window.FindControl<Button>("UpdateReadyPill")!.IsVisible);
        Assert.True(window.FindControl<ShadUI.Card>("UpdateReadyNotification")!.IsVisible);
        SaveOptionalSnapshot(window, "update-ready-light.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Wide_workspace_uses_a_centered_max_width_container() {
        MainWindow window = new(new MainWindowViewModel()) { Width = 2048, Height = 1200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Tidverk.App.Views.MonthWorkspaceView view = window.GetVisualDescendants().OfType<Tidverk.App.Views.MonthWorkspaceView>().Single();
        Grid content = view.FindControl<Grid>("WorkspaceContent")!;
        Assert.Equal(1440, content.Bounds.Width);
        Assert.InRange(Math.Abs(content.Bounds.X - ((view.Bounds.Width - content.Bounds.Width) / 2)), 0, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Metric_cards_use_consistent_top_right_icons() {
        MainWindow window = new(new MainWindowViewModel()) { Width = 1200, Height = 820 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        PathIcon[] icons = [.. window.GetVisualDescendants().OfType<PathIcon>().Where(icon => icon.Classes.Contains("metric-icon"))];
        Assert.Equal(4, icons.Length);
        Assert.All(icons, icon => Assert.Equal((16d, 16d), (icon.Width, icon.Height)));
        AssertIconCentered(window, "BalanceAdjustmentButton");
        AssertGlyphSize(window, "BalanceAdjustmentButton", 12);
        Assert.Equal(default, window.GetVisualDescendants().OfType<Button>().Single(button => string.Equals(button.Name, "BalanceAdjustmentButton", StringComparison.Ordinal)).Margin);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Ui_surfaces_render_to_headless_snapshots_when_requested() {
        string? outputDirectory = Environment.GetEnvironmentVariable("TIDVERK_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory)) {
            return;
        }

        Directory.CreateDirectory(outputDirectory);
        ThemeVariant? originalTheme = Application.Current?.RequestedThemeVariant;
        try {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            MainWindowViewModel viewModel = new();
            MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
            window.Show();

            SaveWorkspaceSnapshots(window, outputDirectory);
            SaveCollapsedSidebarSnapshot(window, viewModel, outputDirectory);
            await viewModel.ShowCalendarCommand.ExecuteAsync(null);
            SaveSnapshot(window, outputDirectory, "calendar-light.png");

            viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
            SaveSnapshot(window, outputDirectory, "editor-light.png");
            viewModel.CloseEditorCommand.Execute(null);

            SaveSettingsSnapshots(window, viewModel, outputDirectory);

            viewModel.OpenBalanceAdjustmentCommand.Execute(null);
            SaveSnapshot(window, outputDirectory, "balance-adjustment-light.png");
            viewModel.CloseBalanceAdjustmentCommand.Execute(null);

            SaveScaledLedgerSnapshot(window, viewModel, outputDirectory);

            SetPrivateBoolean(viewModel, nameof(MainWindowViewModel.IsSetupOpen), true);
            SaveSnapshot(window, outputDirectory, "setup-light.png");
            SetPrivateBoolean(viewModel, nameof(MainWindowViewModel.IsSetupOpen), false);

            viewModel.StartCatchUpCommand.Execute(null);
            SaveSnapshot(window, outputDirectory, "catch-up-light.png");
            viewModel.CloseCatchUpCommand.Execute(null);

            viewModel.OpenReportCommand.Execute(null);
            SaveSnapshot(window, outputDirectory, "report-light.png");
            viewModel.CloseReportCommand.Execute(null);

            SaveResetMonthSnapshot(window, viewModel, outputDirectory);

            await SaveUnstartedMonthSnapshot(window, viewModel, outputDirectory);
            await viewModel.ShowLedgerCommand.ExecuteAsync(null);
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            SaveSnapshot(window, outputDirectory, "ledger-dark.png");
            window.Close();
        }
        finally {
            if (Application.Current is not null) {
                Application.Current.RequestedThemeVariant = originalTheme;
            }
        }
    }

    [AvaloniaFact]
    public void Icon_only_buttons_center_their_icons() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        AssertIconCentered(window, "PreviousMonthButton");
        AssertIconCentered(window, "NextMonthButton");
        AssertIconCentered(window, "SidebarToggle");
        AssertGlyphSize(window, "PreviousMonthButton", 10);
        AssertGlyphSize(window, "NextMonthButton", 10);
        AssertGlyphSize(window, "SidebarToggle", 14);

        viewModel.ToggleSidebarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, window.FindControl<PathIcon>("SidebarToggleGlyph")!.Opacity);

        viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        AssertIconCentered(window, "CloseEditorButton");
        AssertGlyphSize(window, "CloseEditorButton", 16);
        AssertButtonHasSingleIcon(window, "NormalDayButton");
        AssertButtonHasSingleIcon(window, "CopyPreviousButton");
        AssertButtonHasSingleIcon(window, "CopyLastWeekButton");
        AssertButtonHasSingleIcon(window, "ResetEntryButton");
        AssertGlyphSize(window, "NormalDayButton", 16);
        AssertGlyphSize(window, "CopyPreviousButton", 16);
        AssertGlyphSize(window, "CopyLastWeekButton", 16);
        AssertGlyphSize(window, "ResetEntryButton", 16);
        viewModel.CloseEditorCommand.Execute(null);
        viewModel.OpenBalanceAdjustmentCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AssertIconCentered(window, "CloseBalanceAdjustmentButton");
        AssertGlyphSize(window, "CloseBalanceAdjustmentButton", 16);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Calendar_day_backgrounds_do_not_change_for_today_or_selection() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        try {
            await viewModel.ShowCalendarCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            DayItemViewModel today = viewModel.CalendarDays.Single(day => day.IsToday);
            DayItemViewModel ordinaryWeekday = viewModel.CalendarDays.First(day => day.IsInMonth && !day.IsToday && !day.IsWeekend);
            DayItemViewModel selectedWeekend = viewModel.CalendarDays.First(day => day.IsInMonth && day.IsWeekend);
            DayItemViewModel ordinaryWeekend = viewModel.CalendarDays.Last(day => day.IsInMonth && day.IsWeekend);
            Button FindCell(DayItemViewModel day) => window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("calendar-cell") && ReferenceEquals(button.DataContext, day));

            Assert.Equal(FindCell(ordinaryWeekday).Background, FindCell(today).Background);
            viewModel.OpenEditorCommand.Execute(selectedWeekend);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(FindCell(ordinaryWeekend).Background, FindCell(selectedWeekend).Background);
        }
        finally {
            viewModel.CloseEditorCommand.Execute(null);
            await viewModel.ShowLedgerCommand.ExecuteAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Combo_box_popups_inherit_interface_scale_and_disable_the_native_host_shadow() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.ShowEmploymentSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        ComboBox comboBox = window.GetLogicalDescendants().OfType<ComboBox>().Single(control => string.Equals(control.Name, "TaxModeComboBox", StringComparison.Ordinal));
        Popup popup = comboBox.GetVisualDescendants().OfType<Popup>().Single();
        HyperlinkButton taxGuide = window.GetVisualDescendants().OfType<HyperlinkButton>().Single(control => string.Equals(control.Name, "TaxTableGuideLink", StringComparison.Ordinal));

        Assert.False(popup.ShouldUseOverlayLayer);
        Assert.True(popup.InheritsTransform);
        Assert.False(popup.WindowManagerAddShadowHint);
        Uri navigateUri = Assert.IsType<Uri>(taxGuide.NavigateUri);
        Assert.Equal("www.skatteverket.se", navigateUri.Host);
        window.Close();
    }

    [AvaloniaFact]
    public void Data_surfaces_clip_content_to_their_rounded_template_border() {
        MainWindow window = new(new MainWindowViewModel()) { Width = 1200, Height = 820 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        ShadUI.Card ledger = window.GetVisualDescendants().OfType<ShadUI.Card>()
            .Single(control => string.Equals(control.Name, "LedgerView", StringComparison.Ordinal));
        Assert.Contains(ledger.GetVisualDescendants().OfType<Border>(), border =>
            border.ClipToBounds && border.CornerRadius == ledger.CornerRadius);
        window.Close();
    }

    [AvaloniaFact]
    public void Ledger_rows_only_open_the_day_editor_from_action_buttons() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        IReadOnlyList<Button> buttons = window.GetLogicalDescendants().OfType<Button>().ToArray();
        Assert.DoesNotContain(buttons, control => control.Classes.Contains("ledger-hit"));
        Button action = buttons.First(control => control.Classes.Contains("ledger-action"));
        DayItemViewModel day = Assert.IsType<DayItemViewModel>(action.CommandParameter);
        action.Command!.Execute(day);

        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal(day.Date, viewModel.SelectedDay?.Date);
        window.Close();
    }

    [AvaloniaFact]
    public void Current_month_scale_sticky_header_and_compact_progress_are_rendered_from_state() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Button currentMonth = window.GetVisualDescendants().OfType<Button>()
            .Single(control => string.Equals(control.Name, "CurrentMonthButton", StringComparison.Ordinal));
        Assert.False(currentMonth.IsEnabled);

        ScrollViewer rows = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(control => string.Equals(control.Name, "LedgerRowsScroll", StringComparison.Ordinal));
        Border header = window.GetVisualDescendants().OfType<Border>()
            .Single(control => string.Equals(control.Name, "LedgerHeader", StringComparison.Ordinal));
        Assert.Same(rows.GetVisualParent(), header.GetVisualParent());
        Assert.Equal(1, Grid.GetRow(rows));
        Assert.Equal(0, Grid.GetRow(header));

        viewModel.StartCatchUpCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Border badge = window.FindControl<Border>("CatchUpProgressBadge")!;
        Assert.InRange(badge.Bounds.Height, 20, 32);
        Assert.InRange(badge.GetVisualDescendants().OfType<TextBlock>().Single().FontSize, 11, 13);

        viewModel.SelectedInterfaceScale = 125;
        Dispatcher.UIThread.RunJobs();
        LayoutTransformControl scaleRoot = window.FindControl<LayoutTransformControl>("InterfaceScaleRoot")!;
        ScaleTransform scale = Assert.IsType<ScaleTransform>(scaleRoot.LayoutTransform);
        Assert.Equal(1.25, scale.ScaleX);
        Assert.Equal(1.25, scale.ScaleY);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Calendar_fits_every_cell_without_an_internal_scroller_at_minimum_size_and_high_scale() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        MainWindow window = new(viewModel) { Width = 980, Height = 660 };
        window.Show();
        await viewModel.InitializeAsync();
        await viewModel.ShowCalendarCommand.ExecuteAsync(null);
        viewModel.SelectedInterfaceScale = 150;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.False(viewModel.IsSidebarExpanded);
        Assert.False(viewModel.ShowsCalendarMetrics);

        ShadUI.Card calendar = window.GetVisualDescendants().OfType<ShadUI.Card>()
            .Single(control => string.Equals(control.Name, "CalendarView", StringComparison.Ordinal));
        Button[] cells = calendar.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("calendar-cell"))
            .ToArray();
        Assert.Equal(viewModel.CalendarDays.Count, cells.Length);
        ItemsControl items = calendar.GetVisualDescendants().OfType<ItemsControl>()
            .Single(control => string.Equals(control.Name, "CalendarDaysItems", StringComparison.Ordinal));
        Assert.IsType<Grid>(items.GetVisualParent());
        Assert.Equal(1, Grid.GetRow(items));
        Assert.All(cells, cell => {
            Point topLeft = cell.TranslatePoint(default, items)!.Value;
            Point bottomRight = cell.TranslatePoint(new Point(cell.Bounds.Width, cell.Bounds.Height), items)!.Value;
            Assert.True(topLeft.X >= -0.5 && topLeft.Y >= -0.5, $"Calendar cell started outside the card: {topLeft}.");
            Assert.True(bottomRight.X <= items.Bounds.Width + 2.5, $"Calendar cell exceeded the card width: {bottomRight}.");
            Assert.True(bottomRight.Y <= items.Bounds.Height + 2.5, $"Calendar cell exceeded the card height: {bottomRight}.");
        });
        Point calendarBottomRight = calendar.TranslatePoint(new Point(calendar.Bounds.Width, calendar.Bounds.Height), window)!.Value;
        Assert.True(calendarBottomRight.X <= window.ClientSize.Width + 0.5, $"Calendar exceeded the window width: {calendarBottomRight}.");
        Assert.True(calendarBottomRight.Y <= window.ClientSize.Height + 0.5, $"Calendar exceeded the window height: {calendarBottomRight}.");
        SaveOptionalSnapshot(window, "calendar-scale-150-min-light.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Settings_header_is_fixed_and_baseline_fields_share_a_height() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Border header = window.GetVisualDescendants().OfType<Border>()
            .Single(control => string.Equals(control.Name, "SettingsHeader", StringComparison.Ordinal));
        Grid page = window.GetVisualDescendants().OfType<Grid>()
            .Single(control => string.Equals(control.Name, "SettingsPageBackground", StringComparison.Ordinal));
        ScrollViewer scroll = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(control => string.Equals(control.Name, "SettingsScroll", StringComparison.Ordinal));
        Assert.Same(header.GetVisualParent(), scroll.GetVisualParent());
        Assert.Equal(0, Grid.GetRow(header));
        Assert.Equal(1, Grid.GetRow(scroll));
        Assert.Equal(page.Background, header.Background);

        TextBox hourlyRate = window.GetVisualDescendants().OfType<TextBox>()
            .Single(control => string.Equals(control.Name, "HourlyRateBox", StringComparison.Ordinal));
        ComboBox salaryType = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(control => string.Equals(control.Name, "SalaryTypeComboBox", StringComparison.Ordinal));
        Assert.Equal(salaryType.Bounds.Height, hourlyRate.Bounds.Height, precision: 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Changing_language_recreates_visible_combo_box_text_without_reopening_settings() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.ShowAppearanceSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        ComboBox language = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(control => string.Equals(control.Name, "LanguageComboBox", StringComparison.Ordinal));
        viewModel.SelectedLanguage = LanguagePreference.Swedish;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.Equal(LanguagePreference.Swedish, language.SelectedItem);
        TextBlock[] swedishText = language.GetVisualDescendants().OfType<TextBlock>().ToArray();
        Assert.True(
            swedishText.Any(text => string.Equals(text.Text, "Svenska", StringComparison.Ordinal)),
            string.Join(" | ", swedishText.Select(text => text.Text)));

        viewModel.SelectedLanguage = LanguagePreference.English;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.Contains(language.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "English", StringComparison.Ordinal));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("Ledig", StringComparison.Ordinal) == true);
        window.Close();
    }

    [AvaloniaFact]
    public void Report_metrics_use_a_line_height_that_does_not_clip_20_pixel_text() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 980, Height = 660 };
        ThemeVariant? originalTheme = Application.Current?.RequestedThemeVariant;
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        window.Show();
        viewModel.SelectedLanguage = LanguagePreference.Swedish;
        viewModel.OpenReportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        TextBlock[] metrics = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(control => control.IsVisible && control.FontSize == 20 && control.FontWeight == FontWeight.SemiBold)
            .ToArray();
        Assert.Equal(4, metrics.Length);
        Assert.All(metrics, metric => Assert.Equal(28, metric.LineHeight));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
            label => label.IsVisible && string.Equals(label.Text, "NETTO UPPSK.", StringComparison.Ordinal));
        SaveOptionalSnapshot(window, "report-swedish-min-dark.png");
        window.Close();
        Application.Current.RequestedThemeVariant = originalTheme;
    }

    private static void SaveSnapshot(Window window, string directory, string filename) {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string path = Path.Combine(directory, filename);
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(path).Length > 0, $"Snapshot was empty: {path}");
    }

    private static void SaveSettingsSnapshots(MainWindow window, MainWindowViewModel viewModel, string outputDirectory) {
        viewModel.OpenSettingsCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "settings-light.png");
        viewModel.SelectedOvertimeMode = Tidverk.Core.OvertimeCompensationMode.Paid;
        viewModel.AddOvertimeRateBandCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "settings-tax-light.png");
        viewModel.ShowAppearanceSettingsCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "settings-appearance-light.png");
        viewModel.ShowDataSettingsCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "settings-data-light.png");
        viewModel.CloseSettingsCommand.Execute(null);
    }

    private static void AssertSidebarState(ShadUI.Sidebar sidebar, MainWindowViewModel viewModel, bool expanded, double width) {
        Assert.Equal(expanded, viewModel.IsSidebarExpanded);
        Assert.Equal(expanded, sidebar.Expanded);
        Assert.Equal(width, sidebar.Width);
    }

    private static void SaveCollapsedSidebarSnapshot(MainWindow window, MainWindowViewModel viewModel, string outputDirectory) {
        ShadUI.Sidebar shellSidebar = window.FindControl<ShadUI.Sidebar>("ShellSidebar")!;
        shellSidebar.CollapseAnimationDuration = 0;
        shellSidebar.ExpandAnimationDuration = 0;
        viewModel.IsSidebarExpanded = false;
        SaveSnapshot(window, outputDirectory, "sidebar-collapsed-light.png");
        viewModel.OpenSettingsCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "settings-collapsed-light.png");
        viewModel.CloseSettingsCommand.Execute(null);
        viewModel.IsSidebarExpanded = true;
    }

    private static void SaveWorkspaceSnapshots(MainWindow window, string outputDirectory) {
        SaveSnapshot(window, outputDirectory, "ledger-light.png");
        window.Width = 2048;
        window.Height = 1200;
        SaveSnapshot(window, outputDirectory, "ledger-wide-light.png");
        window.Width = 1200;
        window.Height = 820;
    }

    private static async Task SaveUnstartedMonthSnapshot(MainWindow window, MainWindowViewModel viewModel, string outputDirectory) {
        await viewModel.PreviousMonthCommand.ExecuteAsync(null).ConfigureAwait(true);
        SaveSnapshot(window, outputDirectory, "unstarted-month-light.png");
        await viewModel.TodayCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private static void SaveResetMonthSnapshot(MainWindow window, MainWindowViewModel viewModel, string outputDirectory) {
        viewModel.OpenResetMonthConfirmationCommand.Execute(null);
        SaveSnapshot(window, outputDirectory, "reset-month-light.png");
        viewModel.CancelMonthActionCommand.Execute(null);
    }

    private static void SaveScaledLedgerSnapshot(MainWindow window, MainWindowViewModel viewModel, string outputDirectory) {
        viewModel.SelectedInterfaceScale = 125;
        SaveSnapshot(window, outputDirectory, "ledger-scale-125-light.png");
        viewModel.SelectedInterfaceScale = 100;
    }

    private static void SetPrivateBoolean(MainWindowViewModel viewModel, string propertyName, bool value) {
        typeof(MainWindowViewModel).GetProperty(propertyName)!.SetValue(viewModel, value);
    }

    private static void SetPrivateProperty<T>(UpdateService service, string propertyName, T value) =>
        typeof(UpdateService).GetProperty(propertyName)!.SetValue(service, value);

    private static void SaveOptionalSnapshot(MainWindow window, string fileName) {
        string? outputDirectory = Environment.GetEnvironmentVariable("TIDVERK_SNAPSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(outputDirectory)) {
            Directory.CreateDirectory(outputDirectory);
            SaveSnapshot(window, outputDirectory, fileName);
        }
    }

    private static void AssertIconCentered(Window window, string buttonName) {
        Button button = window.FindControl<Button>(buttonName) ??
            window.GetVisualDescendants().OfType<Button>().Single(control => string.Equals(control.Name, buttonName, StringComparison.Ordinal));
        Control icon = GetButtonGlyph(button);
        Point center = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), button)!.Value;

        double horizontalOffset = Math.Abs(center.X - (button.Bounds.Width / 2));
        double verticalOffset = Math.Abs(center.Y - (button.Bounds.Height / 2));
        Assert.True(horizontalOffset <= 0.5, $"{buttonName} horizontal offset was {horizontalOffset}; button={button.Bounds}, glyph={icon.Bounds}, center={center}.");
        Assert.True(verticalOffset <= 0.5, $"{buttonName} vertical offset was {verticalOffset}; button={button.Bounds}, glyph={icon.Bounds}, center={center}.");
    }

    private static void AssertGlyphSize(Window window, string buttonName, double expectedSize) {
        Button button = window.FindControl<Button>(buttonName) ??
            window.GetVisualDescendants().OfType<Button>().Single(control => string.Equals(control.Name, buttonName, StringComparison.Ordinal));
        Control glyph = GetButtonGlyph(button);
        Point topLeft = glyph.TranslatePoint(new Point(0, 0), button)!.Value;
        Point bottomRight = glyph.TranslatePoint(new Point(glyph.Bounds.Width, glyph.Bounds.Height), button)!.Value;
        Assert.InRange(Math.Abs(bottomRight.X - topLeft.X), expectedSize - 0.1, expectedSize + 0.1);
        Assert.InRange(Math.Abs(bottomRight.Y - topLeft.Y), expectedSize - 0.1, expectedSize + 0.1);
    }

    private static void AssertButtonHasSingleIcon(Window window, string buttonName) =>
        Assert.Single(window.FindControl<Button>(buttonName)!.GetVisualDescendants().OfType<PathIcon>());

    private static Control GetButtonGlyph(Button button) {
        PathIcon[] icons = [.. button.GetVisualDescendants().OfType<PathIcon>()];
        return icons.SingleOrDefault(icon => string.Equals(icon.Name, "SidebarToggleGlyph", StringComparison.Ordinal)) ?? icons.Single();
    }
}
