namespace Tidverk.Core;

/// <summary>Why an estimate could not be produced. Callers localize this; the enum is the contract.</summary>
public enum TaxUnavailableReason {
    None,
    ManualDeductionNotConfigured,
    TaxYearNotBundled
}

public sealed record TaxEstimate {
    private TaxEstimate(decimal grossPay, decimal? preliminaryTax, decimal? estimatedNetPay, TaxUnavailableReason unavailableReason) {
        GrossPay = grossPay;
        PreliminaryTax = preliminaryTax;
        EstimatedNetPay = estimatedNetPay;
        UnavailableReason = unavailableReason;
    }

    public decimal GrossPay { get; }

    public decimal? PreliminaryTax { get; }

    public decimal? EstimatedNetPay { get; }

    public TaxUnavailableReason UnavailableReason { get; }

    public bool IsAvailable => UnavailableReason == TaxUnavailableReason.None;

    /// <summary>Withholding is clamped to the gross pay so the net estimate can never go negative.</summary>
    public static TaxEstimate Available(decimal grossPay, decimal preliminaryTax) {
        decimal tax = Math.Clamp(preliminaryTax, 0m, grossPay);
        return new(grossPay, tax, grossPay - tax, TaxUnavailableReason.None);
    }

    public static TaxEstimate Unavailable(decimal grossPay, TaxUnavailableReason reason) {
        if (reason == TaxUnavailableReason.None) {
            throw new ArgumentOutOfRangeException(nameof(reason), "An unavailable estimate needs a reason.");
        }

        return new(grossPay, null, null, reason);
    }
}

/// <summary>Skatteverket's published withholding tables for a bundled tax year.</summary>
public interface IPrimaryIncomeTaxTable {
    bool HasYear(int taxYear);

    decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay);
}

public interface ITaxCalculator {
    TaxEstimate Calculate(decimal grossPay, TaxSettings settings);
}

public sealed class TaxCalculator(IPrimaryIncomeTaxTable? primaryIncomeTable = null) : ITaxCalculator {
    public TaxEstimate Calculate(decimal grossPay, TaxSettings settings) {
        ArgumentOutOfRangeException.ThrowIfNegative(grossPay);
        ArgumentNullException.ThrowIfNull(settings);

        return settings.Mode switch {
            TaxMode.Disabled => TaxEstimate.Available(grossPay, 0m),
            TaxMode.SecondaryIncomeThirtyPercent => TaxEstimate.Available(grossPay, decimal.Truncate(grossPay * 0.30m)),
            TaxMode.ManualMonthlyDeduction => settings.ManualMonthlyDeduction is decimal manual
                ? TaxEstimate.Available(grossPay, manual)
                : TaxEstimate.Unavailable(grossPay, TaxUnavailableReason.ManualDeductionNotConfigured),
            TaxMode.PrimaryIncomeTaxTable => FromTable(grossPay, settings),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown tax mode.")
        };
    }

    private TaxEstimate FromTable(decimal grossPay, TaxSettings settings) {
        if (settings.TaxYear <= 0 || primaryIncomeTable is null || !primaryIncomeTable.HasYear(settings.TaxYear)) {
            return TaxEstimate.Unavailable(grossPay, TaxUnavailableReason.TaxYearNotBundled);
        }

        return TaxEstimate.Available(
            grossPay,
            primaryIncomeTable.GetPreliminaryTax(settings.TaxYear, settings.TableNumber, settings.Column, grossPay));
    }
}
