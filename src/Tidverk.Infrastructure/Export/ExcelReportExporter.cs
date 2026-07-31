using System.Globalization;
using ClosedXML.Excel;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Export;

public sealed record ReportExportRequest(
    int Year,
    int Month,
    string EmployeeName,
    string EmployerName,
    IReadOnlyList<WorkEntry> Entries,
    MonthlySummary Summary,
    ExportLanguagePreference Language = ExportLanguagePreference.Swedish,
    OvertimeCompensationMode OvertimeMode = OvertimeCompensationMode.CompTime,
    decimal DailyOvertimeThresholdHours = 8m);

public sealed record ExportValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) {
    public bool CanExport => Errors.Count == 0;
}

public static class ReportExportValidator {
    public static ExportValidationResult Validate(ReportExportRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        List<string> errors = [];
        foreach (WorkEntry entry in request.Entries) {
            if (entry.Date.Year != request.Year || entry.Date.Month != request.Month) {
                errors.Add($"{entry.Date:yyyy-MM-dd} is outside the selected month.");
            }

            errors.AddRange(entry.Validate().Select(error => $"{entry.Date:yyyy-MM-dd}: {error}"));
        }

        List<string> warnings = request.Summary.MissingPastDayCount == 0
            ? []
            : [$"{request.Summary.MissingPastDayCount} previous workday(s) still need an entry."];
        return new(errors, warnings);
    }
}

public static class ExportFilename {
    public static string Create(string employeeName, int year, int month) {
        string safeName = string.Concat(employeeName.Trim().Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) || char.IsWhiteSpace(character) ? '_' : character));
        safeName = string.Join('_', safeName.Split('_', StringSplitOptions.RemoveEmptyEntries));
        return $"Tidverk_{safeName}_{year:D4}-{month:D2}.xlsx";
    }
}

public static class ExcelReportExporter {
    public static async Task ExportAsync(ReportExportRequest request, string path, CancellationToken cancellationToken = default) {
        ExportValidationResult validation = ReportExportValidator.Validate(request);
        if (!validation.CanExport) {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        await Task.Run(() => CreateWorkbook(request).SaveAs(path, new SaveOptions {
            EvaluateFormulasBeforeSaving = true,
            ValidatePackage = true
        }), cancellationToken);
    }

    public static XLWorkbook CreateWorkbook(ReportExportRequest request) {
        XLWorkbook workbook = new();
        CultureInfo culture = request.Language == ExportLanguagePreference.English
            ? CultureInfo.GetCultureInfo("en")
            : CultureInfo.GetCultureInfo("sv-SE");
        IXLWorksheet sheet = workbook.Worksheets.Add(new DateTime(request.Year, request.Month, 1).ToString("MMMM yyyy", culture));
        sheet.Cell("A1").Value = Text(request.Language, "Tidverk - Time report", "Tidverk - Tidrapport");
        sheet.Cell("A2").Value = request.EmployeeName;
        sheet.Cell("D2").Value = request.EmployerName;
        sheet.Cell("A4").Value = Text(request.Language, "Day", "Dag");
        sheet.Cell("B4").Value = "Start";
        sheet.Cell("C4").Value = Text(request.Language, "Stop", "Slut");
        sheet.Cell("D4").Value = "Lunch";
        sheet.Cell("E4").Value = Text(request.Language, "Hours", "Timmar");
        sheet.Cell("F4").Value = Text(request.Language, "Overtime", "Övertid");
        sheet.Cell("G4").Value = "Status";
        sheet.Cell("H4").Value = Text(request.Language, "Project", "Projekt");

        IReadOnlyDictionary<DateOnly, WorkEntry> entries = request.Entries.ToDictionary(entry => entry.Date);
        int dayCount = DateTime.DaysInMonth(request.Year, request.Month);
        for (int day = 1; day <= dayCount; day++) {
            int row = 4 + day;
            DateOnly date = new(request.Year, request.Month, day);
            sheet.Cell(row, 1).Value = day;
            if (!entries.TryGetValue(date, out WorkEntry? entry)) {
                continue;
            }

            if (entry.Status == WorkEntryStatus.Worked) {
                sheet.Cell(row, 2).Value = entry.StartTime!.Value.ToTimeSpan();
                sheet.Cell(row, 3).Value = entry.EndTime!.Value.ToTimeSpan();
                sheet.Cell(row, 4).Value = entry.LunchMinutes.ToTimeSpan();
                string threshold = request.DailyOvertimeThresholdHours.ToString(CultureInfo.InvariantCulture);
                sheet.Cell(row, 5).FormulaA1 = $"=IF(OR(B{row}=\"\",C{row}=\"\"),\"\",MIN({threshold},MAX(0,(C{row}-B{row}-D{row})*24)))";
                sheet.Cell(row, 6).FormulaA1 = $"=IF(OR(B{row}=\"\",C{row}=\"\"),\"\",MAX(0,(C{row}-B{row}-D{row})*24-{threshold}))";
                sheet.Cell(row, 8).Value = entry.ProjectName ?? string.Empty;
            }
            else if (entry.Status == WorkEntryStatus.Off) {
                sheet.Cell(row, 7).Value = Text(request.Language, "Day off", "Ledig");
            }
        }

        int summaryRow = AddReportSummary(sheet, dayCount, request.Language);

        AddBalanceSheet(workbook, request, sheet.Name, dayCount, summaryRow);
        Style(sheet, dayCount, summaryRow, summaryRow);
        return workbook;
    }

    private static int AddReportSummary(IXLWorksheet sheet, int dayCount, ExportLanguagePreference language) {
        int regularRow = dayCount + 6;
        string workedRange = $"E5:E{4 + dayCount}";
        sheet.Cell(regularRow, 4).Value = Text(language, "Total regular hours", "Totalt ordinarie timmar");
        sheet.Cell(regularRow, 5).FormulaA1 = $"=SUM({workedRange})";
        return regularRow;
    }

    private static void AddBalanceSheet(
        XLWorkbook workbook,
        ReportExportRequest request,
        string reportSheetName,
        int dayCount,
        int regularRow) {
        IXLWorksheet balance = workbook.Worksheets.Add(Text(request.Language, "Time balance", "Tidsbalans"));
        string escapedReportSheetName = reportSheetName.Replace("'", "''", StringComparison.Ordinal);
        string regularRange = $"'{escapedReportSheetName}'!E5:E{4 + dayCount}";
        string overtimeRange = $"'{escapedReportSheetName}'!F5:F{4 + dayCount}";
        CultureInfo culture = request.Language == ExportLanguagePreference.English
            ? CultureInfo.GetCultureInfo("en")
            : CultureInfo.GetCultureInfo("sv-SE");
        balance.Cell("A1").Value = Text(request.Language, "Time balance - personal tracking", "Tidsbalans - personlig uppföljning");
        balance.Cell("A2").Value = Text(request.Language, "Month", "Månad");
        balance.Cell("B2").Value = new DateTime(request.Year, request.Month, 1).ToString("MMMM yyyy", culture);
        balance.Cell("A4").Value = Text(request.Language, "Regular hours", "Ordinarie timmar");
        balance.Cell("B4").FormulaA1 = $"='{escapedReportSheetName}'!E{regularRow}";
        balance.Cell("A5").Value = Text(request.Language, "Overtime", "Övertid");
        balance.Cell("B5").FormulaA1 = $"=SUM({overtimeRange})";
        balance.Cell("A6").Value = Text(request.Language, "Worked hours", "Arbetade timmar");
        balance.Cell("B6").FormulaA1 = $"=SUM({regularRange})+SUM({overtimeRange})";
        balance.Cell("A8").Value = Text(request.Language, "Expected hours", "Förväntade timmar");
        balance.Cell("B8").Value = request.Summary.ExpectedHours;
        balance.Cell("A9").Value = Text(request.Language, "Monthly time balance", "Månadens tidsbalans");
        balance.Cell("B9").FormulaA1 = request.OvertimeMode == OvertimeCompensationMode.CompTime
            ? "=B6-B8"
            : "=B4-B8";
        balance.Cell("A10").Value = Text(request.Language, "Opening time balance", "Ingående tidsbalans");
        balance.Cell("B10").Value = request.Summary.OpeningBalanceMinutes / 60m;
        balance.Cell("A11").Value = Text(request.Language, "Closing time balance", "Utgående tidsbalans");
        balance.Cell("B11").FormulaA1 = "=B9+B10";

        balance.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        balance.Range("A4:A11").Style.Font.SetBold();
        balance.Range("B4:B11").Style.NumberFormat.Format = "0.00";
        balance.Columns("A:B").AdjustToContents();
        balance.Column("A").Width = Math.Max(balance.Column("A").Width, 34);
        balance.Column("B").Width = Math.Max(balance.Column("B").Width, 18);
    }

    private static string Text(ExportLanguagePreference language, string english, string swedish) =>
        language == ExportLanguagePreference.English ? english : swedish;

    private static void Style(IXLWorksheet sheet, int dayCount, int firstSummaryRow, int lastSummaryRow) {
        sheet.Range("A1:H1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Range("A4:H4").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E3ECE7"));
        sheet.Range(4, 1, 4 + dayCount, 8).Style.Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(XLColor.FromHtml("#D9DAD3"));
        sheet.Range(5, 2, 4 + dayCount, 4).Style.NumberFormat.Format = "hh:mm";
        sheet.Range(5, 5, lastSummaryRow, 6).Style.NumberFormat.Format = "0.00";
        sheet.Range(firstSummaryRow, 4, lastSummaryRow, 5).Style.Font.SetBold();
        sheet.SheetView.FreezeRows(4);
        sheet.Column(1).Width = 8;
        sheet.Columns(2, 4).Width = 12;
        sheet.Column(4).Width = 25;
        sheet.Column(5).Width = 18;
        sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 14;
        sheet.Column(8).Width = 24;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
    }
}
