using System.Text.Json;
using Tidverk.Core;
using Tidverk.Infrastructure.Tax;

namespace Tidverk.Infrastructure.Tests;

/// <summary>
/// The provider indexes and binary-searches the bundled brackets. These tests check that against an
/// exhaustive scan of the same file, so the index can never quietly disagree with the source data.
/// </summary>
public sealed class TaxTableLookupTests {
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Tax", "Data");

    [Fact]
    public void Every_bracket_boundary_matches_an_exhaustive_scan() {
        JsonTaxTableProvider provider = new(DataDirectory);
        TaxTableFile file = LoadBundledYear();

        foreach (TaxTableRange range in file.Ranges) {
            foreach (int income in Boundaries(range)) {
                decimal expected = Expected(file, range.TableNumber, column: 1, income);
                Assert.Equal(expected, provider.GetPreliminaryTax(file.TaxYear, range.TableNumber, 1, income));
            }
        }
    }

    [Fact]
    public void Bundled_brackets_do_not_overlap_within_a_table() {
        TaxTableFile file = LoadBundledYear();

        foreach (IGrouping<int, TaxTableRange> table in file.Ranges.GroupBy(range => range.TableNumber)) {
            TaxTableRange[] ordered = table.OrderBy(range => range.LowerBound).ToArray();
            for (int index = 1; index < ordered.Length; index++) {
                Assert.True(
                    ordered[index].LowerBound > ordered[index - 1].UpperBound,
                    $"Table {table.Key} brackets overlap at {ordered[index].LowerBound}.");
            }
        }
    }

    /// <summary>A gap would make one income throw while its neighbours resolve.</summary>
    [Fact]
    public void Bundled_brackets_cover_every_income_from_one_krona_upwards() {
        TaxTableFile file = LoadBundledYear();

        foreach (IGrouping<int, TaxTableRange> table in file.Ranges.GroupBy(range => range.TableNumber)) {
            TaxTableRange[] ordered = table.OrderBy(range => range.LowerBound).ToArray();
            Assert.Equal(1, ordered[0].LowerBound);
            Assert.Equal(int.MaxValue, ordered[^1].UpperBound);
            for (int index = 1; index < ordered.Length; index++) {
                Assert.Equal(ordered[index - 1].UpperBound + 1, ordered[index].LowerBound);
            }
        }
    }

    [Theory]
    [InlineData(28, 1)]
    [InlineData(43, 1)]
    [InlineData(33, 0)]
    [InlineData(33, 7)]
    public void Tables_and_columns_outside_the_published_range_are_rejected(int tableNumber, int column) {
        JsonTaxTableProvider provider = new(DataDirectory);

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetPreliminaryTax(2026, tableNumber, column, 30_704m));
    }

    [Fact]
    public void An_unbundled_year_reports_a_missing_key_rather_than_a_wrong_number() {
        JsonTaxTableProvider provider = new(DataDirectory);

        Assert.Throws<KeyNotFoundException>(() => provider.GetPreliminaryTax(1999, 33, 1, 30_704m));
    }

    private static IEnumerable<int> Boundaries(TaxTableRange range) {
        yield return range.LowerBound;
        if (range.UpperBound != int.MaxValue) {
            yield return range.UpperBound;
        }
    }

    private static decimal Expected(TaxTableFile file, int tableNumber, int column, int income) {
        TaxTableRange range = file.Ranges.First(item =>
            item.TableNumber == tableNumber && income >= item.LowerBound && income <= item.UpperBound);
        decimal value = range.Columns[column - 1];
        return range.AmountKind == '%' ? decimal.Truncate(income * value / 100m) : value;
    }

    private static TaxTableFile LoadBundledYear() {
        string path = Directory.GetFiles(DataDirectory, "tax-*.json").Single();
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<TaxTableFile>(stream)!;
    }
}
