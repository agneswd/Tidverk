using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Tax;

/// <summary>
/// Reads Skatteverket's fixed-width "allmänna tabeller" text file and writes the deterministic JSON
/// the application ships. Only the monthly period is imported; other periods are skipped.
/// </summary>
public static class SkatteverketTaxTableImporter {
    private const int MonthlyPeriodCode = 30;
    private const int ColumnCount = 6;
    private const int ColumnWidth = 5;
    private const int FirstColumnOffset = 19;
    private const int MinimumRecordLength = FirstColumnOffset + (ColumnCount * ColumnWidth);

    public static async Task ImportFileAsync(
        string inputPath,
        string outputPath,
        int taxYear,
        string sourceTitle,
        CancellationToken cancellationToken = default) {
        byte[] source = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        string checksum = Convert.ToHexStringLower(SHA256.HashData(source));
        string[] lines = Encoding.UTF8.GetString(source).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        TaxTableFile file = Parse(lines, taxYear, Path.GetFileName(inputPath), sourceTitle, checksum, DateTimeOffset.UtcNow);

        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory)) {
            Directory.CreateDirectory(outputDirectory);
        }

        await using FileStream output = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(output, file, cancellationToken: cancellationToken);
    }

    public static TaxTableFile Parse(
        IEnumerable<string> lines,
        int taxYear,
        string sourceFileName,
        string sourceTitle,
        string checksum,
        DateTimeOffset importedAt) {
        ArgumentNullException.ThrowIfNull(lines);
        List<TaxTableRange> ranges = [];
        foreach (string rawLine in lines) {
            if (TryParseMonthlyRange(rawLine, out TaxTableRange? range)) {
                ranges.Add(range);
            }
        }

        int[] missingTables = Enumerable
            .Range(TaxSettings.MinimumTableNumber, TaxSettings.MaximumTableNumber - TaxSettings.MinimumTableNumber + 1)
            .Except(ranges.Select(range => range.TableNumber))
            .ToArray();
        if (missingTables.Length > 0) {
            throw new InvalidDataException(
                $"The source is missing monthly tables {string.Join(", ", missingTables)}; tables " +
                $"{TaxSettings.MinimumTableNumber}-{TaxSettings.MaximumTableNumber} are all required.");
        }

        return new(taxYear, sourceFileName, sourceTitle, importedAt, checksum, ranges);
    }

    private static bool TryParseMonthlyRange(string rawLine, out TaxTableRange range) {
        range = null!;
        string line = rawLine.TrimStart('\uFEFF').TrimEnd();
        if (line.Length < MinimumRecordLength ||
            !int.TryParse(line.AsSpan(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int period) ||
            period != MonthlyPeriodCode) {
            return false;
        }

        char amountKind = line[2];
        int table = ParseInt(line, 3, 2);
        int lowerBound = ParseInt(line, 5, 7);
        int upperBound = ParseInt(line, 12, 7, blankValue: int.MaxValue);
        decimal[] columns = new decimal[ColumnCount];
        for (int index = 0; index < ColumnCount; index++) {
            columns[index] = ParseInt(line, FirstColumnOffset + (index * ColumnWidth), ColumnWidth);
        }

        if (table < TaxSettings.MinimumTableNumber ||
            table > TaxSettings.MaximumTableNumber ||
            lowerBound < 0 ||
            upperBound < lowerBound ||
            amountKind is not ('B' or '%')) {
            throw new InvalidDataException($"Malformed tax-table range: {line}");
        }

        range = new(table, lowerBound, upperBound, amountKind, columns);
        return true;
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
}
