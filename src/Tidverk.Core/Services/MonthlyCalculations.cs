namespace Tidverk.Core;

/// <summary>Everything the month view and the export need about one month, computed in one pass.</summary>
public sealed record MonthlySummary {
    public required int Year { get; init; }

    public required int Month { get; init; }

    public required Minutes WorkedMinutes { get; init; }

    public required Minutes RegularMinutes { get; init; }

    public required Minutes OvertimeMinutes { get; init; }

    /// <summary>Ordinary hours included in hourly pay after applying the selected pay basis.</summary>
    public required Minutes OrdinaryPaidMinutes { get; init; }

    /// <summary>The worked minutes that move the time balance; paid overtime is excluded.</summary>
    public required Minutes BalanceEligibleMinutes { get; init; }

    public required Minutes ExpectedMinutes { get; init; }

    public required int MonthlyDifferenceMinutes { get; init; }

    public required int OpeningBalanceMinutes { get; init; }

    public required int ClosingBalanceMinutes { get; init; }

    public required decimal GrossSalary { get; init; }

    public required decimal BaseSalary { get; init; }

    public required decimal OvertimeCompensation { get; init; }

    public required decimal ObCompensation { get; init; }

    public required Minutes ObMinutes { get; init; }

    public required int CompletedDayCount { get; init; }

    public required IReadOnlyList<DateOnly> MissingPastDays { get; init; }

    public int MissingPastDayCount => MissingPastDays.Count;

    public decimal WorkedHours => WorkedMinutes.Hours;

    public decimal RegularHours => RegularMinutes.Hours;

    public decimal OvertimeHours => OvertimeMinutes.Hours;

    public decimal OrdinaryPaidHours => OrdinaryPaidMinutes.Hours;

    public decimal ObHours => ObMinutes.Hours;

    public decimal ExpectedHours => ExpectedMinutes.Hours;

    /// <summary>Pay earned from ordinary hourly work, with the monthly base, overtime and OB taken out.</summary>
    public decimal OrdinaryPay => GrossSalary - BaseSalary - OvertimeCompensation - ObCompensation;
}

public readonly record struct DailyPayBreakdown(
    decimal RegularPay,
    decimal OvertimePay,
    decimal ObPay,
    Minutes ObMinutes) {
    public decimal Total => RegularPay + OvertimePay + ObPay;
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
        ISwedishHolidayService holidayService) =>
        GrossSalary(entry, expectedHours, SalarySettings.Hourly(hourlySalary), overtimeCompensation, holidayService);

    public static decimal GrossSalary(
        WorkEntry entry,
        ExpectedHoursSettings expectedHours,
        SalarySettings salary,
        OvertimeCompensationSettings overtimeCompensation,
        ISwedishHolidayService holidayService) =>
        CalculatePay(entry, expectedHours, salary, overtimeCompensation, holidayService).Total;

    public static DailyPayBreakdown CalculatePay(
        WorkEntry entry,
        ExpectedHoursSettings expectedHours,
        SalarySettings salary,
        OvertimeCompensationSettings overtimeCompensation,
        ISwedishHolidayService holidayService) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ArgumentNullException.ThrowIfNull(salary);
        ArgumentNullException.ThrowIfNull(overtimeCompensation);
        ArgumentNullException.ThrowIfNull(holidayService);
        if (entry.Status != WorkEntryStatus.Worked || entry.EndTime is null) {
            return new(0m, 0m, 0m, Minutes.Zero);
        }

        (int regularMinutes, int overtimeMinutes) = SplitOvertime(entry, expectedHours, overtimeCompensation, holidayService);
        decimal regularPay = salary.Type == SalaryType.Hourly
            ? regularMinutes * salary.HourlySalary.Amount / 60m
            : 0m;
        bool paysOvertime = overtimeCompensation.Mode == OvertimeCompensationMode.Paid && overtimeMinutes > 0;
        bool paysOb = overtimeCompensation.RateBands.Any(band => band.CompensationType == CompensationRuleType.Ob);
        if (!paysOvertime && !paysOb) {
            return new(Round(regularPay), 0m, 0m, Minutes.Zero);
        }

        // Lunch is unpaid and sits between the ordinary block and the overtime block, so the two
        // blocks together span the whole shift and every minute lands on its real clock time.
        int lunchMinutes = entry.LunchMinutes.Value;
        DayContext days = new(expectedHours, holidayService);
        decimal overtimePay = 0m;
        decimal obPay = 0m;
        int obMinutes = 0;
        for (int minute = 0; minute < regularMinutes + overtimeMinutes; minute++) {
            bool isOvertimeMinute = minute >= regularMinutes;
            (DateOnly date, TimeOnly time) = entry.ClockAt(isOvertimeMinute ? minute + lunchMinutes : minute);
            (bool isScheduledWorkday, bool isPublicHoliday) = days.For(date);
            bool isMajorHoliday = holidayService.IsMajorHolidayPeriod(date, time);

            // Agreements differ on whether OB and overtime can cover the same minute. The selected
            // combination controls OB; overtime is priced separately below.
            bool paysObForMinute = paysOb && (!isOvertimeMinute ||
                overtimeCompensation.ObOvertimeCombination == ObOvertimeCombinationMode.IncludeOb);
            if (paysObForMinute) {
                decimal obAmount = overtimeCompensation.HourlyAmountAt(
                    CompensationRuleType.Ob, salary, date, time, isScheduledWorkday, isPublicHoliday, isMajorHoliday);
                if (obAmount > 0m) {
                    obPay += obAmount / 60m;
                    obMinutes++;
                }
            }

            if (isOvertimeMinute && paysOvertime) {
                overtimePay += overtimeCompensation.HourlyAmountAt(
                    CompensationRuleType.Overtime, salary, date, time, isScheduledWorkday, isPublicHoliday, isMajorHoliday) / 60m;
            }
        }

        return new(Round(regularPay), Round(overtimePay), Round(obPay), new(obMinutes));
    }

    /// <summary>Caches the per-date schedule and holiday lookups a shift needs; it spans at most two dates.</summary>
    private sealed class DayContext(ExpectedHoursSettings expectedHours, ISwedishHolidayService holidays) {
        private readonly Dictionary<DateOnly, (bool IsScheduledWorkday, bool IsPublicHoliday)> cache = [];

        public (bool IsScheduledWorkday, bool IsPublicHoliday) For(DateOnly date) {
            if (!cache.TryGetValue(date, out (bool IsScheduledWorkday, bool IsPublicHoliday) flags)) {
                flags = (expectedHours.IsScheduledWorkday(date, holidays), holidays.IsPublicHoliday(date));
                cache[date] = flags;
            }

            return flags;
        }
    }

    internal static (int RegularMinutes, int OvertimeMinutes) SplitOvertime(Minutes worked, OvertimeCompensationSettings overtimeCompensation) {
        int regular = Math.Min(worked.Value, overtimeCompensation.DailyThresholdMinutes.Value);
        return (regular, worked.Value - regular);
    }

    public static (int RegularMinutes, int OvertimeMinutes) SplitOvertime(
        WorkEntry entry,
        ExpectedHoursSettings expectedHours,
        OvertimeCompensationSettings overtimeCompensation,
        ISwedishHolidayService holidayService) {
        Minutes threshold = overtimeCompensation.ThresholdFor(entry, expectedHours, holidayService);
        int regular = Math.Min(entry.WorkedMinutes.Value, threshold.Value);
        return (regular, entry.WorkedMinutes.Value - regular);
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
        return Calculate(
            month,
            entries,
            expectedHours,
            SalarySettings.Hourly(hourlySalary),
            today,
            holidayService,
            overtimeCompensation);
    }

    public static MonthlySummary Calculate(
        MonthRecord month,
        IEnumerable<WorkEntry> entries,
        ExpectedHoursSettings expectedHours,
        SalarySettings salary,
        DateOnly today,
        ISwedishHolidayService? holidayService = null,
        OvertimeCompensationSettings? overtimeCompensation = null) {
        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(expectedHours);
        ArgumentNullException.ThrowIfNull(salary);
        ISwedishHolidayService holidays = holidayService ?? new SwedishHolidayService();
        OvertimeCompensationSettings overtime = overtimeCompensation ?? OvertimeCompensationSettings.CompTime;

        Dictionary<DateOnly, WorkEntry> entriesByDate = entries
            .Where(entry => entry.Date.Year == month.Year && entry.Date.Month == month.Month)
            .ToDictionary(entry => entry.Date);
        Minutes expectedMinutes = CalculateExpectedMinutes(month, entriesByDate.Values, expectedHours, holidays);
        DateOnly[] missingPastDays = ExpectedHoursCalculator
            .GetExpectedWorkdays(month.Year, month.Month, expectedHours, holidays)
            .Where(date => date < today && IsUnfilled(entriesByDate, date))
            .ToArray();

        MonthTotals totals = CalculateTotals(entriesByDate.Values, expectedHours, salary, overtime, holidays);

        Minutes balanceEligibleMinutes = overtime.Mode == OvertimeCompensationMode.CompTime
            ? totals.Worked
            : totals.Regular;
        Minutes ordinaryPaidMinutes = salary.Type == SalaryType.Hourly ? totals.Regular : Minutes.Zero;
        decimal grossSalary = totals.GrossSalary;
        if (salary.Type == SalaryType.Hourly &&
            overtime.Mode == OvertimeCompensationMode.CompTime &&
            salary.HourlyPayBasis == HourlyPayBasis.MonthlyExpectedHours) {
            ordinaryPaidMinutes = new(Math.Min(totals.Worked.Value, expectedMinutes.Value));
            grossSalary = SalaryCalculator.GrossSalary(ordinaryPaidMinutes, salary.HourlySalary) + totals.ObPay;
        }

        return new MonthlySummary {
            Year = month.Year,
            Month = month.Month,
            WorkedMinutes = totals.Worked,
            RegularMinutes = totals.Regular,
            OvertimeMinutes = totals.Overtime,
            OrdinaryPaidMinutes = ordinaryPaidMinutes,
            BalanceEligibleMinutes = balanceEligibleMinutes,
            ExpectedMinutes = expectedMinutes,
            MonthlyDifferenceMinutes = BalanceCalculator.MonthlyDifference(balanceEligibleMinutes, expectedMinutes),
            OpeningBalanceMinutes = month.OpeningBalanceMinutes,
            ClosingBalanceMinutes = BalanceCalculator.ClosingBalance(month.OpeningBalanceMinutes, balanceEligibleMinutes, expectedMinutes),
            GrossSalary = Math.Round(grossSalary, 2, MidpointRounding.AwayFromZero),
            BaseSalary = salary.BaseMonthlyPay,
            OvertimeCompensation = totals.OvertimePay,
            ObCompensation = totals.ObPay,
            ObMinutes = totals.ObMinutes,
            CompletedDayCount = entriesByDate.Values.Count(entry => entry.IsComplete),
            MissingPastDays = missingPastDays
        };
    }

    private static Minutes CalculateExpectedMinutes(
        MonthRecord month,
        IEnumerable<WorkEntry> entries,
        ExpectedHoursSettings expectedHours,
        ISwedishHolidayService holidays) {
        Minutes expected = ExpectedHoursCalculator.CalculateExpectedMinutes(
            month.Year,
            month.Month,
            expectedHours,
            holidays,
            month.ExpectedMinutesOverride);
        if (month.ExpectedMinutesOverride is not null) {
            return expected;
        }

        int adjusted = expected.Value;
        foreach (WorkEntry entry in entries.Where(entry => entry.ScheduledMinutesOverride is not null)) {
            adjusted -= expectedHours.ExpectedMinutes(entry.Date, holidays).Value;
            adjusted += entry.ScheduledMinutesOverride!.Value;
        }

        return new(Math.Max(0, adjusted));
    }

    private static MonthTotals CalculateTotals(
        IEnumerable<WorkEntry> entries,
        ExpectedHoursSettings expectedHours,
        SalarySettings salary,
        OvertimeCompensationSettings overtime,
        ISwedishHolidayService holidays) {
        MonthTotals totals = new(Minutes.Zero, Minutes.Zero, Minutes.Zero, salary.BaseMonthlyPay, 0m, 0m, Minutes.Zero);
        foreach (WorkEntry entry in entries.Where(entry => entry.Status == WorkEntryStatus.Worked)) {
            (int regular, int overtimeForDay) = SalaryCalculator.SplitOvertime(entry, expectedHours, overtime, holidays);
            DailyPayBreakdown pay = SalaryCalculator.CalculatePay(entry, expectedHours, salary, overtime, holidays);
            totals = totals with {
                Worked = totals.Worked + entry.WorkedMinutes,
                Regular = totals.Regular + new Minutes(regular),
                Overtime = totals.Overtime + new Minutes(overtimeForDay),
                GrossSalary = totals.GrossSalary + pay.Total,
                OvertimePay = totals.OvertimePay + pay.OvertimePay,
                ObPay = totals.ObPay + pay.ObPay,
                ObMinutes = totals.ObMinutes + pay.ObMinutes
            };
        }

        return totals;
    }

    private static bool IsUnfilled(Dictionary<DateOnly, WorkEntry> entriesByDate, DateOnly date) =>
        !entriesByDate.TryGetValue(date, out WorkEntry? entry) || entry.Status == WorkEntryStatus.Incomplete;

    private readonly record struct MonthTotals(
        Minutes Worked,
        Minutes Regular,
        Minutes Overtime,
        decimal GrossSalary,
        decimal OvertimePay,
        decimal ObPay,
        Minutes ObMinutes);
}
