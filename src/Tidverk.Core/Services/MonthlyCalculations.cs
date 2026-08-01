namespace Tidverk.Core;

/// <summary>Everything the month view and the export need about one month, computed in one pass.</summary>
public sealed record MonthlySummary {
    public required int Year { get; init; }

    public required int Month { get; init; }

    public required Minutes WorkedMinutes { get; init; }

    public required Minutes RegularMinutes { get; init; }

    public required Minutes OvertimeMinutes { get; init; }

    /// <summary>The worked minutes that move the time balance; paid overtime is excluded.</summary>
    public required Minutes BalanceEligibleMinutes { get; init; }

    public required Minutes ExpectedMinutes { get; init; }

    public required int MonthlyDifferenceMinutes { get; init; }

    public required int OpeningBalanceMinutes { get; init; }

    public required int ClosingBalanceMinutes { get; init; }

    public required decimal GrossSalary { get; init; }

    public required int CompletedDayCount { get; init; }

    public required IReadOnlyList<DateOnly> MissingPastDays { get; init; }

    public int MissingPastDayCount => MissingPastDays.Count;

    public decimal WorkedHours => WorkedMinutes.Hours;

    public decimal RegularHours => RegularMinutes.Hours;

    public decimal OvertimeHours => OvertimeMinutes.Hours;

    public decimal ExpectedHours => ExpectedMinutes.Hours;
}

public static class ExpectedHoursCalculator {
    public static IReadOnlyList<DateOnly> GetDates(int year, int month) {
        int lastDay = DateTime.DaysInMonth(year, month);
        DateOnly[] dates = new DateOnly[lastDay];
        for (int day = 1; day <= lastDay; day++) {
            dates[day - 1] = new DateOnly(year, month, day);
        }

        return dates;
    }

    public static IReadOnlyList<DateOnly> GetExpectedWorkdays(
        int year,
        int month,
        ExpectedHoursSettings settings,
        ISwedishHolidayService holidayService) {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(holidayService);

        return GetDates(year, month)
            .Where(date => settings.IsScheduledWorkday(date, holidayService))
            .ToArray();
    }

    public static Minutes CalculateExpectedMinutes(
        int year,
        int month,
        ExpectedHoursSettings settings,
        ISwedishHolidayService holidayService,
        int? expectedMinutesOverride = null) {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(holidayService);
        if (expectedMinutesOverride is < 0) {
            throw new ArgumentOutOfRangeException(nameof(expectedMinutesOverride), "Expected minutes cannot be negative.");
        }

        return expectedMinutesOverride is int overrideMinutes
            ? new(overrideMinutes)
            : new(GetExpectedWorkdays(year, month, settings, holidayService).Count * settings.DailyMinutes.Value);
    }
}

public static class BalanceCalculator {
    public static int MonthlyDifference(Minutes workedMinutes, Minutes expectedMinutes) =>
        workedMinutes.Value - expectedMinutes.Value;

    public static int ClosingBalance(int openingBalanceMinutes, Minutes workedMinutes, Minutes expectedMinutes) =>
        openingBalanceMinutes + MonthlyDifference(workedMinutes, expectedMinutes);
}

public static class SalaryCalculator {
    public static decimal GrossSalary(Minutes workedMinutes, HourlySalary hourlySalary) =>
        Round(workedMinutes.Hours * hourlySalary.Amount);

    public static decimal GrossSalary(Minutes workedMinutes, decimal hourlyRate) =>
        GrossSalary(workedMinutes, new HourlySalary(hourlyRate));

    /// <summary>
    /// Pay for a single day. Minutes past the daily threshold are overtime; under
    /// <see cref="OvertimeCompensationMode.Paid"/> each overtime minute is priced with the premium
    /// that applies at the clock time it was worked, so evening and weekend bands are honoured.
    /// </summary>
    public static decimal GrossSalary(
        WorkEntry entry,
        ExpectedHoursSettings expectedHours,
        HourlySalary hourlySalary,
        OvertimeCompensationSettings overtimeCompensation,
        ISwedishHolidayService holidayService) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ArgumentNullException.ThrowIfNull(overtimeCompensation);
        ArgumentNullException.ThrowIfNull(holidayService);
        if (entry.Status != WorkEntryStatus.Worked || entry.EndTime is null) {
            return 0m;
        }

        (int regularMinutes, int overtimeMinutes) = SplitOvertime(entry.WorkedMinutes, overtimeCompensation);
        decimal minuteRate = hourlySalary.Amount / 60m;
        decimal pay = regularMinutes * minuteRate;
        if (overtimeCompensation.Mode != OvertimeCompensationMode.Paid) {
            return Round(pay);
        }

        bool isPublicHoliday = holidayService.IsPublicHoliday(entry.Date);
        bool isScheduledWorkday = expectedHours.IsScheduledWorkday(entry.Date, holidayService);
        TimeOnly overtimeStart = entry.EndTime.Value.AddMinutes(-overtimeMinutes);
        for (int minute = 0; minute < overtimeMinutes; minute++) {
            decimal premium = overtimeCompensation.PremiumAt(
                entry.Date,
                overtimeStart.AddMinutes(minute),
                isScheduledWorkday,
                isPublicHoliday);
            pay += minuteRate * (1m + premium / 100m);
        }

        return Round(pay);
    }

    internal static (int RegularMinutes, int OvertimeMinutes) SplitOvertime(Minutes worked, OvertimeCompensationSettings overtimeCompensation) {
        int regular = Math.Min(worked.Value, overtimeCompensation.DailyThresholdMinutes.Value);
        return (regular, worked.Value - regular);
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public static class MonthlyCalculator {
    public static MonthlySummary Calculate(
        MonthRecord month,
        IEnumerable<WorkEntry> entries,
        ExpectedHoursSettings expectedHours,
        HourlySalary hourlySalary,
        DateOnly today,
        ISwedishHolidayService? holidayService = null,
        OvertimeCompensationSettings? overtimeCompensation = null) {
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ISwedishHolidayService holidays = holidayService ?? new SwedishHolidayService();
        OvertimeCompensationSettings overtime = overtimeCompensation ?? OvertimeCompensationSettings.CompTime;

        Dictionary<DateOnly, WorkEntry> entriesByDate = entries
            .Where(entry => entry.Date.Year == month.Year && entry.Date.Month == month.Month)
            .ToDictionary(entry => entry.Date);
        Minutes expectedMinutes = ExpectedHoursCalculator.CalculateExpectedMinutes(
            month.Year,
            month.Month,
            expectedHours,
            holidays,
            month.ExpectedMinutesOverride);
        DateOnly[] missingPastDays = ExpectedHoursCalculator
            .GetExpectedWorkdays(month.Year, month.Month, expectedHours, holidays)
            .Where(date => date < today && IsUnfilled(entriesByDate, date))
            .ToArray();

        Minutes workedMinutes = Minutes.Zero;
        Minutes regularMinutes = Minutes.Zero;
        Minutes overtimeMinutes = Minutes.Zero;
        decimal grossSalary = 0m;
        foreach (WorkEntry entry in entriesByDate.Values) {
            if (entry.Status != WorkEntryStatus.Worked) {
                continue;
            }

            (int regular, int overtimeForDay) = SalaryCalculator.SplitOvertime(entry.WorkedMinutes, overtime);
            workedMinutes += entry.WorkedMinutes;
            regularMinutes += new Minutes(regular);
            overtimeMinutes += new Minutes(overtimeForDay);
            grossSalary += SalaryCalculator.GrossSalary(entry, expectedHours, hourlySalary, overtime, holidays);
        }

        Minutes balanceEligibleMinutes = overtime.Mode == OvertimeCompensationMode.CompTime
            ? workedMinutes
            : regularMinutes;

        return new MonthlySummary {
            Year = month.Year,
            Month = month.Month,
            WorkedMinutes = workedMinutes,
            RegularMinutes = regularMinutes,
            OvertimeMinutes = overtimeMinutes,
            BalanceEligibleMinutes = balanceEligibleMinutes,
            ExpectedMinutes = expectedMinutes,
            MonthlyDifferenceMinutes = BalanceCalculator.MonthlyDifference(balanceEligibleMinutes, expectedMinutes),
            OpeningBalanceMinutes = month.OpeningBalanceMinutes,
            ClosingBalanceMinutes = BalanceCalculator.ClosingBalance(month.OpeningBalanceMinutes, balanceEligibleMinutes, expectedMinutes),
            GrossSalary = grossSalary,
            CompletedDayCount = entriesByDate.Values.Count(entry => entry.IsComplete),
            MissingPastDays = missingPastDays
        };
    }

    private static bool IsUnfilled(Dictionary<DateOnly, WorkEntry> entriesByDate, DateOnly date) =>
        !entriesByDate.TryGetValue(date, out WorkEntry? entry) || entry.Status == WorkEntryStatus.Incomplete;
}
