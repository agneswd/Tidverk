using ClosedXML.Excel;
using Tidverk.Core;
using Tidverk.Infrastructure.Export;

namespace Tidverk.Infrastructure.Tests;

public sealed class ExcelExportTests {
    [Fact]
    public async Task Workbook_uses_actual_month_days_guarded_formulas_and_reopens() {
        string path = Path.Combine(Path.GetTempPath(), $"tidverk-{Guid.NewGuid():N}.xlsx");
        WorkEntry overtimeEntry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(18, 30), 30, "Rungard");
        WorkEntry shortEntry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 2), new TimeOnly(8, 0), new TimeOnly(16, 0), 30, "Rungard");
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, 60, 16 * 60),
            [overtimeEntry, shortEntry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(202m),
            new DateOnly(2026, 7, 3));
        ReportExportRequest request = new(2026, 7, "Elias Andreasson", "Employer", [overtimeEntry, shortEntry], summary);

        try {
            await ExcelReportExporter.ExportAsync(request, path, TestContext.Current.CancellationToken);
            using XLWorkbook workbook = new(path);
            IXLWorksheet sheet = workbook.Worksheet(1);
            Assert.Equal(31, sheet.Cell(35, 1).GetValue<int>());
            Assert.Contains("IF(OR", sheet.Cell(5, 5).FormulaA1, StringComparison.Ordinal);
            Assert.Equal("Totalt ordinarie timmar", sheet.Cell(37, 4).GetString());
            Assert.Equal(15.5, sheet.Cell(37, 5).GetDouble());
            Assert.True(sheet.Cell(38, 4).IsEmpty());
            Assert.True(sheet.Cell(39, 4).IsEmpty());

            IXLWorksheet balance = workbook.Worksheet("Tidsbalans");
            Assert.Equal("Ordinarie timmar (max 8 h/dag)", balance.Cell("A4").GetString());
            Assert.Equal(15.5, balance.Cell("B4").GetDouble());
            Assert.Equal(2, balance.Cell("B5").GetDouble());
            Assert.Equal(17.5, balance.Cell("B6").GetDouble());
            Assert.Equal(1.5, balance.Cell("B9").GetDouble());
            Assert.Equal(1, balance.Cell("B10").GetDouble());
            Assert.Equal(2.5, balance.Cell("B11").GetDouble());
        }
        finally {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Elias Andreasson", "Tidverk_Elias_Andreasson_2026-07.xlsx")]
    [InlineData(" A/B ", "Tidverk_A_B_2026-07.xlsx")]
    public void Filename_is_sanitized(string employeeName, string expected) {
        Assert.Equal(expected, ExportFilename.Create(employeeName, 2026, 7));
    }
}
