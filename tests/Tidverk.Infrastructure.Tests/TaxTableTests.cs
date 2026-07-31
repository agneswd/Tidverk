using Tidverk.Core;
using Tidverk.Infrastructure.Tax;

namespace Tidverk.Infrastructure.Tests;

public sealed class TaxTableTests {
    [Fact]
    public void Bundled_2026_table_matches_official_known_value() {
        JsonTaxTableProvider provider = new(Path.Combine(AppContext.BaseDirectory, "Tax", "Data"));

        decimal deduction = provider.GetPreliminaryTax(2026, 33, 1, 30_704m);
        TaxEstimate estimate = new TaxCalculator(provider).Calculate(30_704m, new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1));

        Assert.Equal(6_079m, deduction);
        Assert.Equal(24_625m, estimate.EstimatedNetPay);
        Assert.False(provider.HasYear(2025));
    }

    [Fact]
    public void Zero_gross_pay_has_zero_preliminary_tax() {
        JsonTaxTableProvider provider = new(Path.Combine(AppContext.BaseDirectory, "Tax", "Data"));

        decimal deduction = provider.GetPreliminaryTax(2026, 33, 1, 0m);

        Assert.Equal(0m, deduction);
    }

    [Fact]
    public void Importer_rejects_incomplete_or_malformed_data() {
        Assert.Throws<InvalidDataException>(() => SkatteverketTaxTableImporter.Parse(
            ["30B33      1   2000    0    0    0    0    0    0"],
            2026,
            "source.txt",
            "Source",
            "ABC",
            DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Missing_year_returns_unavailable_instead_of_guessing() {
        JsonTaxTableProvider provider = new(Path.Combine(AppContext.BaseDirectory, "Tax", "Data"));

        TaxEstimate estimate = new TaxCalculator(provider).Calculate(30_704m, new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2025, 33, 1));

        Assert.False(estimate.IsAvailable);
        Assert.Equal("Tax estimate unavailable for this year.", estimate.UnavailableReason);
    }
}
