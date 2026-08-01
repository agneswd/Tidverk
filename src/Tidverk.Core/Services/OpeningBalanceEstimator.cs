namespace Tidverk.Core;

/// <summary>
/// Works out what a month should open with by replaying earlier months forward. It walks back until
/// it finds a month whose opening balance the user edited by hand, because that value is authoritative
/// and nothing before it can change the result.
/// </summary>
public sealed class OpeningBalanceEstimator(IWorkEntryRepository workEntries, IMonthRepository months, ISwedishHolidayService holidays) {
    /// <summary>How far back to replay before giving up and treating the settings balance as the start.</summary>
    private const int MaximumMonthsOfHistory = 120;

    public async Task<int> EstimateAsync(
        DateOnly month,
        AppSettings settings,
        DateOnly today,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(settings);

        List<(MonthRecord Month, IReadOnlyList<WorkEntry> Entries)> history = [];
        DateOnly cursor = month.AddMonths(-1);
        for (int count = 0; count < MaximumMonthsOfHistory; count++, cursor = cursor.AddMonths(-1)) {
            IReadOnlyList<WorkEntry> entries = await workEntries.GetMonthAsync(cursor.Year, cursor.Month, cancellationToken).ConfigureAwait(false);
            MonthRecord record = await months.GetAsync(cursor.Year, cursor.Month, settings.OpeningBalanceMinutes, cancellationToken).ConfigureAwait(false);
            history.Add((record, entries));
            if (record.OpeningBalanceWasEdited) {
                break;
            }
        }

        int balance = settings.OpeningBalanceMinutes;
        for (int index = history.Count - 1; index >= 0; index--) {
            (MonthRecord record, IReadOnlyList<WorkEntry> entries) = history[index];
            int opening = record.OpeningBalanceWasEdited ? record.OpeningBalanceMinutes : balance;
            balance = entries.Any(entry => entry.Status != WorkEntryStatus.Incomplete)
                ? CloseMonth(record, entries, opening, settings, today)
                : opening;
        }

        return balance;
    }

    private int CloseMonth(
        MonthRecord record,
        IReadOnlyList<WorkEntry> entries,
        int openingBalance,
        AppSettings settings,
        DateOnly today) {
        MonthRecord carried = new(record.Year, record.Month, openingBalance, record.ExpectedMinutesOverride, record.OpeningBalanceWasEdited);
        return MonthlyCalculator.Calculate(
            carried,
            entries,
            settings.ExpectedHours,
            settings.HourlySalary,
            today,
            holidays,
            settings.OvertimeCompensation).ClosingBalanceMinutes;
    }
}
