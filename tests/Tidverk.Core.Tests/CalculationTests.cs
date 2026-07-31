using Tidverk.Core;
using Xunit;

namespace Tidverk.Core.Tests;

public sealed class CalculationTests {
    private static readonly ExpectedHoursSettings EightHourWeek = new(
        8m,
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        excludePublicHolidays: false);

    [Fact]
    public void June_has_thirty_dates_and_expected_hours_use_the_real_month_length() {
        var dates = ExpectedHoursCalculator.GetDates(2026, 6);
        var expected = ExpectedHoursCalculator.CalculateExpectedMinutes(2026, 6, EightHourWeek, new SwedishHolidayService());

        Assert.Equal(30, dates.Count);
        Assert.Equal(176 * 60, expected.Value);
        Assert.Equal(30, dates[^1].Day);
        Assert.Equal(6, dates[^1].Month);
    }

    [Fact]
    public void July_reference_month_has_thirty_one_dates_and_expected_weekdays() {
        var dates = ExpectedHoursCalculator.GetDates(2026, 7);
        var expected = ExpectedHoursCalculator.CalculateExpectedMinutes(2026, 7, EightHourWeek, new SwedishHolidayService());

        Assert.Equal(31, dates.Count);
        Assert.Equal(23 * 8 * 60, expected.Value);
        Assert.Equal(new DateOnly(2026, 7, 31), dates[^1]);
    }

    [Fact]
    public void Leap_february_includes_february_29() {
        var dates = ExpectedHoursCalculator.GetDates(2024, 2);
        var expected = ExpectedHoursCalculator.CalculateExpectedMinutes(2024, 2, EightHourWeek, new SwedishHolidayService());

        Assert.Equal(29, dates.Count);
        Assert.Equal(21 * 8 * 60, expected.Value);
        Assert.Equal(new DateOnly(2024, 2, 29), dates[^1]);
    }

    [Fact]
    public void Missing_days_only_include_past_expected_incomplete_days() {
        var month = new MonthRecord(2026, 6, openingBalanceMinutes: 60);
        var entries = new[]
        {
            WorkEntry.CreateWorked(new DateOnly(2026, 6, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30),
            WorkEntry.CreateOff(new DateOnly(2026, 6, 2))
        };

        var summary = MonthlyCalculator.Calculate(month, entries, EightHourWeek, new HourlySalary(202m), new DateOnly(2026, 6, 4));

        Assert.Equal(new[] { new DateOnly(2026, 6, 3) }, summary.MissingPastDays);
        Assert.Equal(1, summary.MissingPastDayCount);
        Assert.Equal(60 + 480 - 22 * 8 * 60, summary.ClosingBalanceMinutes);
    }

    [Fact]
    public void Future_dates_are_not_missing() {
        var summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 6), [], EightHourWeek, new HourlySalary(200m), new DateOnly(2026, 6, 1));

        Assert.DoesNotContain(new DateOnly(2026, 6, 2), summary.MissingPastDays);
        Assert.Equal(0, summary.MissingPastDayCount);
    }

    [Fact]
    public void Weekend_work_counts_towards_worked_minutes() {
        var entry = WorkEntry.CreateWorked(new DateOnly(2026, 6, 6), new TimeOnly(9, 0), new TimeOnly(13, 0), 0);
        var summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 6), [entry], EightHourWeek, new HourlySalary(200m), new DateOnly(2026, 6, 7));

        Assert.Equal(240, summary.WorkedMinutes.Value);
        Assert.Equal(22 * 8 * 60, summary.ExpectedMinutes.Value);
    }

    [Fact]
    public void Public_holiday_exclusion_changes_expected_hours_only_when_configured() {
        var holidays = new SwedishHolidayService();
        var excluding = new ExpectedHoursSettings(8m, EightHourWeek.WorkingWeekdays, excludePublicHolidays: true);
        var includedDates = ExpectedHoursCalculator.GetExpectedWorkdays(2025, 6, EightHourWeek, holidays);
        var excludedDates = ExpectedHoursCalculator.GetExpectedWorkdays(2025, 6, excluding, holidays);

        Assert.Equal(21 * 8 * 60, ExpectedHoursCalculator.CalculateExpectedMinutes(2025, 6, EightHourWeek, holidays).Value);
        Assert.Equal(20 * 8 * 60, ExpectedHoursCalculator.CalculateExpectedMinutes(2025, 6, excluding, holidays).Value);
        Assert.Contains(new DateOnly(2025, 6, 6), includedDates);
        Assert.DoesNotContain(new DateOnly(2025, 6, 6), excludedDates);
        Assert.DoesNotContain(new DateOnly(2025, 6, 7), excludedDates);
    }

    [Fact]
    public void Month_override_replaces_calculated_expected_minutes() {
        var expected = ExpectedHoursCalculator.CalculateExpectedMinutes(
            2026, 6, EightHourWeek, new SwedishHolidayService(), expectedMinutesOverride: 600);

        Assert.Equal(600, expected.Value);
    }

    [Fact]
    public void Month_override_flows_into_monthly_difference() {
        var entry = WorkEntry.CreateWorked(new DateOnly(2026, 6, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        var summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 6, expectedMinutesOverride: 600),
            [entry],
            EightHourWeek,
            new HourlySalary(200m),
            new DateOnly(2026, 6, 2));

        Assert.Equal(600, summary.ExpectedMinutes.Value);
        Assert.Equal(-120, summary.MonthlyDifferenceMinutes);
    }

    [Fact]
    public void Balance_carries_opening_minutes_into_closing_balance() {
        Assert.Equal(120, BalanceCalculator.ClosingBalance(60, new Minutes(480), new Minutes(420)));
    }

    [Fact]
    public void Monthly_summary_reports_opening_difference_and_closing_balance() {
        var entry = WorkEntry.CreateWorked(new DateOnly(2026, 6, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        var summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 6, openingBalanceMinutes: 60, expectedMinutesOverride: 420),
            [entry],
            EightHourWeek,
            new HourlySalary(200m),
            new DateOnly(2026, 6, 2));

        Assert.Equal(60, summary.OpeningBalanceMinutes);
        Assert.Equal(60, summary.MonthlyDifferenceMinutes);
        Assert.Equal(120, summary.ClosingBalanceMinutes);
    }

    [Fact]
    public void Negative_monthly_difference_does_not_create_negative_worked_hours() {
        var summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 6, expectedMinutesOverride: 480),
            [],
            EightHourWeek,
            new HourlySalary(200m),
            new DateOnly(2026, 6, 2));

        Assert.Equal(0, summary.WorkedMinutes.Value);
        Assert.Equal(0m, summary.GrossSalary);
        Assert.Equal(-480, summary.MonthlyDifferenceMinutes);
        Assert.Equal(-480, summary.ClosingBalanceMinutes);
    }

    [Theory]
    [InlineData(2024, 3, 29)]
    [InlineData(2024, 3, 31)]
    [InlineData(2024, 4, 1)]
    [InlineData(2024, 5, 9)]
    [InlineData(2024, 6, 22)]
    [InlineData(2024, 11, 2)]
    [InlineData(2025, 4, 18)]
    [InlineData(2025, 4, 20)]
    [InlineData(2025, 5, 29)]
    [InlineData(2025, 6, 21)]
    [InlineData(2025, 11, 1)]
    [InlineData(2026, 4, 3)]
    [InlineData(2026, 4, 5)]
    [InlineData(2026, 5, 14)]
    [InlineData(2026, 6, 20)]
    [InlineData(2026, 10, 31)]
    public void Swedish_holidays_include_statutory_dates_across_multiple_years(int year, int month, int day) {
        var service = new SwedishHolidayService();

        Assert.Contains(new DateOnly(year, month, day), service.GetHolidays(year).Select(holiday => holiday.Date));
    }

    [Theory]
    [InlineData(2024, 6, 21)]
    [InlineData(2024, 12, 24)]
    [InlineData(2024, 12, 31)]
    [InlineData(2025, 6, 20)]
    [InlineData(2025, 12, 24)]
    [InlineData(2025, 12, 31)]
    [InlineData(2026, 6, 19)]
    [InlineData(2026, 12, 24)]
    [InlineData(2026, 12, 31)]
    public void Swedish_holidays_exclude_eves(int year, int month, int day) {
        var service = new SwedishHolidayService();

        Assert.DoesNotContain(new DateOnly(year, month, day), service.GetHolidays(year).Select(holiday => holiday.Date));
    }

    [Fact]
    public void Salary_uses_decimal_arithmetic() {
        Assert.Equal(30_704m, SalaryCalculator.GrossSalary(new Minutes(152 * 60), 202m));
        Assert.Equal(185.18m, SalaryCalculator.GrossSalary(new Minutes(90), 123.45m));
    }

    [Fact]
    public void Monthly_salary_caps_each_worked_day_and_tracks_overtime_separately() {
        WorkEntry tenHourDay = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(18, 30), 30);
        WorkEntry sevenAndHalfHourDay = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 2), new TimeOnly(8, 0), new TimeOnly(16, 0), 30);

        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 16 * 60),
            [tenHourDay, sevenAndHalfHourDay],
            EightHourWeek,
            new HourlySalary(202m),
            new DateOnly(2026, 7, 3));

        Assert.Equal(17.5m, summary.WorkedHours);
        Assert.Equal(15.5m, summary.RegularHours);
        Assert.Equal(2m, summary.OvertimeHours);
        Assert.Equal(3_131m, summary.GrossSalary);
        Assert.Equal(90, summary.MonthlyDifferenceMinutes);
    }

    [Fact]
    public void Paid_overtime_increases_salary_but_not_time_balance() {
        WorkEntry tenHourDay = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(18, 30), 30);
        OvertimeCompensationSettings paidOvertime = new(OvertimeCompensationMode.Paid, premiumPercent: 50m);

        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 8 * 60),
            [tenHourDay],
            EightHourWeek,
            new HourlySalary(200m),
            new DateOnly(2026, 7, 2),
            overtimeCompensation: paidOvertime);

        Assert.Equal(2m, summary.OvertimeHours);
        Assert.Equal(2_200m, summary.GrossSalary);
        Assert.Equal(0, summary.MonthlyDifferenceMinutes);
        Assert.Equal(0, summary.ClosingBalanceMinutes);
    }

    [Fact]
    public void Tax_modes_use_explicit_whole_krona_secondary_withholding() {
        var calculator = new TaxCalculator(new FakeTaxTable());
        var disabled = calculator.Calculate(1_000m, TaxSettings.Disabled);
        var secondary = calculator.Calculate(1_000.99m, new TaxSettings(TaxMode.SecondaryIncomeThirtyPercent));
        var manual = calculator.Calculate(1_000m, new TaxSettings(TaxMode.ManualMonthlyDeduction, manualMonthlyDeduction: 125m));
        var primary = calculator.Calculate(30_704m, new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1));
        var unavailable = new TaxCalculator().Calculate(30_704m, new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1));

        Assert.True(disabled.IsAvailable);
        Assert.Equal(0m, disabled.PreliminaryTax);
        Assert.Equal(1_000m, disabled.EstimatedNetPay);
        Assert.Equal(300m, secondary.PreliminaryTax);
        Assert.Equal(700.99m, secondary.EstimatedNetPay);
        Assert.Equal(125m, manual.PreliminaryTax);
        Assert.Equal(875m, manual.EstimatedNetPay);
        Assert.Equal(6_079m, primary.PreliminaryTax);
        Assert.Equal(24_625m, primary.EstimatedNetPay);
        Assert.False(unavailable.IsAvailable);
        Assert.Null(unavailable.PreliminaryTax);
        Assert.Equal("Tax estimate unavailable for this year.", unavailable.UnavailableReason);
    }

    private sealed class FakeTaxTable : IPrimaryIncomeTaxTable {
        public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) => 6_079m;
    }
}
