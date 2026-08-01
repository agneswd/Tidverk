namespace Tidverk.Infrastructure.Tax;

/// <summary>
/// One bundled tax year, exactly as imported from Skatteverket. The source metadata and checksum
/// are kept so a table can be traced back to the file it came from.
/// </summary>
public sealed record TaxTableFile(
    int TaxYear,
    string SourceFileName,
    string SourceTitle,
    DateTimeOffset ImportedAt,
    string Sha256,
    IReadOnlyList<TaxTableRange> Ranges);

/// <summary>
/// Withholding for one monthly income bracket of one table. <paramref name="AmountKind"/> is 'B' when
/// the columns hold whole kronor and '%' when they hold a percentage of the income.
/// </summary>
public sealed record TaxTableRange(
    int TableNumber,
    int LowerBound,
    int UpperBound,
    char AmountKind,
    IReadOnlyList<decimal> Columns);
