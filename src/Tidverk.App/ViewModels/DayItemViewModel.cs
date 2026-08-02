using CommunityToolkit.Mvvm.ComponentModel;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>
/// One day cell, shared by the ledger and the calendar. Everything except the selection is fixed at
/// construction: the shell rebuilds these whenever the month changes.
/// </summary>
public sealed class DayItemViewModel : ObservableObject {
    private readonly ILocalizationService localization;
    private bool isSelected;

    public DayItemViewModel(
        DateOnly date,
        WorkEntry entry,
        bool isInMonth,
        string? holidayName,
        bool isExpectedWorkday,
        DateOnly today,
        bool monthStarted,
        ILocalizationService localization,
        string payText = "") {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(localization);
        Date = date;
        Entry = entry;
        IsInMonth = isInMonth;
        HolidayName = holidayName;
        IsExpectedWorkday = isExpectedWorkday;
        IsToday = date == today;
        IsFuture = date > today;
        IsUnstarted = !monthStarted;
        PayText = payText;
        this.localization = localization;
    }

    public DateOnly Date { get; }

    public WorkEntry Entry { get; }

    /// <summary>False for the padding days the calendar shows around the month; those are not editable.</summary>
    public bool IsInMonth { get; }

    public string? HolidayName { get; }

    public bool IsToday { get; }

    public bool IsFuture { get; }

    /// <summary>True while the month has no entries at all, which keeps it from looking full of gaps.</summary>
    public bool IsUnstarted { get; }

    public bool IsExpectedWorkday { get; }

    public bool IsOptional => Entry.Status == WorkEntryStatus.Incomplete && !IsExpectedWorkday;

    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>A workday in the past that the user has not filled in yet.</summary>
    public bool IsMissing =>
        IsInMonth && IsExpectedWorkday && !IsUnstarted && !IsToday && !IsFuture &&
        Entry.Status == WorkEntryStatus.Incomplete;

    public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }

    public string DayNumber => Date.Day.ToString(localization.Culture);

    public string DateText =>
        $"{Date.ToString("ddd d MMMM", localization.Culture)}{(IsToday ? $" - {localization.Get("Today")}" : string.Empty)}";

    /// <summary>A shift that ends the next day is marked "+1" so 22:00-06:00 cannot read as backwards.</summary>
    public string TimeText => Entry.Status switch {
        WorkEntryStatus.Worked => $"{Entry.StartTime:HH\\:mm}-{Entry.EndTime:HH\\:mm}{(Entry.CrossesMidnight ? " +1" : string.Empty)}",
        WorkEntryStatus.Off => localization.Get("Off"),
        _ => EmptyDayText("NotCompleted")
    };

    public string CalendarTimeText => Entry.Status == WorkEntryStatus.Worked ? TimeText : StatusText;

    public string LunchText => Entry.Status == WorkEntryStatus.Worked ? $"{Entry.LunchMinutes.Value} min" : string.Empty;

    public string HoursText => Entry.Status == WorkEntryStatus.Worked ? $"{Entry.WorkedHours:0.0}" : string.Empty;

    public string ProjectText => Entry.ProjectName ?? string.Empty;

    public string StatusText => Entry.Status switch {
        WorkEntryStatus.Worked => $"{Entry.WorkedHours:0.0} h{(Entry.ProjectName is null ? string.Empty : $" - {Entry.ProjectName}")}",
        WorkEntryStatus.Off => localization.Get("Off"),
        _ => EmptyDayText("MissingEntry")
    };

    public string ActionText => Entry.Status == WorkEntryStatus.Incomplete
        ? localization.Get("AddEntry")
        : localization.Get("Edit");

    public string PayText { get; }

    public string HolidayText => HolidayName ?? string.Empty;

    /// <summary>
    /// What an empty day says. Days outside the month, days in an untouched month and days that were
    /// never expected say nothing at all, so the views stay quiet instead of nagging.
    /// </summary>
    private string EmptyDayText(string missingKey) {
        if (!IsInMonth || IsUnstarted || !IsExpectedWorkday) {
            return string.Empty;
        }

        if (IsToday) {
            return localization.Get("Today");
        }

        return IsFuture ? localization.Get("Upcoming") : localization.Get(missingKey);
    }
}
