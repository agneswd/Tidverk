using CommunityToolkit.Mvvm.ComponentModel;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

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
        Date = date;
        Entry = entry;
        IsInMonth = isInMonth;
        HolidayName = holidayName;
        IsExpectedWorkday = isExpectedWorkday;
        IsToday = date == today;
        IsFuture = date > today;
        IsUnstarted = !monthStarted;
        this.localization = localization;
        PayText = payText;
    }

    public DateOnly Date { get; }

    public WorkEntry Entry { get; }

    public bool IsInMonth { get; }

    public string? HolidayName { get; }

    public bool IsToday { get; }

    public bool IsFuture { get; }

    public bool IsUnstarted { get; }

    public bool IsExpectedWorkday { get; }

    public bool IsOptional => Entry.Status == WorkEntryStatus.Incomplete && !IsExpectedWorkday;

    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public bool IsMissing => IsInMonth && Entry.Status == WorkEntryStatus.Incomplete && IsExpectedWorkday && !IsUnstarted && !IsToday && !IsFuture;

    public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }

    public string DayNumber => Date.Day.ToString(localization.Culture);

    public string DateText => $"{Date.ToString("ddd d MMMM", localization.Culture)}{(IsToday ? $" - {localization.Get("Today")}" : string.Empty)}";

    public string TimeText => Entry.Status switch {
        WorkEntryStatus.Worked => $"{Entry.StartTime:HH\\:mm}-{Entry.EndTime:HH\\:mm}",
        WorkEntryStatus.Off => localization.Get("Off"),
        _ when !IsInMonth => string.Empty,
        _ when IsUnstarted => string.Empty,
        _ when !IsExpectedWorkday => string.Empty,
        _ when IsToday => localization.Get("Today"),
        _ when IsFuture => localization.Get("Upcoming"),
        _ => localization.Get("NotCompleted")
    };

    public string CalendarTimeText => Entry.Status == WorkEntryStatus.Worked ? TimeText : StatusText;

    public string LunchText => Entry.Status == WorkEntryStatus.Worked ? $"{Entry.LunchMinutes.Value} min" : string.Empty;

    public string HoursText => Entry.Status == WorkEntryStatus.Worked ? $"{Entry.WorkedHours:0.0}" : string.Empty;

    public string ProjectText => Entry.ProjectName ?? string.Empty;

    public string StatusText => Entry.Status switch {
        WorkEntryStatus.Worked => $"{Entry.WorkedHours:0.0} h{(Entry.ProjectName is null ? string.Empty : $" - {Entry.ProjectName}")}",
        WorkEntryStatus.Off => localization.Get("Off"),
        _ when !IsInMonth => string.Empty,
        _ when IsUnstarted => string.Empty,
        _ when !IsExpectedWorkday => string.Empty,
        _ when IsToday => localization.Get("Today"),
        _ when IsFuture => localization.Get("Upcoming"),
        _ => localization.Get("MissingEntry")
    };

    public string ActionText => Entry.Status == WorkEntryStatus.Incomplete ? localization.Get("AddEntry") : localization.Get("Edit");

    public string PayText { get; }

    public string HolidayText => HolidayName ?? string.Empty;
}
