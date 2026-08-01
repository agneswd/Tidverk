namespace Tidverk.Core;

public enum OvertimeCompensationMode {
    /// <summary>Overtime is banked as time off and counts towards the time balance.</summary>
    CompTime,

    /// <summary>Overtime is paid at a premium and is excluded from the time balance.</summary>
    Paid
}

public enum OvertimeDayCategory {
    AllDays,
    ScheduledWorkdays,
    NonWorkdays,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
    PublicHolidays
}

/// <summary>A premium that applies to overtime worked on matching days within a time window.</summary>
public sealed record OvertimeRateBand {
    public OvertimeRateBand(string name, OvertimeDayCategory dayCategory, TimeOnly startTime, TimeOnly endTime, decimal premiumPercent) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Rate band name is required.", nameof(name));
        }

        OvertimePremium.Validate(premiumPercent, nameof(premiumPercent));

        Name = name.Trim();
        DayCategory = dayCategory;
        StartTime = startTime;
        EndTime = endTime;
        PremiumPercent = premiumPercent;
    }

    public string Name { get; }

    public OvertimeDayCategory DayCategory { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    public decimal PremiumPercent { get; }

    public bool Matches(DateOnly date, TimeOnly time, bool isScheduledWorkday, bool isPublicHoliday) =>
        MatchesDay(date, isScheduledWorkday, isPublicHoliday) && MatchesTime(time);

    private bool MatchesDay(DateOnly date, bool isScheduledWorkday, bool isPublicHoliday) => DayCategory switch {
        OvertimeDayCategory.ScheduledWorkdays => isScheduledWorkday,
        OvertimeDayCategory.NonWorkdays => !isScheduledWorkday,
        OvertimeDayCategory.PublicHolidays => isPublicHoliday,
        OvertimeDayCategory.Monday => date.DayOfWeek == DayOfWeek.Monday,
        OvertimeDayCategory.Tuesday => date.DayOfWeek == DayOfWeek.Tuesday,
        OvertimeDayCategory.Wednesday => date.DayOfWeek == DayOfWeek.Wednesday,
        OvertimeDayCategory.Thursday => date.DayOfWeek == DayOfWeek.Thursday,
        OvertimeDayCategory.Friday => date.DayOfWeek == DayOfWeek.Friday,
        OvertimeDayCategory.Saturday => date.DayOfWeek == DayOfWeek.Saturday,
        OvertimeDayCategory.Sunday => date.DayOfWeek == DayOfWeek.Sunday,
        _ => true
    };

    /// <summary>An equal start and end covers the whole day; a start after the end wraps past midnight.</summary>
    private bool MatchesTime(TimeOnly time) {
        if (StartTime == EndTime) {
            return true;
        }

        return StartTime < EndTime
            ? time >= StartTime && time < EndTime
            : time >= StartTime || time < EndTime;
    }
}

/// <summary>How work beyond the daily threshold is compensated.</summary>
public sealed record OvertimeCompensationSettings {
    public OvertimeCompensationSettings(
        OvertimeCompensationMode mode,
        decimal premiumPercent = 50m,
        decimal dailyThresholdHours = 8m,
        IEnumerable<OvertimeRateBand>? rateBands = null) {
        OvertimePremium.Validate(premiumPercent, nameof(premiumPercent));
        if (dailyThresholdHours <= 0m || decimal.Truncate(dailyThresholdHours * 60m) != dailyThresholdHours * 60m) {
            throw new ArgumentOutOfRangeException(nameof(dailyThresholdHours), "Daily overtime threshold must be positive and resolve to whole minutes.");
        }

        Mode = mode;
        PremiumPercent = premiumPercent;
        DailyThresholdHours = dailyThresholdHours;
        DailyThresholdMinutes = new((int)(dailyThresholdHours * 60m));
        RateBands = rateBands?.ToArray() ?? [];
    }

    public OvertimeCompensationMode Mode { get; }

    /// <summary>The premium used when no rate band matches.</summary>
    public decimal PremiumPercent { get; }

    public decimal DailyThresholdHours { get; }

    public Minutes DailyThresholdMinutes { get; }

    public IReadOnlyList<OvertimeRateBand> RateBands { get; }

    public static OvertimeCompensationSettings CompTime { get; } = new(OvertimeCompensationMode.CompTime);

    /// <summary>The highest premium among matching rate bands, or the default premium when none matches.</summary>
    public decimal PremiumAt(DateOnly date, TimeOnly time, bool isScheduledWorkday, bool isPublicHoliday) {
        decimal highest = 0m;
        bool matched = false;
        for (int index = 0; index < RateBands.Count; index++) {
            OvertimeRateBand band = RateBands[index];
            if (!band.Matches(date, time, isScheduledWorkday, isPublicHoliday)) {
                continue;
            }

            highest = matched ? Math.Max(highest, band.PremiumPercent) : band.PremiumPercent;
            matched = true;
        }

        return matched ? highest : PremiumPercent;
    }
}

internal static class OvertimePremium {
    public static void Validate(decimal premiumPercent, string parameterName) {
        if (premiumPercent is < 0m or > 500m) {
            throw new ArgumentOutOfRangeException(parameterName, "Overtime premium must be between 0% and 500%.");
        }
    }
}
