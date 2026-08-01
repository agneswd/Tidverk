using System.Collections.Frozen;
using System.Text.Json;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Tax;

/// <summary>
/// Reads the tax tables bundled with the application. Nothing is fetched at runtime: a year is
/// either shipped with the build or reported as unavailable.
/// </summary>
public sealed class JsonTaxTableProvider : IPrimaryIncomeTaxTable {
    private readonly FrozenDictionary<int, TaxYearTable> years;

    public JsonTaxTableProvider(string dataDirectory) {
        years = Directory.Exists(dataDirectory)
            ? Directory.GetFiles(dataDirectory, "tax-*.json")
                .Select(Load)
                .ToFrozenDictionary(file => file.TaxYear, TaxYearTable.Build)
            : FrozenDictionary<int, TaxYearTable>.Empty;
    }

    public bool HasYear(int taxYear) => years.ContainsKey(taxYear);

    public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) {
        if (!years.TryGetValue(taxYear, out TaxYearTable? year)) {
            throw new KeyNotFoundException($"Tax data for {taxYear} is not bundled.");
        }

        if (!TaxSettings.IsValidTable(tableNumber, column)) {
            throw new ArgumentOutOfRangeException(nameof(tableNumber), "Table number or column is outside the published range.");
        }

        if (grossPay <= 0m) {
            return 0m;
        }

        int wholeKrona = decimal.ToInt32(decimal.Floor(grossPay));
        TaxTableRange range = year.Find(tableNumber, wholeKrona)
            ?? throw new ArgumentOutOfRangeException(nameof(grossPay), "Gross pay is outside the official table ranges.");
        decimal value = range.Columns[column - 1];
        return range.AmountKind == '%'
            ? decimal.Truncate(wholeKrona * value / 100m)
            : value;
    }

    private static TaxTableFile Load(string path) {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<TaxTableFile>(stream)
            ?? throw new InvalidDataException($"Tax data file is empty: {path}");
    }

    /// <summary>
    /// The ranges of one year, grouped by table and sorted, so a lookup binary-searches one table
    /// instead of scanning every bracket of every table.
    /// </summary>
    private sealed class TaxYearTable {
        private readonly FrozenDictionary<int, TaxTableRange[]> rangesByTable;

        private TaxYearTable(FrozenDictionary<int, TaxTableRange[]> rangesByTable) => this.rangesByTable = rangesByTable;

        public static TaxYearTable Build(TaxTableFile file) => new(
            file.Ranges
                .GroupBy(range => range.TableNumber)
                .ToFrozenDictionary(
                    group => group.Key,
                    group => group.OrderBy(range => range.LowerBound).ToArray()));

        public TaxTableRange? Find(int tableNumber, int wholeKrona) {
            if (!rangesByTable.TryGetValue(tableNumber, out TaxTableRange[]? ranges)) {
                return null;
            }

            int low = 0;
            int high = ranges.Length - 1;
            while (low <= high) {
                int middle = low + ((high - low) / 2);
                TaxTableRange range = ranges[middle];
                if (wholeKrona < range.LowerBound) {
                    high = middle - 1;
                }
                else if (wholeKrona > range.UpperBound) {
                    low = middle + 1;
                }
                else {
                    return range;
                }
            }

            return null;
        }
    }
}
