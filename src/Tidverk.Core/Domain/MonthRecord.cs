namespace Tidverk.Core;

/// <summary>Per-month bookkeeping that the daily entries cannot express on their own.</summary>
public sealed record MonthRecord {
    public MonthRecord(
        int year,
        int month,
        int openingBalanceMinutes = 0,
        int? expectedMinutesOverride = null,
        bool openingBalanceWasEdited = false) {
        _ = new DateOnly(year, month, 1); // Rejects impossible year/month combinations.
        if (expectedMinutesOverride < 0) {
            throw new ArgumentOutOfRangeException(nameof(expectedMinutesOverride), "Expected minutes cannot be negative.");
        }

        Year = year;
        Month = month;
        OpeningBalanceMinutes = openingBalanceMinutes;
        ExpectedMinutesOverride = expectedMinutesOverride;
        OpeningBalanceWasEdited = openingBalanceWasEdited;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>The time balance carried in from earlier months. May be negative.</summary>
    public int OpeningBalanceMinutes { get; }

    public int? ExpectedMinutesOverride { get; }

    /// <summary>Set when the user typed the opening balance, which stops it being recalculated from history.</summary>
    public bool OpeningBalanceWasEdited { get; }
}
