using Tidverk.Core;

namespace Tidverk.App.Services;

public sealed record MonthlyWorkspace(
    IReadOnlyList<WorkEntry> Entries,
    MonthRecord Month,
    MonthlySummary Summary,
    TaxEstimate TaxEstimate);

/// <summary>Loads and changes the domain state behind the ledger and calendar.</summary>
public sealed class MonthlyWorkspaceService(
    IWorkEntryRepository workEntries,
    IMonthRepository months,
    ISwedishHolidayService holidays,
    OpeningBalanceEstimator openingBalances,
    IClock clock,
    ITaxCalculator taxes) {
    public DateOnly Today => clock.Today;

    public async Task<MonthlyWorkspace> LoadAsync(
        DateOnly selectedMonth,
        AppSettings settings,
        CancellationToken cancellationToken = default) {
        IReadOnlyList<WorkEntry> entries = await workEntries.GetMonthAsync(
            selectedMonth.Year,
            selectedMonth.Month,
            cancellationToken);
        int suggestedOpeningBalance = await openingBalances.EstimateAsync(
            selectedMonth,
            settings,
            clock.Today,
            cancellationToken);
        MonthRecord month = await months.GetAsync(
            selectedMonth.Year,
            selectedMonth.Month,
            suggestedOpeningBalance,
            cancellationToken);
        MonthlySummary summary = MonthlyCalculator.Calculate(
            month,
            entries,
            settings.ExpectedHours,
            settings.Salary,
            clock.Today,
            holidays,
            settings.OvertimeCompensation);
        return new(entries, month, summary, taxes.Calculate(summary.GrossSalary, settings.TaxSettings));
    }

    public Task SaveEntryAsync(WorkEntry entry, CancellationToken cancellationToken = default) =>
        workEntries.SaveAsync(entry, cancellationToken);

    public Task ResetEntryAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        workEntries.ResetAsync(date, cancellationToken);

    public async Task SaveOpeningBalanceAsync(
        DateOnly selectedMonth,
        int minutes,
        CancellationToken cancellationToken = default) {
        MonthRecord current = await months.GetAsync(
            selectedMonth.Year,
            selectedMonth.Month,
            minutes,
            cancellationToken);
        await months.SaveAsync(
            new MonthRecord(
                selectedMonth.Year,
                selectedMonth.Month,
                minutes,
                current.ExpectedMinutesOverride,
                openingBalanceWasEdited: true),
            cancellationToken);
    }

    public string? GetHolidayName(DateOnly date) => holidays.GetHolidayName(date);

    public bool IsScheduledWorkday(DateOnly date, AppSettings settings) =>
        settings.ExpectedHours.IsScheduledWorkday(date, holidays);

    public Minutes ScheduledMinutes(DateOnly date, AppSettings settings) =>
        settings.ExpectedHours.ExpectedMinutes(date, holidays);

    public decimal GrossSalary(WorkEntry entry, AppSettings settings) => SalaryCalculator.GrossSalary(
        entry,
        settings.ExpectedHours,
        settings.Salary,
        settings.OvertimeCompensation,
        holidays);

    public (int RegularMinutes, int OvertimeMinutes) SplitTime(WorkEntry entry, AppSettings settings) =>
        SalaryCalculator.SplitOvertime(entry, settings.ExpectedHours, settings.OvertimeCompensation, holidays);
}
