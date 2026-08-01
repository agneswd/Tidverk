namespace Tidverk.Core;

public enum TaxMode {
    Disabled,

    /// <summary>Withholding read from Skatteverket's monthly table for primary income.</summary>
    PrimaryIncomeTaxTable,

    /// <summary>The flat 30% withheld from secondary income.</summary>
    SecondaryIncomeThirtyPercent,

    /// <summary>A fixed amount the user enters.</summary>
    ManualMonthlyDeduction
}

public sealed record TaxSettings {
    /// <summary>Skatteverket publishes monthly tables 29-42, each with six columns.</summary>
    public const int MinimumTableNumber = 29;
    public const int MaximumTableNumber = 42;
    public const int MinimumColumn = 1;
    public const int MaximumColumn = 6;

    public TaxSettings(
        TaxMode mode,
        int taxYear = 0,
        int tableNumber = 0,
        int column = 0,
        decimal? manualMonthlyDeduction = null) {
        if (mode == TaxMode.PrimaryIncomeTaxTable && !IsValidTable(tableNumber, column)) {
            throw new ArgumentOutOfRangeException(
                nameof(tableNumber),
                $"Primary income requires a table from {MinimumTableNumber} to {MaximumTableNumber} and a column from {MinimumColumn} to {MaximumColumn}.");
        }

        if (manualMonthlyDeduction is < 0) {
            throw new ArgumentOutOfRangeException(nameof(manualMonthlyDeduction), "A manual deduction cannot be negative.");
        }

        Mode = mode;
        TaxYear = taxYear;
        TableNumber = tableNumber;
        Column = column;
        ManualMonthlyDeduction = manualMonthlyDeduction;
    }

    public TaxMode Mode { get; }

    public int TaxYear { get; }

    public int TableNumber { get; }

    public int Column { get; }

    public decimal? ManualMonthlyDeduction { get; }

    public static TaxSettings Disabled { get; } = new(TaxMode.Disabled);

    public static bool IsValidTable(int tableNumber, int column) =>
        tableNumber >= MinimumTableNumber && tableNumber <= MaximumTableNumber &&
        column >= MinimumColumn && column <= MaximumColumn;
}
