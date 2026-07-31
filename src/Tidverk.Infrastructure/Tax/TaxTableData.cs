using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Tax;

public sealed record TaxTableFile(
    int TaxYear,
    string SourceFileName,
    string SourceTitle,
    DateTimeOffset ImportedAt,
    string Sha256,
    IReadOnlyList<TaxTableRange> Ranges);

public sealed record TaxTableRange(
    int TableNumber,
    int LowerBound,
    int UpperBound,
    char AmountKind,
    IReadOnlyList<decimal> Columns);

public sealed class JsonTaxTableProvider : IPrimaryIncomeTaxTable {
    private readonly IReadOnlyDictionary<int, TaxTableFile> files;

    public JsonTaxTableProvider(string dataDirectory) {
        if (!Directory.Exists(dataDirectory)) {
            files = new Dictionary<int, TaxTableFile>();
            return;
        }

        files = Directory.GetFiles(dataDirectory, "tax-*.json")
            .Select(Load)
            .ToDictionary(file => file.TaxYear);
    }

    public bool HasYear(int taxYear) => files.ContainsKey(taxYear);

    public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) {
        if (!files.TryGetValue(taxYear, out TaxTableFile? file)) {
            throw new KeyNotFoundException($"Tax data for {taxYear} is not bundled.");
        }

        if (tableNumber is < 29 or > 42 || column is < 1 or > 6) {
            throw new ArgumentOutOfRangeException(nameof(tableNumber));
        }

        if (grossPay == 0) {
            return 0;
        }

        int wholeKrona = decimal.ToInt32(decimal.Floor(grossPay));
        TaxTableRange range = file.Ranges.FirstOrDefault(item =>
            item.TableNumber == tableNumber && wholeKrona >= item.LowerBound && wholeKrona <= item.UpperBound)
            ?? throw new ArgumentOutOfRangeException(nameof(grossPay), "Gross pay is outside the official table ranges.");
        decimal value = range.Columns[column - 1];
        return range.AmountKind == '%'
            ? decimal.Truncate(wholeKrona * value / 100m)
            : value;
    }

    public TaxTableFile GetMetadata(int taxYear) => files[taxYear];

    private static TaxTableFile Load(string path) {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<TaxTableFile>(stream)
            ?? throw new InvalidDataException($"Tax data file is empty: {path}");
    }
}

public static class SkatteverketTaxTableImporter {
    private const int MonthlyPeriodCode = 30;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static async Task ImportFileAsync(
        string inputPath,
        string outputPath,
        int taxYear,
        string sourceTitle,
        CancellationToken cancellationToken = default) {
        byte[] source = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        string checksum = Convert.ToHexStringLower(SHA256.HashData(source));
        string[] lines = System.Text.Encoding.UTF8.GetString(source).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        TaxTableFile file = Parse(lines, taxYear, Path.GetFileName(inputPath), sourceTitle, checksum, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await using FileStream output = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(output, file, JsonOptions, cancellationToken);
    }

    public static TaxTableFile Parse(
        IEnumerable<string> lines,
        int taxYear,
        string sourceFileName,
        string sourceTitle,
        string checksum,
        DateTimeOffset importedAt) {
        List<TaxTableRange> ranges = [];
        foreach (string rawLine in lines) {
            string line = rawLine.TrimStart('\uFEFF').TrimEnd();
            if (line.Length < 49 ||
                !int.TryParse(line.AsSpan(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int period) ||
                period != MonthlyPeriodCode) {
                continue;
            }

            char amountKind = line[2];
            int table = ParseInt(line, 3, 2);
            int lower = ParseInt(line, 5, 7);
            int upper = ParseInt(line, 12, 7, int.MaxValue);
            decimal[] columns = Enumerable.Range(0, 6).Select(index => (decimal)ParseInt(line, 19 + index * 5, 5)).ToArray();
            Validate(table, lower, upper, amountKind, columns);
            ranges.Add(new(table, lower, upper, amountKind, columns));
        }

        if (ranges.Count == 0 || Enumerable.Range(29, 14).Any(table => !ranges.Any(range => range.TableNumber == table))) {
            throw new InvalidDataException("The source does not contain complete monthly tables 29-42.");
        }

        return new(taxYear, sourceFileName, sourceTitle, importedAt, checksum, ranges);
    }

    private static int ParseInt(string line, int start, int length, int? blankValue = null) {
        ReadOnlySpan<char> field = line.AsSpan(start, length);
        if (blankValue is int fallback && field.IsWhiteSpace()) {
            return fallback;
        }

        if (!int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) {
            throw new InvalidDataException($"Malformed numeric field in tax-table record: {line}");
        }

        return value;
    }

    private static void Validate(int table, int lower, int upper, char amountKind, IReadOnlyCollection<decimal> columns) {
        if (table is < 29 or > 42 || lower < 0 || upper < lower || amountKind is not ('B' or '%') || columns.Count != 6) {
            throw new InvalidDataException("Malformed tax-table range.");
        }
    }
}
