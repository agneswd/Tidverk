namespace Tidverk.Core;

public enum TaxMode {
    Disabled,
    PrimaryIncomeTaxTable,
    SecondaryIncomeThirtyPercent,
    ManualMonthlyDeduction
}

public sealed record TaxSettings {
    public TaxSettings(
        TaxMode mode,
        int taxYear = 0,
        int tableNumber = 0,
        int column = 0,
        decimal? manualMonthlyDeduction = null) {
        if (mode == TaxMode.PrimaryIncomeTaxTable && (tableNumber is < 29 or > 42 || column is < 1 or > 6)) {
            throw new ArgumentOutOfRangeException(nameof(tableNumber), "Primary income requires a table from 29 to 42 and a column from 1 to 6.");
        }

        if (manualMonthlyDeduction is < 0) {
            throw new ArgumentOutOfRangeException(nameof(manualMonthlyDeduction));
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
}

public sealed record TaxEstimate {
    private TaxEstimate(decimal grossPay, decimal? preliminaryTax, decimal? estimatedNetPay, bool isAvailable, string? unavailableReason) {
        GrossPay = grossPay;
        PreliminaryTax = preliminaryTax;
        EstimatedNetPay = estimatedNetPay;
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
    }

    public decimal GrossPay { get; }

    public decimal? PreliminaryTax { get; }

    public decimal? EstimatedNetPay { get; }

    public bool IsAvailable { get; }

    public string? UnavailableReason { get; }

    public static TaxEstimate Available(decimal grossPay, decimal preliminaryTax) {
        var tax = Math.Clamp(preliminaryTax, 0m, grossPay);
        return new(grossPay, tax, grossPay - tax, true, null);
    }

    public static TaxEstimate Unavailable(decimal grossPay, string reason) =>
        new(grossPay, null, null, false, reason);
}

public interface IPrimaryIncomeTaxTable {
    bool HasYear(int taxYear) => true;

    decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay);
}

public interface ITaxCalculator {
    TaxEstimate Calculate(decimal grossPay, TaxSettings settings);
}

public sealed class TaxCalculator : ITaxCalculator {
    private readonly IPrimaryIncomeTaxTable? primaryIncomeTable;

    public TaxCalculator(IPrimaryIncomeTaxTable? primaryIncomeTable = null) {
        this.primaryIncomeTable = primaryIncomeTable;
    }

    public TaxEstimate Calculate(decimal grossPay, TaxSettings settings) {
        if (grossPay < 0) {
            throw new ArgumentOutOfRangeException(nameof(grossPay));
        }

        ArgumentNullException.ThrowIfNull(settings);

        return settings.Mode switch {
            TaxMode.Disabled => TaxEstimate.Available(grossPay, 0m),
            TaxMode.SecondaryIncomeThirtyPercent => TaxEstimate.Available(
                grossPay,
                decimal.Truncate(grossPay * 0.30m)),
            TaxMode.ManualMonthlyDeduction => settings.ManualMonthlyDeduction is decimal manual
                ? TaxEstimate.Available(grossPay, manual)
                : TaxEstimate.Unavailable(grossPay, "Manual monthly deduction is not configured."),
            TaxMode.PrimaryIncomeTaxTable when settings.TaxYear <= 0 =>
                TaxEstimate.Unavailable(grossPay, "Tax estimate unavailable for this year."),
            TaxMode.PrimaryIncomeTaxTable when primaryIncomeTable is null =>
                TaxEstimate.Unavailable(grossPay, "Tax estimate unavailable for this year."),
            TaxMode.PrimaryIncomeTaxTable when !primaryIncomeTable.HasYear(settings.TaxYear) =>
                TaxEstimate.Unavailable(grossPay, "Tax estimate unavailable for this year."),
            TaxMode.PrimaryIncomeTaxTable => TaxEstimate.Available(
                grossPay,
                primaryIncomeTable!.GetPreliminaryTax(settings.TaxYear, settings.TableNumber, settings.Column, grossPay)),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown tax mode.")
        };
    }
}
