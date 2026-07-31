namespace Tidverk.Core;

public sealed record MonthlySummary {
    public required int Year { get; init; }

    public required int Month { get; init; }

    public required Minutes WorkedMinutes { get; init; }

    public required Minutes RegularMinutes { get; init; }

    public required Minutes OvertimeMinutes { get; init; }

    public required Minutes BalanceEligibleMinutes { get; init; }

    public required Minutes ExpectedMinutes { get; init; }

    public required int MonthlyDifferenceMinutes { get; init; }

    public required int OpeningBalanceMinutes { get; init; }

    public required int ClosingBalanceMinutes { get; init; }

    public required decimal GrossSalary { get; init; }

    public required int CompletedDayCount { get; init; }

    public required int MissingPastDayCount { get; init; }

    public required IReadOnlyList<DateOnly> MissingPastDays { get; init; }

    public decimal WorkedHours => WorkedMinutes.Hours;

    public decimal RegularHours => RegularMinutes.Hours;

    public decimal OvertimeHours => OvertimeMinutes.Hours;

    public decimal ExpectedHours => ExpectedMinutes.Hours;
}

public static class ExpectedHoursCalculator {
    public static IReadOnlyList<DateOnly> GetDates(int year, int month) {
        var lastDay = DateTime.DaysInMonth(year, month);
        return Enumerable.Range(1, lastDay).Select(day => new DateOnly(year, month, day)).ToArray();
    }

    public static IReadOnlyList<DateOnly> GetExpectedWorkdays(
        int year,
        int month,
        ExpectedHoursSettings settings,
        ISwedishHolidayService holidayService) {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(holidayService);

        return GetDates(year, month)
            .Where(date => settings.IsExpectedWeekday(date) &&
                (!settings.ExcludePublicHolidays || !holidayService.IsPublicHoliday(date)))
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
            throw new ArgumentOutOfRangeException(nameof(expectedMinutesOverride));
        }

        return expectedMinutesOverride is int overrideMinutes
            ? new(overrideMinutes)
            : new(GetExpectedWorkdays(year, month, settings, holidayService).Count * settings.DailyMinutes.Value);
    }
}

public static class BalanceCalculator {
    public static int MonthlyDifference(Minutes workedMinutes, Minutes expectedMinutes) => workedMinutes.Value - expectedMinutes.Value;

    public static int ClosingBalance(int openingBalanceMinutes, Minutes workedMinutes, Minutes expectedMinutes) =>
        openingBalanceMinutes + MonthlyDifference(workedMinutes, expectedMinutes);
}

public static class SalaryCalculator {
    public static decimal GrossSalary(Minutes workedMinutes, HourlySalary hourlySalary) =>
        Math.Round(workedMinutes.Hours * hourlySalary.Amount, 2, MidpointRounding.AwayFromZero);

    public static decimal GrossSalary(Minutes workedMinutes, decimal hourlyRate) =>
        GrossSalary(workedMinutes, new HourlySalary(hourlyRate));

    public static decimal GrossSalary(
        Minutes regularMinutes,
        Minutes overtimeMinutes,
        HourlySalary hourlySalary,
        OvertimeCompensationSettings overtimeCompensation) {
        ArgumentNullException.ThrowIfNull(overtimeCompensation);
        decimal regularPay = regularMinutes.Hours * hourlySalary.Amount;
        decimal overtimePay = overtimeCompensation.Mode == OvertimeCompensationMode.Paid
            ? overtimeMinutes.Hours * hourlySalary.Amount * (1m + overtimeCompensation.PremiumPercent / 100m)
            : 0m;
        return Math.Round(regularPay + overtimePay, 2, MidpointRounding.AwayFromZero);
    }
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
        overtimeCompensation ??= OvertimeCompensationSettings.CompTime;

        var entryByDate = entries
            .Where(entry => entry.Date.Year == month.Year && entry.Date.Month == month.Month)
            .ToDictionary(entry => entry.Date);
        var holidays = holidayService ?? new SwedishHolidayService();
        var expectedMinutes = ExpectedHoursCalculator.CalculateExpectedMinutes(
            month.Year,
            month.Month,
            expectedHours,
            holidays,
            month.ExpectedMinutesOverride);
        var monthDates = ExpectedHoursCalculator.GetDates(month.Year, month.Month);
        var expectedWorkdays = ExpectedHoursCalculator.GetExpectedWorkdays(month.Year, month.Month, expectedHours, holidays);
        var expectedSet = expectedWorkdays.ToHashSet();
        var missingPastDays = monthDates
            .Where(date => date < today && expectedSet.Contains(date) &&
                (!entryByDate.TryGetValue(date, out var entry) || entry.Status == WorkEntryStatus.Incomplete))
            .ToArray();
        WorkEntry[] workedEntries = entryByDate.Values
            .Where(entry => entry.Status == WorkEntryStatus.Worked)
            .ToArray();
        var workedMinutes = workedEntries
            .Aggregate(Minutes.Zero, (total, entry) => total + entry.WorkedMinutes);
        var regularMinutes = workedEntries.Aggregate(
            Minutes.Zero,
            (total, entry) => total + new Minutes(Math.Min(entry.WorkedMinutes.Value, expectedHours.DailyMinutes.Value)));
        var overtimeMinutes = workedEntries.Aggregate(
            Minutes.Zero,
            (total, entry) => total + new Minutes(Math.Max(0, entry.WorkedMinutes.Value - expectedHours.DailyMinutes.Value)));
        Minutes balanceEligibleMinutes = overtimeCompensation.Mode == OvertimeCompensationMode.CompTime
            ? workedMinutes
            : regularMinutes;
        var difference = BalanceCalculator.MonthlyDifference(balanceEligibleMinutes, expectedMinutes);

        return new MonthlySummary {
            Year = month.Year,
            Month = month.Month,
            WorkedMinutes = workedMinutes,
            RegularMinutes = regularMinutes,
            OvertimeMinutes = overtimeMinutes,
            BalanceEligibleMinutes = balanceEligibleMinutes,
            ExpectedMinutes = expectedMinutes,
            MonthlyDifferenceMinutes = difference,
            OpeningBalanceMinutes = month.OpeningBalanceMinutes,
            ClosingBalanceMinutes = BalanceCalculator.ClosingBalance(month.OpeningBalanceMinutes, balanceEligibleMinutes, expectedMinutes),
            GrossSalary = SalaryCalculator.GrossSalary(regularMinutes, overtimeMinutes, hourlySalary, overtimeCompensation),
            CompletedDayCount = entryByDate.Values.Count(entry => entry.IsComplete),
            MissingPastDayCount = missingPastDays.Length,
            MissingPastDays = missingPastDays
        };
    }
}
