namespace Tidverk.Core;

public enum WorkEntryStatus {
    Incomplete,
    Worked,
    Off,
    Missing = Incomplete,
    Leave = Off,
    Ledig = Off
}

public sealed class DomainValidationException : ArgumentException {
    public DomainValidationException(string message)
        : base(message) {
    }
}

public sealed record WorkEntry {
    private WorkEntry(
        DateOnly date,
        WorkEntryStatus status,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Minutes lunchMinutes,
        string? projectName,
        string? notes) {
        Date = date;
        Status = status;
        StartTime = startTime;
        EndTime = endTime;
        LunchMinutes = lunchMinutes;
        ProjectName = string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public DateOnly Date { get; }

    public WorkEntryStatus Status { get; }

    public TimeOnly? StartTime { get; }

    public TimeOnly? EndTime { get; }

    public Minutes LunchMinutes { get; }

    public string? ProjectName { get; }

    public string? Notes { get; }

    public Minutes WorkedMinutes => MinuteMath.Worked(StartTime, EndTime, LunchMinutes);

    public decimal WorkedHours => WorkedMinutes.Hours;

    public bool IsComplete => Status is WorkEntryStatus.Worked or WorkEntryStatus.Off;

    public static WorkEntry CreateIncomplete(DateOnly date) => new(date, WorkEntryStatus.Incomplete, null, null, Minutes.Zero, null, null);

    public static WorkEntry CreateOff(DateOnly date, string? notes = null) => new(date, WorkEntryStatus.Off, null, null, Minutes.Zero, null, notes);

    public static WorkEntry CreateWorked(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        Minutes lunchMinutes,
        string? projectName = null,
        string? notes = null) {
        var errors = ValidateWorked(date, startTime, endTime, lunchMinutes.Value);
        if (errors.Count > 0) {
            throw new DomainValidationException(string.Join(" ", errors));
        }

        return new(date, WorkEntryStatus.Worked, startTime, endTime, lunchMinutes, projectName, notes);
    }

    public static WorkEntry CreateWorked(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int lunchMinutes,
        string? projectName = null,
        string? notes = null) {
        var errors = ValidateWorked(date, startTime, endTime, lunchMinutes);
        if (errors.Count > 0) {
            throw new DomainValidationException(string.Join(" ", errors));
        }

        return new(date, WorkEntryStatus.Worked, startTime, endTime, new(lunchMinutes), projectName, notes);
    }

    public static bool TryCreateWorked(
        DateOnly date,
        string startTime,
        string endTime,
        int lunchMinutes,
        out WorkEntry? entry,
        out IReadOnlyList<string> errors,
        string? projectName = null,
        string? notes = null) {
        var validationErrors = new List<string>();
        if (!TimeInput.TryNormalize(startTime, out var normalizedStart)) {
            validationErrors.Add("Start time is invalid.");
        }

        if (!TimeInput.TryNormalize(endTime, out var normalizedEnd)) {
            validationErrors.Add("End time is invalid.");
        }

        if (validationErrors.Count == 0) {
            var start = TimeInput.Parse(normalizedStart);
            var end = TimeInput.Parse(normalizedEnd);
            validationErrors.AddRange(ValidateWorked(date, start, end, lunchMinutes));
            if (validationErrors.Count == 0) {
                entry = CreateWorked(date, start, end, lunchMinutes, projectName, notes);
                errors = validationErrors;
                return true;
            }
        }

        entry = null;
        errors = validationErrors;
        return false;
    }

    public IReadOnlyList<string> Validate() {
        return Status switch {
            WorkEntryStatus.Incomplete => ValidateEmptyStatus("incomplete"),
            WorkEntryStatus.Off => ValidateEmptyStatus("off"),
            WorkEntryStatus.Worked when StartTime is not null && EndTime is not null =>
                ValidateWorked(Date, StartTime.Value, EndTime.Value, LunchMinutes.Value),
            WorkEntryStatus.Worked => Date == default
                ? ["Date is required.", "A worked entry requires start and end times."]
                : ["A worked entry requires start and end times."],
            _ => ["Unknown work entry status."]
        };
    }

    public WorkEntry Reset() => CreateIncomplete(Date);

    private IReadOnlyList<string> ValidateEmptyStatus(string statusName) {
        var errors = new List<string>();
        if (Date == default) {
            errors.Add("Date is required.");
        }

        if (StartTime is not null || EndTime is not null || LunchMinutes != Minutes.Zero) {
            errors.Add($"An {statusName} entry cannot contain worked-time values.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateWorked(DateOnly date, TimeOnly startTime, TimeOnly endTime, int lunchMinutes) {
        var errors = new List<string>();
        if (date == default) {
            errors.Add("Date is required.");
        }

        if (endTime <= startTime) {
            errors.Add("End time must be later than start time; overnight shifts are not supported.");
        }

        if (lunchMinutes < 0) {
            errors.Add("Lunch minutes cannot be negative.");
        }

        var elapsed = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;
        if (lunchMinutes > elapsed) {
            errors.Add("Lunch cannot exceed the elapsed work interval.");
        }

        return errors;
    }
}
