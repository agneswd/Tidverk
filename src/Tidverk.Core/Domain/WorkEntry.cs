namespace Tidverk.Core;

public enum WorkEntryStatus {
    Incomplete,
    Worked,
    Off
}

/// <summary>Thrown when a caller tries to build or persist an entry that breaks a domain rule.</summary>
public sealed class DomainValidationException(string message) : ArgumentException(message);

/// <summary>One calendar day of the timesheet. Immutable: an edit produces a new entry.</summary>
public sealed record WorkEntry {
    private WorkEntry(
        DateOnly date,
        WorkEntryStatus status,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Minutes lunchMinutes,
        string? projectName,
        string? notes,
        int? scheduledMinutesOverride) {
        Date = date;
        Status = status;
        StartTime = startTime;
        EndTime = endTime;
        LunchMinutes = lunchMinutes;
        ProjectName = Clean(projectName);
        Notes = Clean(notes);
        ScheduledMinutesOverride = scheduledMinutesOverride;
    }

    public DateOnly Date { get; }

    public WorkEntryStatus Status { get; }

    public TimeOnly? StartTime { get; }

    public TimeOnly? EndTime { get; }

    public Minutes LunchMinutes { get; }

    public string? ProjectName { get; }

    public string? Notes { get; }

    /// <summary>Planned minutes for this date when the normal weekly schedule does not apply.</summary>
    public int? ScheduledMinutesOverride { get; }

    public Minutes WorkedMinutes => MinuteMath.Worked(StartTime, EndTime, LunchMinutes);

    public decimal WorkedHours => WorkedMinutes.Hours;

    public bool IsComplete => Status is WorkEntryStatus.Worked or WorkEntryStatus.Off;

    public static WorkEntry CreateIncomplete(DateOnly date) =>
        new(date, WorkEntryStatus.Incomplete, null, null, Minutes.Zero, null, null, null);

    public static WorkEntry CreateOff(DateOnly date, string? notes = null) =>
        new(date, WorkEntryStatus.Off, null, null, Minutes.Zero, null, notes, null);

    public static WorkEntry CreateWorked(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        Minutes lunchMinutes,
        string? projectName = null,
        string? notes = null,
        int? scheduledMinutesOverride = null) =>
        CreateWorked(date, startTime, endTime, lunchMinutes.Value, projectName, notes, scheduledMinutesOverride);

    public static WorkEntry CreateWorked(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int lunchMinutes,
        string? projectName = null,
        string? notes = null,
        int? scheduledMinutesOverride = null) {
        IReadOnlyList<string> errors = ValidateWorked(date, startTime, endTime, lunchMinutes, scheduledMinutesOverride);
        if (errors.Count > 0) {
            throw new DomainValidationException(string.Join(" ", errors));
        }

        return new(date, WorkEntryStatus.Worked, startTime, endTime, new(lunchMinutes), projectName, notes, scheduledMinutesOverride);
    }

    /// <summary>Builds a worked entry from raw editor text, collecting every problem instead of throwing.</summary>
    public static bool TryCreateWorked(
        DateOnly date,
        string startTime,
        string endTime,
        int lunchMinutes,
        string? projectName,
        string? notes,
        out WorkEntry? entry,
        out IReadOnlyList<string> errors) =>
        TryCreateWorked(date, startTime, endTime, lunchMinutes, projectName, notes, null, out entry, out errors);

    public static bool TryCreateWorked(
        DateOnly date,
        string startTime,
        string endTime,
        int lunchMinutes,
        string? projectName,
        string? notes,
        int? scheduledMinutesOverride,
        out WorkEntry? entry,
        out IReadOnlyList<string> errors) {
        List<string> problems = [];
        if (!TimeInput.TryNormalize(startTime, out string normalizedStart)) {
            problems.Add("Start time is invalid.");
        }

        if (!TimeInput.TryNormalize(endTime, out string normalizedEnd)) {
            problems.Add("End time is invalid.");
        }

        entry = null;
        errors = problems;
        if (problems.Count > 0) {
            return false;
        }

        TimeOnly start = TimeInput.Parse(normalizedStart);
        TimeOnly end = TimeInput.Parse(normalizedEnd);
        problems.AddRange(ValidateWorked(date, start, end, lunchMinutes, scheduledMinutesOverride));
        if (problems.Count > 0) {
            return false;
        }

        entry = CreateWorked(date, start, end, lunchMinutes, projectName, notes, scheduledMinutesOverride);
        return true;
    }

    public IReadOnlyList<string> Validate() => Status switch {
        WorkEntryStatus.Incomplete => ValidateWithoutWorkedTime("incomplete"),
        WorkEntryStatus.Off => ValidateWithoutWorkedTime("day off"),
        WorkEntryStatus.Worked when StartTime is not null && EndTime is not null =>
            ValidateWorked(Date, StartTime.Value, EndTime.Value, LunchMinutes.Value, ScheduledMinutesOverride),
        WorkEntryStatus.Worked => Date == default
            ? ["Date is required.", "A worked entry requires start and end times."]
            : ["A worked entry requires start and end times."],
        _ => ["Unknown work entry status."]
    };

    public WorkEntry Reset() => CreateIncomplete(Date);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IReadOnlyList<string> ValidateWithoutWorkedTime(string statusName) {
        List<string> errors = [];
        if (Date == default) {
            errors.Add("Date is required.");
        }

        if (StartTime is not null || EndTime is not null || LunchMinutes != Minutes.Zero) {
            errors.Add($"An entry marked '{statusName}' cannot contain worked-time values.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateWorked(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int lunchMinutes,
        int? scheduledMinutesOverride) {
        List<string> errors = [];
        if (date == default) {
            errors.Add("Date is required.");
        }

        if (endTime <= startTime) {
            errors.Add("End time must be later than start time; overnight shifts are not supported.");
        }

        if (lunchMinutes < 0) {
            errors.Add("Lunch minutes cannot be negative.");
        }

        if (scheduledMinutesOverride < 0) {
            errors.Add("Scheduled minutes cannot be negative.");
        }

        int elapsed = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;
        if (lunchMinutes > elapsed) {
            errors.Add("Lunch cannot exceed the elapsed work interval.");
        }

        return errors;
    }
}
