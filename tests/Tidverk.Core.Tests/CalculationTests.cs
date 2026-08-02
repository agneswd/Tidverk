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
    public void Paid_overtime_uses_daily_threshold_and_highest_matching_time_band() {
        WorkEntry entry = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(20, 0), 0);
        OvertimeCompensationSettings policy = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 10m,
            dailyThresholdHours: 8m,
            rateBands: [
                new("Early evening", OvertimeDayCategory.ScheduledWorkdays, new TimeOnly(17, 0), new TimeOnly(18, 0), 20m),
                new("Overlap", OvertimeDayCategory.Wednesday, new TimeOnly(17, 30), new TimeOnly(18, 30), 40m),
                new("Late evening", OvertimeDayCategory.AllDays, new TimeOnly(18, 0), new TimeOnly(21, 0), 50m)
            ]);

        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 8 * 60),
            [entry],
            EightHourWeek,
            new HourlySalary(100m),
            new DateOnly(2026, 7, 2),
            overtimeCompensation: policy);

        Assert.Equal(8m, summary.RegularHours);
        Assert.Equal(4m, summary.OvertimeHours);
        Assert.Equal(1_340m, summary.GrossSalary);
    }

    [Fact]
    public void Monthly_salary_uses_full_time_divisors_for_citymail_overtime() {
        WorkEntry entry = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(14, 0), 0);
        SalarySettings salary = new(SalaryType.Monthly, new HourlySalary(0m), monthlySalary: 12_123m, employmentPercent: 50m);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 72m,
            rateBands: [
                new(
                    "Simple overtime",
                    OvertimeDayCategory.ScheduledWeekdays,
                    new TimeOnly(6, 0),
                    new TimeOnly(20, 0),
                    premiumPercent: 0m,
                    rateType: CompensationRateType.FullTimeMonthlySalaryDivisor,
                    rateValue: 94m)
            ],
            thresholdMode: OvertimeThresholdMode.ScheduledHours,
            defaultRateType: CompensationRateType.FullTimeMonthlySalaryDivisor);
        ExpectedHoursSettings schedule = new(4m, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], true);

        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 4 * 60),
            [entry],
            schedule,
            salary,
            new DateOnly(2026, 7, 2),
            overtimeCompensation: compensation);

        Assert.Equal(24_246m, salary.FullTimeMonthlySalary);
        Assert.Equal(4m, summary.RegularHours);
        Assert.Equal(2m, summary.OvertimeHours);
        Assert.Equal(515.87m, summary.OvertimeCompensation);
        Assert.Equal(12_638.87m, summary.GrossSalary);
        Assert.Equal(0, summary.ClosingBalanceMinutes);
    }

    [Fact]
    public void Zero_scheduled_hours_make_the_whole_entry_paid_overtime_without_moving_balance() {
        WorkEntry entry = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 4),
            new TimeOnly(8, 0),
            new TimeOnly(11, 0),
            0,
            scheduledMinutesOverride: 0);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 50m,
            thresholdMode: OvertimeThresholdMode.ScheduledHours);

        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 0),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(100m),
            new DateOnly(2026, 7, 5),
            overtimeCompensation: compensation);

        Assert.Equal(0m, summary.RegularHours);
        Assert.Equal(3m, summary.OvertimeHours);
        Assert.Equal(450m, summary.GrossSalary);
        Assert.Equal(0, summary.ClosingBalanceMinutes);
    }

    [Fact]
    public void Monthly_salary_ob_uses_divisor_and_also_covers_overtime_minutes() {
        SalarySettings salary = new(SalaryType.Monthly, new HourlySalary(0m), monthlySalary: 12_123m, employmentPercent: 50m);
        OvertimeRateBand weekendOb = new(
            "Weekend OB",
            OvertimeDayCategory.Weekends,
            TimeOnly.MinValue,
            TimeOnly.MinValue,
            premiumPercent: 0m,
            compensationType: CompensationRuleType.Ob,
            rateType: CompensationRateType.FullTimeMonthlySalaryDivisor,
            rateValue: 400m);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 72m,
            rateBands: [weekendOb],
            thresholdMode: OvertimeThresholdMode.ScheduledHours,
            defaultRateType: CompensationRateType.FullTimeMonthlySalaryDivisor);
        ExpectedHoursSettings saturdaySchedule = new(4m, [DayOfWeek.Saturday], excludePublicHolidays: false);
        WorkEntry ordinary = WorkEntry.CreateWorked(new DateOnly(2026, 7, 4), new TimeOnly(8, 0), new TimeOnly(12, 0), 0);
        WorkEntry overtimeOnly = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 11),
            new TimeOnly(8, 0),
            new TimeOnly(11, 0),
            0,
            scheduledMinutesOverride: 0);

        DailyPayBreakdown ordinaryPay = SalaryCalculator.CalculatePay(ordinary, saturdaySchedule, salary, compensation, new SwedishHolidayService());
        DailyPayBreakdown overtimePay = SalaryCalculator.CalculatePay(overtimeOnly, saturdaySchedule, salary, compensation, new SwedishHolidayService());

        Assert.Equal(242.46m, ordinaryPay.ObPay);
        Assert.Equal(4m, ordinaryPay.ObMinutes.Hours);

        // A shift with no scheduled hours is entirely overtime. OB compensates the inconvenient hours
        // themselves, so it still accrues; the overtime premium is priced separately on top.
        Assert.Equal(181.85m, overtimePay.ObPay);
        Assert.Equal(3m, overtimePay.ObMinutes.Hours);
        Assert.Equal(1_010.25m, overtimePay.OvertimePay);
    }

    [Fact]
    public void Fixed_overtime_threshold_accepts_zero() {
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(10, 0), 0);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.Paid,
            premiumPercent: 50m,
            dailyThresholdHours: 0m);

        (int regular, int overtime) = SalaryCalculator.SplitOvertime(
            entry,
            ExpectedHoursSettings.Standard,
            compensation,
            new SwedishHolidayService());

        Assert.Equal(0, regular);
        Assert.Equal(120, overtime);
    }

    [Fact]
    public void Overnight_shift_counts_the_hours_worked_past_midnight() {
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(22, 0), new TimeOnly(6, 0), 30);

        Assert.True(entry.CrossesMidnight);
        Assert.Equal(450, entry.WorkedMinutes.Value);
        Assert.Equal(new DateOnly(2026, 7, 2), entry.ClockAt(150).Date);
        Assert.Equal(new TimeOnly(0, 30), entry.ClockAt(150).Time);
    }

    [Fact]
    public void Overnight_ob_is_priced_by_the_clock_time_and_date_each_minute_falls_on() {
        // Two hours of night OB before midnight and six after it, on the following calendar date.
        OvertimeRateBand nightOb = new(
            "Night OB",
            OvertimeDayCategory.AllDays,
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            premiumPercent: 0m,
            compensationType: CompensationRuleType.Ob,
            rateType: CompensationRateType.FixedHourlyAmount,
            rateValue: 50m);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.CompTime,
            rateBands: [nightOb],
            thresholdMode: OvertimeThresholdMode.ScheduledHours);
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(22, 0), new TimeOnly(6, 0), 0);

        DailyPayBreakdown pay = SalaryCalculator.CalculatePay(
            entry,
            EightHourWeek,
            SalarySettings.Hourly(new HourlySalary(200m)),
            compensation,
            new SwedishHolidayService());

        Assert.Equal(8m, pay.ObMinutes.Hours);
        Assert.Equal(400m, pay.ObPay);
    }

    [Fact]
    public void Overnight_ob_stops_at_the_band_edge_after_midnight() {
        OvertimeRateBand nightOb = new(
            "Night OB",
            OvertimeDayCategory.AllDays,
            new TimeOnly(22, 0),
            new TimeOnly(2, 0),
            premiumPercent: 0m,
            compensationType: CompensationRuleType.Ob,
            rateType: CompensationRateType.FixedHourlyAmount,
            rateValue: 60m);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.CompTime,
            rateBands: [nightOb],
            thresholdMode: OvertimeThresholdMode.ScheduledHours);
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(21, 0), new TimeOnly(5, 0), 0);

        DailyPayBreakdown pay = SalaryCalculator.CalculatePay(
            entry,
            EightHourWeek,
            SalarySettings.Hourly(new HourlySalary(200m)),
            compensation,
            new SwedishHolidayService());

        // 22:00-02:00 qualifies; the hour before and the three hours after do not.
        Assert.Equal(4m, pay.ObMinutes.Hours);
        Assert.Equal(240m, pay.ObPay);
    }

    [Fact]
    public void Lunch_sits_between_ordinary_and_overtime_so_evening_ob_starts_on_the_clock() {
        // 08:00-19:00 with a 60-minute lunch: ten worked hours, eight ordinary then two overtime.
        // The overtime block therefore runs 17:00-19:00 and only that meets an 18:00 evening band.
        OvertimeRateBand eveningOb = new(
            "Evening OB",
            OvertimeDayCategory.AllDays,
            new TimeOnly(18, 0),
            new TimeOnly(22, 0),
            premiumPercent: 0m,
            compensationType: CompensationRuleType.Ob,
            rateType: CompensationRateType.FixedHourlyAmount,
            rateValue: 40m);
        OvertimeCompensationSettings compensation = new(
            OvertimeCompensationMode.CompTime,
            rateBands: [eveningOb],
            thresholdMode: OvertimeThresholdMode.ScheduledHours);
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(19, 0), 60);

        DailyPayBreakdown pay = SalaryCalculator.CalculatePay(
            entry,
            EightHourWeek,
            SalarySettings.Hourly(new HourlySalary(200m)),
            compensation,
            new SwedishHolidayService());

        Assert.Equal(600, entry.WorkedMinutes.Value);
        Assert.Equal(1m, pay.ObMinutes.Hours);
        Assert.Equal(40m, pay.ObPay);
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
        Assert.Equal(TaxUnavailableReason.TaxYearNotBundled, unavailable.UnavailableReason);
    }

    private sealed class FakeTaxTable : IPrimaryIncomeTaxTable {
        public bool HasYear(int taxYear) => true;

        public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) => 6_079m;
    }
}
