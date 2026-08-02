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
            Assert.Equal(10, sheet.Cell(5, 5).GetDouble(), precision: 10);
            Assert.Equal("Timmar", sheet.Cell("E4").GetString());
            Assert.Equal("Status", sheet.Cell("F4").GetString());
            Assert.Equal("Projekt", sheet.Cell("G4").GetString());
            Assert.True(sheet.Column(8).IsHidden);
            Assert.Equal("Totalt ordinarie timmar", sheet.Cell(37, 4).GetString());
            Assert.Equal(15.5, sheet.Cell(37, 5).GetDouble());
            Assert.Equal("Total övertid", sheet.Cell(38, 4).GetString());
            Assert.Equal(2, sheet.Cell(38, 5).GetDouble());
            Assert.Equal("Totala OB-timmar", sheet.Cell(39, 4).GetString());
            Assert.Equal(0, sheet.Cell(39, 5).GetDouble());

            IXLWorksheet balance = workbook.Worksheet("Tidsbalans");
            Assert.Equal("Ordinarie timmar", balance.Cell("A4").GetString());
            Assert.Equal(15.5, balance.Cell("B4").GetDouble());
            Assert.Equal(2, balance.Cell("B5").GetDouble());
            Assert.Equal(17.5, balance.Cell("B6").GetDouble());
            Assert.Equal(0, balance.Cell("B7").GetDouble());
            Assert.Equal(1.5, balance.Cell("B9").GetDouble());
            Assert.Equal(1, balance.Cell("B10").GetDouble());
            Assert.Equal(2.5, balance.Cell("B11").GetDouble());
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Overnight_shift_reports_its_real_length_instead_of_zero() {
        // 22:00-06:00 with a 30-minute break is 7.5 hours. Subtracting the stop time from the start
        // gives a negative interval, which used to be clamped to zero in the employer report.
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(22, 0), new TimeOnly(6, 0), 30, "Rungard");
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(202m),
            new DateOnly(2026, 7, 1));
        ReportExportRequest request = new(2026, 7, "Elias", "Employer", [entry], summary);

        using XLWorkbook workbook = ExcelReportExporter.CreateWorkbook(request);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal(7.5, sheet.Cell(5, 5).GetDouble(), precision: 10);
        Assert.Equal(7.5m, entry.WorkedMinutes.Hours);
    }

    [Fact]
    public void Workbook_uses_selected_export_language() {
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Rungard");
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(202m),
            new DateOnly(2026, 7, 1));
        ReportExportRequest request = new(2026, 7, "Elias", "Employer", [entry], summary, ExportLanguagePreference.English);

        using XLWorkbook workbook = ExcelReportExporter.CreateWorkbook(request);

        Assert.Equal("July 2026", workbook.Worksheet(1).Name);
        Assert.Equal("Hours", workbook.Worksheet(1).Cell("E4").GetString());
        Assert.Equal("Status", workbook.Worksheet(1).Cell("F4").GetString());
        Assert.Equal("Total overtime", workbook.Worksheet(1).Cell("D38").GetString());
        Assert.Equal("Regular hours", workbook.Worksheet("Time balance").Cell("A4").GetString());
    }

    [Fact]
    public void Workbook_uses_operating_system_language_when_selected() {
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Rungard");
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(202m),
            new DateOnly(2026, 7, 1));
        ReportExportRequest request = new(2026, 7, "Elias", "Employer", [entry], summary, ExportLanguagePreference.System);

        using XLWorkbook workbook = ExcelReportExporter.CreateWorkbook(request);

        string expected = string.Equals(System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "sv", StringComparison.Ordinal)
            ? "Timmar"
            : "Hours";
        Assert.Equal(expected, workbook.Worksheet(1).Cell("E4").GetString());
    }

    [Fact]
    public void Paid_overtime_is_visible_but_excluded_from_time_balance() {
        WorkEntry entry = WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(18, 30), 30, "Rungard");
        OvertimeCompensationSettings paidOvertime = new(OvertimeCompensationMode.Paid, 50m, dailyThresholdHours: 7.5m);
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 8 * 60),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(200m),
            new DateOnly(2026, 7, 2),
            overtimeCompensation: paidOvertime);
        ReportExportRequest request = new(
            2026, 7, "Elias", "Employer", [entry], summary,
            ExportLanguagePreference.English,
            OvertimeCompensationMode.Paid,
            7.5m);

        using XLWorkbook workbook = ExcelReportExporter.CreateWorkbook(request);

        Assert.Equal(10, workbook.Worksheet(1).Cell("E5").GetDouble(), precision: 10);
        Assert.Equal(2.5, workbook.Worksheet(1).Cell("E38").GetDouble(), precision: 10);
        Assert.Equal(-0.5, workbook.Worksheet("Time balance").Cell("B9").GetDouble(), precision: 10);
    }

    [Fact]
    public void Schedule_based_export_treats_a_zero_hour_day_as_all_overtime() {
        WorkEntry entry = WorkEntry.CreateWorked(
            new DateOnly(2026, 7, 4),
            new TimeOnly(8, 0),
            new TimeOnly(11, 0),
            0,
            scheduledMinutesOverride: 0);
        OvertimeCompensationSettings overtime = new(
            OvertimeCompensationMode.Paid,
            thresholdMode: OvertimeThresholdMode.ScheduledHours);
        MonthlySummary summary = MonthlyCalculator.Calculate(
            new MonthRecord(2026, 7, expectedMinutesOverride: 0),
            [entry],
            ExpectedHoursSettings.Standard,
            new HourlySalary(100m),
            new DateOnly(2026, 7, 5),
            overtimeCompensation: overtime);
        ReportExportRequest request = new(
            2026,
            7,
            "Elias",
            "Employer",
            [entry],
            summary,
            ExportLanguagePreference.English,
            OvertimeCompensationMode.Paid,
            ExpectedHours: ExpectedHoursSettings.Standard,
            OvertimeSettings: overtime);

        using XLWorkbook workbook = ExcelReportExporter.CreateWorkbook(request);

        Assert.Equal(0, workbook.Worksheet(1).Cell("E37").GetDouble());
        Assert.Equal(3, workbook.Worksheet(1).Cell("E38").GetDouble());
        Assert.Equal(0, workbook.Worksheet("Time balance").Cell("B9").GetDouble());
    }

    [Theory]
    [InlineData("Elias Andreasson", "Tidverk_Elias_Andreasson_2026-07.xlsx")]
    [InlineData(" A/B ", "Tidverk_A_B_2026-07.xlsx")]
    public void Filename_is_sanitized(string employeeName, string expected) {
        Assert.Equal(expected, ExportFilename.Create(employeeName, 2026, 7));
    }
}
