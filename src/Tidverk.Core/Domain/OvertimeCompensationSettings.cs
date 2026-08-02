namespace Tidverk.Core;

public enum OvertimeCompensationMode {
    /// <summary>Overtime is banked as time off and counts towards the time balance.</summary>
    CompTime,

    /// <summary>Overtime is paid at a premium and is excluded from the time balance.</summary>
    Paid
}

public enum OvertimeThresholdMode {
    FixedDailyHours,
    ScheduledHours
}

public enum ObOvertimeCombinationMode {
    OvertimeOnly,
    Additive
}

public enum CompensationRuleType {
    Overtime,
    Ob
}

public enum CompensationRateType {
    HourlyPremiumPercent,
    FixedHourlyAmount,
    FullTimeMonthlySalaryDivisor
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
    PublicHolidays,
    ScheduledWeekdays,
    Weekends,
    MajorHolidays
}

/// <summary>An overtime or OB rate that applies on matching days within a time window.</summary>
public sealed record OvertimeRateBand {
    public OvertimeRateBand(
        string name,
        OvertimeDayCategory dayCategory,
        TimeOnly startTime,
        TimeOnly endTime,
        decimal premiumPercent,
        CompensationRuleType compensationType = CompensationRuleType.Overtime,
        CompensationRateType rateType = CompensationRateType.HourlyPremiumPercent,
        decimal rateValue = -1m) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Rate band name is required.", nameof(name));
        }

        decimal value = rateType == CompensationRateType.HourlyPremiumPercent && rateValue < 0m
            ? premiumPercent
            : rateValue;
        CompensationRate.Validate(rateType, value, nameof(rateValue));

        Name = name.Trim();
        DayCategory = dayCategory;
        StartTime = startTime;
        EndTime = endTime;
        CompensationType = compensationType;
        RateType = rateType;
        RateValue = value;
    }

    public string Name { get; }

    public OvertimeDayCategory DayCategory { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    public CompensationRuleType CompensationType { get; }

    public CompensationRateType RateType { get; }

    public decimal RateValue { get; }

    /// <summary>Retained in stored JSON so existing percentage rules migrate without conversion.</summary>
    public decimal PremiumPercent => RateType == CompensationRateType.HourlyPremiumPercent ? RateValue : 0m;

    public bool Matches(
        CompensationRuleType compensationType,
        DateOnly date,
        TimeOnly time,
        bool isScheduledWorkday,
        bool isPublicHoliday,
        bool isMajorHoliday) =>
        CompensationType == compensationType &&
        MatchesDay(date, isScheduledWorkday, isPublicHoliday, isMajorHoliday) &&
        MatchesTime(time);

    public decimal HourlyAmount(SalarySettings salary, bool includeHourlyBase) =>
        CompensationRate.HourlyAmount(RateType, RateValue, salary, includeHourlyBase);

    private bool MatchesDay(DateOnly date, bool isScheduledWorkday, bool isPublicHoliday, bool isMajorHoliday) => DayCategory switch {
        OvertimeDayCategory.ScheduledWorkdays => isScheduledWorkday,
        OvertimeDayCategory.NonWorkdays => !isScheduledWorkday,
        OvertimeDayCategory.PublicHolidays => isPublicHoliday,
        OvertimeDayCategory.ScheduledWeekdays => isScheduledWorkday && date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
        OvertimeDayCategory.Weekends => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        OvertimeDayCategory.MajorHolidays => isMajorHoliday,
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
        IEnumerable<OvertimeRateBand>? rateBands = null,
        OvertimeThresholdMode thresholdMode = OvertimeThresholdMode.FixedDailyHours,
        CompensationRateType defaultRateType = CompensationRateType.HourlyPremiumPercent,
        ObOvertimeCombinationMode obOvertimeCombination = ObOvertimeCombinationMode.OvertimeOnly) {
        CompensationRate.Validate(defaultRateType, premiumPercent, nameof(premiumPercent));
        if (dailyThresholdHours < 0m || decimal.Truncate(dailyThresholdHours * 60m) != dailyThresholdHours * 60m) {
            throw new ArgumentOutOfRangeException(nameof(dailyThresholdHours), "Daily overtime threshold cannot be negative and must resolve to whole minutes.");
        }

        Mode = mode;
        DefaultRateType = defaultRateType;
        DefaultRateValue = premiumPercent;
        DailyThresholdHours = dailyThresholdHours;
        DailyThresholdMinutes = new((int)(dailyThresholdHours * 60m));
        ThresholdMode = thresholdMode;
        RateBands = rateBands?.ToArray() ?? [];
        ObOvertimeCombination = obOvertimeCombination;
    }

    public OvertimeCompensationMode Mode { get; }

    public CompensationRateType DefaultRateType { get; }

    public decimal DefaultRateValue { get; }

    public decimal PremiumPercent => DefaultRateValue;

    public decimal DailyThresholdHours { get; }

    public Minutes DailyThresholdMinutes { get; }

    public OvertimeThresholdMode ThresholdMode { get; }

    public IReadOnlyList<OvertimeRateBand> RateBands { get; }

    public ObOvertimeCombinationMode ObOvertimeCombination { get; }

    public static OvertimeCompensationSettings CompTime { get; } = new(
        OvertimeCompensationMode.CompTime,
        thresholdMode: OvertimeThresholdMode.ScheduledHours);

    public Minutes ThresholdFor(WorkEntry entry, ExpectedHoursSettings expectedHours, ISwedishHolidayService holidays) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ArgumentNullException.ThrowIfNull(holidays);
        if (entry.ScheduledMinutesOverride is int scheduledMinutes) {
            return new(scheduledMinutes);
        }

        return ThresholdMode == OvertimeThresholdMode.ScheduledHours
            ? expectedHours.ExpectedMinutes(entry.Date, holidays)
            : DailyThresholdMinutes;
    }

    /// <summary>The highest matching hourly amount, or the default overtime amount when no rule matches.</summary>
    public decimal HourlyAmountAt(
        CompensationRuleType compensationType,
        SalarySettings salary,
        DateOnly date,
        TimeOnly time,
        bool isScheduledWorkday,
        bool isPublicHoliday,
        bool isMajorHoliday) {
        decimal highest = 0m;
        bool matched = false;
        for (int index = 0; index < RateBands.Count; index++) {
            OvertimeRateBand band = RateBands[index];
            if (!band.Matches(compensationType, date, time, isScheduledWorkday, isPublicHoliday, isMajorHoliday)) {
                continue;
            }

            decimal amount = band.HourlyAmount(salary, includeHourlyBase: compensationType == CompensationRuleType.Overtime);
            highest = matched ? Math.Max(highest, amount) : amount;
            matched = true;
        }

        if (matched || compensationType == CompensationRuleType.Ob) {
            return matched ? highest : 0m;
        }

        return CompensationRate.HourlyAmount(DefaultRateType, DefaultRateValue, salary, includeHourlyBase: true);
    }
}

internal static class CompensationRate {
    public static void Validate(CompensationRateType type, decimal value, string parameterName) {
        bool valid = type switch {
            CompensationRateType.HourlyPremiumPercent => value is >= 0m and <= 500m,
            CompensationRateType.FixedHourlyAmount => value is >= 0m and <= 100_000m,
            CompensationRateType.FullTimeMonthlySalaryDivisor => value is > 0m and <= 100_000m,
            _ => false
        };
        if (!valid) {
            throw new ArgumentOutOfRangeException(parameterName, "Enter a valid compensation percentage, hourly amount, or salary divisor.");
        }
    }

    public static decimal HourlyAmount(
        CompensationRateType type,
        decimal value,
        SalarySettings salary,
        bool includeHourlyBase) => type switch {
            CompensationRateType.HourlyPremiumPercent => salary.HourlySalary.Amount * ((includeHourlyBase ? 1m : 0m) + value / 100m),
            CompensationRateType.FixedHourlyAmount => value,
            CompensationRateType.FullTimeMonthlySalaryDivisor when salary.Type == SalaryType.Monthly => salary.FullTimeMonthlySalary / value,
            CompensationRateType.FullTimeMonthlySalaryDivisor => 0m,
            _ => 0m
        };
}
