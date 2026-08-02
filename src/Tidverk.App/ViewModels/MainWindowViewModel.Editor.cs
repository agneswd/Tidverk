using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>The day editor and the catch-up flow that walks the month's missing days.</summary>
public sealed partial class MainWindowViewModel {
    private readonly List<DateOnly> catchUpDates = [];
    private int catchUpIndex;
    private bool isEditorOpen;
    private bool isCatchUpOpen;
    private DayItemViewModel? selectedDay;
    private string errorText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorHours))]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    [NotifyPropertyChangedFor(nameof(EditorCrossesMidnight))]
    private string editorStart = "08:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorHours))]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    [NotifyPropertyChangedFor(nameof(EditorCrossesMidnight))]
    private string editorEnd = "16:30";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorHours))]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    private int editorLunch = 30;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorHours))]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    [NotifyPropertyChangedFor(nameof(EditorCrossesMidnight))]
    private bool editorIsOff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    private bool editorUseScheduledHoursOverride;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorWorkBreakdown))]
    private decimal editorScheduledHours;

    [ObservableProperty]
    private string editorProject = string.Empty;

    [ObservableProperty]
    private string editorNotes = string.Empty;

    public bool IsEditorOpen { get => isEditorOpen; private set => SetProperty(ref isEditorOpen, value); }

    public bool IsCatchUpOpen { get => isCatchUpOpen; private set => SetProperty(ref isCatchUpOpen, value); }

    public DayItemViewModel? SelectedDay { get => selectedDay; private set => SetProperty(ref selectedDay, value); }

    public string ErrorText {
        get => errorText;
        private set {
            if (SetProperty(ref errorText, value)) {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public string EditorTitle => SelectedDay?.Date.ToString("dddd d MMMM", localization.Culture) ?? localization.Get("Day");

    /// <summary>Live total for the editor; unparsable input simply shows zero rather than an error.</summary>
    public string EditorHours {
        get {
            if (EditorIsOff ||
                !TimeInput.TryNormalize(EditorStart, out string start) ||
                !TimeInput.TryNormalize(EditorEnd, out string end)) {
                return "0.0 h";
            }

            Minutes worked = MinuteMath.Worked(TimeInput.Parse(start), TimeInput.Parse(end), new Minutes(Math.Max(0, EditorLunch)));
            return $"{worked.Hours.ToString("0.0", localization.Culture)} h";
        }
    }

    /// <summary>Confirms that an end before the start was read as a shift running past midnight.</summary>
    public bool EditorCrossesMidnight =>
        !EditorIsOff &&
        TimeInput.TryNormalize(EditorStart, out string start) &&
        TimeInput.TryNormalize(EditorEnd, out string end) &&
        TimeInput.Parse(end) <= TimeInput.Parse(start);

    public string EditorWorkBreakdown {
        get {
            if (SelectedDay is null || EditorIsOff || !TryGetScheduledMinutesOverride(out int? scheduledMinutes)) {
                return string.Empty;
            }

            if (!WorkEntry.TryCreateWorked(
                    SelectedDay.Date,
                    EditorStart,
                    EditorEnd,
                    EditorLunch,
                    EditorProject,
                    EditorNotes,
                    scheduledMinutes,
                    out WorkEntry? entry,
                    out _)) {
                return string.Empty;
            }

            (int regular, int overtime) = workspace.SplitTime(entry!, settings);
            return localization.Format("EditorWorkBreakdown", regular / 60m, overtime / 60m);
        }
    }

    public string CatchUpTitle => SelectedDay?.Date.ToString("dddd d MMMM", localization.Culture) ?? string.Empty;

    public string CatchUpProgress => catchUpDates.Count == 0
        ? string.Empty
        : localization.Format("CatchUpProgress", catchUpIndex + 1, catchUpDates.Count);

    [RelayCommand]
    private void OpenEditor(DayItemViewModel? day) {
        if (day is null || !day.IsInMonth) {
            return;
        }

        Select(day);
        WorkEntry entry = day.Entry;
        EditorIsOff = entry.Status == WorkEntryStatus.Off;
        EditorStart = TimeInput.Format(entry.StartTime ?? settings.DefaultStartTime);
        EditorEnd = TimeInput.Format(entry.EndTime ?? settings.DefaultEndTime);
        EditorLunch = entry.Status == WorkEntryStatus.Worked ? entry.LunchMinutes.Value : settings.DefaultLunchMinutes.Value;
        EditorProject = entry.ProjectName ?? settings.DefaultProject;
        EditorNotes = entry.Notes ?? string.Empty;
        EditorUseScheduledHoursOverride = entry.ScheduledMinutesOverride is not null;
        EditorScheduledHours = entry.ScheduledMinutesOverride is int scheduledMinutes
            ? scheduledMinutes / 60m
            : workspace.ScheduledMinutes(day.Date, settings).Hours;
        ErrorText = string.Empty;
        IsEditorOpen = true;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorHours));
        OnPropertyChanged(nameof(EditorWorkBreakdown));
        OnPropertyChanged(nameof(EditorCrossesMidnight));
    }

    [RelayCommand]
    private void CloseEditor() {
        IsEditorOpen = false;
        ErrorText = string.Empty;
        ClearSelection();
    }

    [RelayCommand]
    private async Task SaveEntryAsync() {
        if (SelectedDay is null) {
            return;
        }

        DateOnly date = SelectedDay.Date;
        try {
            if (BuildEntry(date) is not WorkEntry entry) {
                return;
            }

            await workspace.SaveEntryAsync(entry);
            ErrorText = string.Empty;
            await LoadMonthAsync();
            IsEditorOpen = false;
            ClearSelection();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Saving work entry for {Date} failed", date);
            ErrorText = localization.Get("EntryNotSaved");
        }
    }

    [RelayCommand]
    private async Task SaveAndNextAsync() {
        await SaveEntryAsync();
        if (!HasError && IsCatchUpOpen) {
            MoveCatchUp(1, preserveInput: true);
        }
    }

    [RelayCommand]
    private async Task ResetEntryAsync() {
        if (SelectedDay is null) {
            return;
        }

        DateOnly date = SelectedDay.Date;
        try {
            await workspace.ResetEntryAsync(date);
            ErrorText = string.Empty;
            await LoadMonthAsync();
            IsEditorOpen = false;
            ClearSelection();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Resetting work entry for {Date} failed", date);
            ErrorText = localization.Get("EntryNotSaved");
        }
    }

    [RelayCommand]
    private void NormalDay() {
        EditorIsOff = false;
        EditorStart = TimeInput.Format(settings.DefaultStartTime);
        EditorEnd = TimeInput.Format(settings.DefaultEndTime);
        EditorLunch = settings.DefaultLunchMinutes.Value;
        EditorProject = settings.DefaultProject;
        EditorUseScheduledHoursOverride = false;
    }

    [RelayCommand]
    private void CopyPrevious() {
        WorkEntry? previous = monthEntries.Values
            .Where(entry => entry.Date < SelectedDay?.Date && entry.Status == WorkEntryStatus.Worked)
            .OrderByDescending(entry => entry.Date)
            .FirstOrDefault();
        CopyEntry(previous);
    }

    [RelayCommand]
    private void CopyLastWeek() {
        if (SelectedDay is not null) {
            CopyEntry(monthEntries.GetValueOrDefault(SelectedDay.Date.AddDays(-7)));
        }
    }

    [RelayCommand]
    private void StartCatchUp() {
        catchUpDates.Clear();
        if (summary is not null) {
            catchUpDates.AddRange(summary.MissingPastDays);
        }

        catchUpIndex = 0;
        IsCatchUpOpen = catchUpDates.Count > 0;
        OpenCatchUpDay();
    }

    [RelayCommand]
    private void SkipCatchUp() => MoveCatchUp(1, preserveInput: true);

    [RelayCommand]
    private void BackCatchUp() => MoveCatchUp(-1);

    [RelayCommand]
    private void CloseCatchUp() {
        IsCatchUpOpen = false;
        IsEditorOpen = false;
        ClearSelection();
    }

    /// <summary>Opens the first day of the month, or today when the current month is showing.</summary>
    [RelayCommand]
    private void StartMonth() {
        DateOnly fallback = new(selectedMonth.Year, selectedMonth.Month, 1);
        DateOnly targetDate = IsCurrentMonth
            ? workspace.Today
            : Days.FirstOrDefault(day => day.IsExpectedWorkday)?.Date ?? fallback;
        OpenEditor(Days.FirstOrDefault(day => day.Date == targetDate));
    }

    /// <summary>Ctrl+S saves whatever the user is looking at.</summary>
    [RelayCommand]
    private Task SaveCurrentAsync() {
        if (IsSettingsPage) {
            return SaveSettingsAsync();
        }

        return IsEditorOpen ? SaveEntryAsync() : Task.CompletedTask;
    }

    /// <summary>Returns null and fills <see cref="ErrorText"/> when the editor holds invalid input.</summary>
    private WorkEntry? BuildEntry(DateOnly date) {
        if (EditorIsOff) {
            return WorkEntry.CreateOff(date, EditorNotes);
        }

        if (!TryGetScheduledMinutesOverride(out int? scheduledMinutes)) {
            ErrorText = localization.Get("ValidScheduledHoursRequired");
            return null;
        }

        if (WorkEntry.TryCreateWorked(
                date,
                EditorStart,
                EditorEnd,
                EditorLunch,
                EditorProject,
                EditorNotes,
                scheduledMinutes,
                out WorkEntry? worked,
                out IReadOnlyList<string> errors)) {
            return worked;
        }

        ErrorText = string.Join(" ", errors);
        return null;
    }

    private void CopyEntry(WorkEntry? entry) {
        if (entry?.Status != WorkEntryStatus.Worked) {
            ErrorText = localization.Get("NoCompletedDay");
            return;
        }

        EditorIsOff = false;
        EditorStart = TimeInput.Format(entry.StartTime!.Value);
        EditorEnd = TimeInput.Format(entry.EndTime!.Value);
        EditorLunch = entry.LunchMinutes.Value;
        EditorProject = entry.ProjectName ?? settings.DefaultProject;
        EditorUseScheduledHoursOverride = entry.ScheduledMinutesOverride is not null;
        EditorScheduledHours = entry.ScheduledMinutesOverride.GetValueOrDefault() / 60m;
        ErrorText = string.Empty;
    }

    private bool TryGetScheduledMinutesOverride(out int? scheduledMinutes) {
        scheduledMinutes = null;
        if (!EditorUseScheduledHoursOverride) {
            return true;
        }

        decimal minutes = EditorScheduledHours * 60m;
        if (EditorScheduledHours < 0m || decimal.Truncate(minutes) != minutes || minutes > int.MaxValue) {
            return false;
        }

        scheduledMinutes = decimal.ToInt32(minutes);
        return true;
    }

    private void MoveCatchUp(int delta, bool preserveInput = false) {
        (string Start, string End, int Lunch, bool IsOff) input = (EditorStart, EditorEnd, EditorLunch, EditorIsOff);
        catchUpIndex += delta;
        if (catchUpIndex < 0) {
            catchUpIndex = 0;
        }
        else if (catchUpIndex >= catchUpDates.Count) {
            CloseCatchUp();
            return;
        }

        OpenCatchUpDay();
        if (preserveInput && IsCatchUpOpen) {
            EditorStart = input.Start;
            EditorEnd = input.End;
            EditorLunch = input.Lunch;
            EditorIsOff = input.IsOff;
        }
    }

    /// <summary>Catch-up reuses the editor's fields but keeps its own dialog, so the editor stays closed.</summary>
    private void OpenCatchUpDay() {
        if (!IsCatchUpOpen) {
            return;
        }

        OpenEditor(Days.FirstOrDefault(item => item.Date == catchUpDates[catchUpIndex]));
        IsEditorOpen = false;
        OnPropertyChanged(nameof(CatchUpTitle));
        OnPropertyChanged(nameof(CatchUpProgress));
    }

    private void Select(DayItemViewModel day) {
        SelectedDay = day;
        foreach (DayItemViewModel item in Days.Concat(CalendarDays)) {
            item.IsSelected = item.IsInMonth && item.Date == day.Date;
        }
    }

    private void ClearSelection() {
        SelectedDay = null;
        foreach (DayItemViewModel item in Days.Concat(CalendarDays)) {
            item.IsSelected = false;
        }
    }
}
