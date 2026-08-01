namespace Tidverk.Core;

/// <summary>The schedule the timesheet is measured against: how long a workday is and which days count.</summary>
public sealed record ExpectedHoursSettings {
    public ExpectedHoursSettings(
        decimal hoursPerWorkday,
        IEnumerable<DayOfWeek> workingWeekdays,
        bool excludePublicHolidays) {
        ArgumentNullException.ThrowIfNull(workingWeekdays);
        if (hoursPerWorkday <= 0 || decimal.Truncate(hoursPerWorkday * 60m) != hoursPerWorkday * 60m) {
            throw new ArgumentOutOfRangeException(nameof(hoursPerWorkday), "Expected hours must be positive and resolve to whole minutes.");
        }

        DayOfWeek[] weekdays = workingWeekdays.Distinct().ToArray();
        if (weekdays.Length == 0) {
            throw new ArgumentException("At least one working weekday is required.", nameof(workingWeekdays));
        }

        HoursPerWorkday = hoursPerWorkday;
        DailyMinutes = new((int)(hoursPerWorkday * 60m));
        WorkingWeekdays = weekdays;
        ExcludePublicHolidays = excludePublicHolidays;
    }

    public decimal HoursPerWorkday { get; }

    public Minutes DailyMinutes { get; }

    public IReadOnlyCollection<DayOfWeek> WorkingWeekdays { get; }

    public bool ExcludePublicHolidays { get; }

    public static ExpectedHoursSettings Standard { get; } = new(
        8m,
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        true);

    public bool IsExpectedWeekday(DateOnly date) => WorkingWeekdays.Contains(date.DayOfWeek);

    /// <summary>Whether the schedule expects work on this date, once public holidays are taken into account.</summary>
    public bool IsScheduledWorkday(DateOnly date, ISwedishHolidayService holidays) {
        ArgumentNullException.ThrowIfNull(holidays);
        return IsExpectedWeekday(date) && (!ExcludePublicHolidays || !holidays.IsPublicHoliday(date));
    }
}
