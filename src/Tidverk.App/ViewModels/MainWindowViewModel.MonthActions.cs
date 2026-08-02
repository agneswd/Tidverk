using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>Bulk actions for the selected month and the session-only month clipboard.</summary>
public sealed partial class MainWindowViewModel {
    private enum MonthAction { None, Fill, Paste, Reset }

    private IReadOnlyList<WorkEntry> copiedMonthEntries = [];
    private DateOnly? copiedMonth;
    private MonthAction pendingMonthAction;
    private bool isMonthActionConfirmationOpen;
    private string monthActionStatus = string.Empty;

    public bool IsMonthActionConfirmationOpen {
        get => isMonthActionConfirmationOpen;
        private set => SetProperty(ref isMonthActionConfirmationOpen, value);
    }

    public bool CanPasteMonth => copiedMonthEntries.Count > 0 && copiedMonth != selectedMonth;

    public bool CanResetMonth => IsMonthStarted || MonthlyOpeningBalance != 0;

    public bool IsMonthActionDestructive => pendingMonthAction == MonthAction.Reset;

    public string MonthActionTitle => pendingMonthAction switch {
        MonthAction.Fill => localization.Get("FillMonthTitle"),
        MonthAction.Paste => localization.Get("PasteMonthTitle"),
        MonthAction.Reset => localization.Get("ResetMonthTitle"),
        _ => string.Empty
    };

    public string MonthActionDescription => pendingMonthAction switch {
        MonthAction.Fill => localization.Format("FillMonthDescription", FillableDates().Count),
        MonthAction.Paste => localization.Format("PasteMonthDescription", PasteableEntries().Count, CopiedMonthTitle()),
        MonthAction.Reset => localization.Format("ResetMonthDescription", MonthTitle),
        _ => string.Empty
    };

    public string MonthActionConfirmText => pendingMonthAction switch {
        MonthAction.Fill => localization.Get("FillMonthConfirm"),
        MonthAction.Paste => localization.Get("PasteMonthConfirm"),
        MonthAction.Reset => localization.Get("ResetMonthConfirm"),
        _ => string.Empty
    };

    public string MonthActionStatus {
        get => monthActionStatus;
        private set {
            if (SetProperty(ref monthActionStatus, value)) {
                OnPropertyChanged(nameof(HasMonthActionStatus));
            }
        }
    }

    public bool HasMonthActionStatus => !string.IsNullOrWhiteSpace(MonthActionStatus);

    [RelayCommand]
    private void OpenFillMonthConfirmation() {
        int count = FillableDates().Count;
        if (count == 0) {
            MonthActionStatus = localization.Get("NoWorkdaysToFill");
            return;
        }

        OpenMonthAction(MonthAction.Fill);
    }

    [RelayCommand]
    private void CopyMonth() {
        copiedMonth = selectedMonth;
        copiedMonthEntries = monthEntries.Values
            .Where(entry => entry.Status != WorkEntryStatus.Incomplete)
            .OrderBy(entry => entry.Date)
            .ToArray();
        MonthActionStatus = localization.Format("MonthCopied", MonthTitle);
        OnPropertyChanged(nameof(CanPasteMonth));
    }

    [RelayCommand]
    private void OpenPasteMonthConfirmation() {
        if (!CanPasteMonth || PasteableEntries().Count == 0) {
            MonthActionStatus = localization.Get("NothingToPaste");
            return;
        }

        OpenMonthAction(MonthAction.Paste);
    }

    [RelayCommand]
    private void OpenResetMonthConfirmation() {
        if (CanResetMonth) {
            OpenMonthAction(MonthAction.Reset);
        }
    }

    [RelayCommand]
    private void CancelMonthAction() {
        pendingMonthAction = MonthAction.None;
        IsMonthActionConfirmationOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmMonthActionAsync() {
        MonthAction action = pendingMonthAction;
        try {
            switch (action) {
                case MonthAction.Fill:
                    IReadOnlyList<DateOnly> dates = FillableDates();
                    await workspace.SaveEntriesAsync(dates.Select(NormalWorkday).ToArray());
                    MonthActionStatus = localization.Format("MonthFilled", dates.Count);
                    break;
                case MonthAction.Paste:
                    IReadOnlyList<WorkEntry> entries = PasteableEntries();
                    await workspace.SaveEntriesAsync(entries);
                    MonthActionStatus = localization.Format("MonthPasted", entries.Count);
                    break;
                case MonthAction.Reset:
                    string month = MonthTitle;
                    await workspace.ResetMonthAsync(selectedMonth);
                    MonthActionStatus = localization.Format("MonthReset", month);
                    break;
                default:
                    return;
            }

            CancelMonthAction();
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Running month action {Action} for {Year}-{Month:D2} failed", action, selectedMonth.Year, selectedMonth.Month);
            CancelMonthAction();
            ErrorText = localization.Get("MonthActionFailed");
        }
    }

    private void OpenMonthAction(MonthAction action) {
        pendingMonthAction = action;
        IsMonthActionConfirmationOpen = true;
        OnPropertyChanged(nameof(MonthActionTitle));
        OnPropertyChanged(nameof(MonthActionDescription));
        OnPropertyChanged(nameof(MonthActionConfirmText));
        OnPropertyChanged(nameof(IsMonthActionDestructive));
    }

    private IReadOnlyList<DateOnly> FillableDates() => Days
        .Where(day => day.IsExpectedWorkday && day.Entry.Status == WorkEntryStatus.Incomplete)
        .Select(day => day.Date)
        .ToArray();

    private WorkEntry NormalWorkday(DateOnly date) => WorkEntry.CreateWorked(
        date,
        settings.DefaultStartTime,
        settings.DefaultEndTime,
        settings.DefaultLunchMinutes.Value,
        settings.DefaultProject);

    private IReadOnlyList<WorkEntry> PasteableEntries() {
        if (!CanPasteMonth) {
            return [];
        }

        List<WorkEntry> result = [];
        foreach (WorkEntry source in copiedMonthEntries) {
            DateOnly? targetDate = MatchingWeekdayOccurrence(source.Date, selectedMonth);
            if (targetDate is null ||
                monthEntries.TryGetValue(targetDate.Value, out WorkEntry? existing) && existing.Status != WorkEntryStatus.Incomplete) {
                continue;
            }

            result.Add(CopyToDate(source, targetDate.Value));
        }

        return result;
    }

    internal static DateOnly? MatchingWeekdayOccurrence(DateOnly source, DateOnly targetMonth) {
        int occurrence = ((source.Day - 1) / 7) + 1;
        DateOnly first = new(targetMonth.Year, targetMonth.Month, 1);
        int offset = ((int)source.DayOfWeek - (int)first.DayOfWeek + 7) % 7;
        DateOnly target = first.AddDays(offset + ((occurrence - 1) * 7));
        return target.Month == targetMonth.Month ? target : null;
    }

    private static WorkEntry CopyToDate(WorkEntry source, DateOnly target) => source.Status switch {
        WorkEntryStatus.Worked => WorkEntry.CreateWorked(
            target,
            source.StartTime!.Value,
            source.EndTime!.Value,
            source.LunchMinutes.Value,
            source.ProjectName,
            source.Notes,
            source.ScheduledMinutesOverride),
        WorkEntryStatus.Off => WorkEntry.CreateOff(target, source.Notes),
        _ => WorkEntry.CreateIncomplete(target)
    };

    private string CopiedMonthTitle() => copiedMonth is DateOnly month
        ? localization.Culture.TextInfo.ToTitleCase(month.ToString("MMMM yyyy", localization.Culture))
        : string.Empty;
}
