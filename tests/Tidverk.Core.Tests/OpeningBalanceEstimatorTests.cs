using Tidverk.Core;
using Xunit;

namespace Tidverk.Core.Tests;

public sealed class OpeningBalanceEstimatorTests {
    private static readonly DateOnly Today = new(2026, 7, 31);
    private static readonly AppSettings Settings = new(
        "Alex",
        "Employer",
        "Route A",
        new HourlySalary(200m),
        new ExpectedHoursSettings(8m, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], excludePublicHolidays: false),
        new TimeOnly(8, 0),
        new TimeOnly(16, 30),
        new Minutes(30),
        TaxSettings.Disabled,
        openingBalanceMinutes: 120);

    [Fact]
    public async Task Untouched_history_falls_back_to_the_settings_balance() {
        Estimator estimator = new();

        int balance = await estimator.EstimateAsync(new DateOnly(2026, 7, 1), Settings, Today, TestContext.Current.CancellationToken);

        Assert.Equal(120, balance);
    }

    [Fact]
    public async Task An_edited_month_stops_the_walk_and_wins_over_settings() {
        Estimator estimator = new();
        estimator.Months.Items[(2026, 6)] = new MonthRecord(2026, 6, 999, expectedMinutesOverride: 0, openingBalanceWasEdited: true);

        int balance = await estimator.EstimateAsync(new DateOnly(2026, 7, 1), Settings, Today, TestContext.Current.CancellationToken);

        Assert.Equal(999, balance);
        Assert.Equal(1, estimator.Entries.MonthQueryCount);
    }

    [Fact]
    public async Task A_worked_month_carries_its_closing_balance_forward() {
        Estimator estimator = new();
        estimator.Months.Items[(2026, 5)] = new MonthRecord(2026, 5, 0, expectedMinutesOverride: 0, openingBalanceWasEdited: true);
        DateOnly worked = new(2026, 6, 1);
        estimator.Entries.Items[worked] = WorkEntry.CreateWorked(worked, new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        estimator.Months.Items[(2026, 6)] = new MonthRecord(2026, 6, 0, expectedMinutesOverride: 60);

        int balance = await estimator.EstimateAsync(new DateOnly(2026, 7, 1), Settings, Today, TestContext.Current.CancellationToken);

        Assert.Equal(480 - 60, balance);
    }

    /// <summary>An empty month must pass its opening balance straight through rather than reset it.</summary>
    [Fact]
    public async Task An_empty_month_between_two_edits_passes_the_balance_through() {
        Estimator estimator = new();
        estimator.Months.Items[(2026, 5)] = new MonthRecord(2026, 5, 300, openingBalanceWasEdited: true);

        int balance = await estimator.EstimateAsync(new DateOnly(2026, 7, 1), Settings, Today, TestContext.Current.CancellationToken);

        Assert.Equal(300, balance);
    }

    private sealed class Estimator {
        public FakeWorkEntries Entries { get; } = new();

        public FakeMonths Months { get; } = new();

        public Task<int> EstimateAsync(DateOnly month, AppSettings settings, DateOnly today, CancellationToken cancellationToken) =>
            new OpeningBalanceEstimator(Entries, Months, new SwedishHolidayService()).EstimateAsync(month, settings, today, cancellationToken);
    }

    private sealed class FakeWorkEntries : IWorkEntryRepository {
        public Dictionary<DateOnly, WorkEntry> Items { get; } = [];

        public int MonthQueryCount { get; private set; }

        public Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) {
            MonthQueryCount++;
            return Task.FromResult<IReadOnlyList<WorkEntry>>(Items.Values
                .Where(entry => entry.Date.Year == year && entry.Date.Month == month)
                .ToArray());
        }

        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(date));

        public Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMonths : IMonthRepository {
        public Dictionary<(int Year, int Month), MonthRecord> Items { get; } = [];

        public Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault((year, month)) ?? new MonthRecord(year, month, suggestedOpeningBalance));

        public Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
