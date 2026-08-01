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
using Tidverk.App.ViewModels;

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

            viewModel.SelectedInterfaceScale = 125;
            SaveSnapshot(window, outputDirectory, "ledger-scale-125-light.png");
            viewModel.SelectedInterfaceScale = 100;

            SetPrivateBoolean(viewModel, nameof(MainWindowViewModel.IsSetupOpen), true);
            SaveSnapshot(window, outputDirectory, "setup-light.png");
            SetPrivateBoolean(viewModel, nameof(MainWindowViewModel.IsSetupOpen), false);

            viewModel.StartCatchUpCommand.Execute(null);
            SaveSnapshot(window, outputDirectory, "catch-up-light.png");
            viewModel.CloseCatchUpCommand.Execute(null);

            viewModel.OpenReportCommand.Execute(null);
            SaveSnapshot(window, outputDirectory, "report-light.png");
            viewModel.CloseReportCommand.Execute(null);

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

    private static void SetPrivateBoolean(MainWindowViewModel viewModel, string propertyName, bool value) {
        typeof(MainWindowViewModel).GetProperty(propertyName)!.SetValue(viewModel, value);
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
