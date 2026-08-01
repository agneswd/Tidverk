using System.Globalization;
using ClosedXML.Excel;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Export;

/// <summary>
/// Writes the monthly time report. Hours are left as live formulas over the real start, stop and
/// lunch values so the recipient can see and check the arithmetic in Excel.
/// </summary>
public static class ExcelReportExporter {
    private const int HeaderRow = 4;
    private const int FirstDayRow = HeaderRow + 1;
    private const int DayColumn = 1;
    private const int StartColumn = 2;
    private const int StopColumn = 3;
    private const int LunchColumn = 4;
    private const int HoursColumn = 5;
    private const int StatusColumn = 6;
    private const int ProjectColumn = 7;
    private const int OvertimeCalculationColumn = 8;

    public static async Task ExportAsync(ReportExportRequest request, string path, CancellationToken cancellationToken = default) {
        ExportValidationResult validation = ReportExportValidator.Validate(request);
        if (!validation.CanExport) {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        await Task.Run(
            () => {
                using XLWorkbook workbook = CreateWorkbook(request);
                workbook.SaveAs(path, new SaveOptions {
                    EvaluateFormulasBeforeSaving = true,
                    ValidatePackage = true
                });
            },
            cancellationToken);
    }

    /// <summary>The caller owns the returned workbook.</summary>
    public static XLWorkbook CreateWorkbook(ReportExportRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        XLWorkbook workbook = new();
        try {
            CultureInfo culture = CultureFor(request.Language);
            IXLWorksheet sheet = workbook.Worksheets.Add(MonthTitle(request, culture));
            WriteHeader(sheet, request);
            int dayCount = DateTime.DaysInMonth(request.Year, request.Month);
            WriteDays(sheet, request, dayCount);
            int totalsRow = WriteTotals(sheet, dayCount, request.Language);
            AddBalanceSheet(workbook, request, sheet.Name, totalsRow, culture);
            Style(sheet, dayCount, totalsRow);
            return workbook;
        }
        catch {
            workbook.Dispose();
            throw;
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, ReportExportRequest request) {
        sheet.Cell("A1").Value = Text(request.Language, "Tidverk - Time report", "Tidverk - Tidrapport");
        sheet.Cell("A2").Value = request.EmployeeName;
        sheet.Cell("D2").Value = request.EmployerName;
        sheet.Cell(HeaderRow, DayColumn).Value = Text(request.Language, "Day", "Dag");
        sheet.Cell(HeaderRow, StartColumn).Value = "Start";
        sheet.Cell(HeaderRow, StopColumn).Value = Text(request.Language, "Stop", "Slut");
        sheet.Cell(HeaderRow, LunchColumn).Value = "Lunch";
        sheet.Cell(HeaderRow, HoursColumn).Value = Text(request.Language, "Hours", "Timmar");
        sheet.Cell(HeaderRow, StatusColumn).Value = "Status";
        sheet.Cell(HeaderRow, ProjectColumn).Value = Text(request.Language, "Project", "Projekt");
    }

    /// <summary>Every calendar day gets a row, whether or not it has an entry.</summary>
    private static void WriteDays(IXLWorksheet sheet, ReportExportRequest request, int dayCount) {
        Dictionary<DateOnly, WorkEntry> entries = request.Entries.ToDictionary(entry => entry.Date);
        string threshold = request.DailyOvertimeThresholdHours.ToString(CultureInfo.InvariantCulture);
        for (int day = 1; day <= dayCount; day++) {
            int row = HeaderRow + day;
            sheet.Cell(row, DayColumn).Value = day;
            if (!entries.TryGetValue(new DateOnly(request.Year, request.Month, day), out WorkEntry? entry)) {
                continue;
            }

            switch (entry.Status) {
                case WorkEntryStatus.Worked:
                    sheet.Cell(row, StartColumn).Value = entry.StartTime!.Value.ToTimeSpan();
                    sheet.Cell(row, StopColumn).Value = entry.EndTime!.Value.ToTimeSpan();
                    sheet.Cell(row, LunchColumn).Value = entry.LunchMinutes.ToTimeSpan();
                    sheet.Cell(row, HoursColumn).FormulaA1 = WorkedHoursFormula(row);
                    sheet.Cell(row, OvertimeCalculationColumn).FormulaA1 = OvertimeHoursFormula(row, threshold);
                    sheet.Cell(row, ProjectColumn).Value = entry.ProjectName ?? string.Empty;
                    break;
                case WorkEntryStatus.Off:
                    sheet.Cell(row, StatusColumn).Value = Text(request.Language, "Day off", "Ledig");
                    break;
                case WorkEntryStatus.Incomplete:
                default:
                    break;
            }
        }
    }

    /// <summary>Guarded so a row with a missing time shows blank instead of a spurious zero.</summary>
    private static string WorkedHoursFormula(int row) =>
        $"=IF(OR(B{row}=\"\",C{row}=\"\"),\"\",MAX(0,(C{row}-B{row}-D{row})*24))";

    private static string OvertimeHoursFormula(int row, string threshold) =>
        $"=IF(OR(B{row}=\"\",C{row}=\"\"),\"\",MAX(0,(C{row}-B{row}-D{row})*24-{threshold}))";

    private static int WriteTotals(IXLWorksheet sheet, int dayCount, ExportLanguagePreference language) {
        int totalsRow = dayCount + 6;
        sheet.Cell(totalsRow, LunchColumn).Value = Text(language, "Total regular hours", "Totalt ordinarie timmar");
        sheet.Cell(totalsRow, HoursColumn).FormulaA1 =
            $"=SUM({ColumnRange(HoursColumn, dayCount)})-SUM({ColumnRange(OvertimeCalculationColumn, dayCount)})";
        sheet.Cell(totalsRow + 1, LunchColumn).Value = Text(language, "Total overtime", "Total övertid");
        sheet.Cell(totalsRow + 1, HoursColumn).FormulaA1 = $"=SUM({ColumnRange(OvertimeCalculationColumn, dayCount)})";
        return totalsRow;
    }

    private static void AddBalanceSheet(
        XLWorkbook workbook,
        ReportExportRequest request,
        string reportSheetName,
        int totalsRow,
        CultureInfo culture) {
        IXLWorksheet balance = workbook.Worksheets.Add(Text(request.Language, "Time balance", "Tidsbalans"));
        string sheetReference = $"'{reportSheetName.Replace("'", "''", StringComparison.Ordinal)}'";

        balance.Cell("A1").Value = Text(request.Language, "Time balance - personal tracking", "Tidsbalans - personlig uppföljning");
        balance.Cell("A2").Value = Text(request.Language, "Month", "Månad");
        balance.Cell("B2").Value = MonthTitle(request, culture);
        balance.Cell("A4").Value = Text(request.Language, "Regular hours", "Ordinarie timmar");
        balance.Cell("B4").FormulaA1 = $"={sheetReference}!E{totalsRow}";
        balance.Cell("A5").Value = Text(request.Language, "Overtime", "Övertid");
        balance.Cell("B5").FormulaA1 = $"={sheetReference}!E{totalsRow + 1}";
        balance.Cell("A6").Value = Text(request.Language, "Worked hours", "Arbetade timmar");
        balance.Cell("B6").FormulaA1 = "=B4+B5";
        balance.Cell("A8").Value = Text(request.Language, "Expected hours", "Förväntade timmar");
        balance.Cell("B8").Value = request.Summary.ExpectedHours;
        balance.Cell("A9").Value = Text(request.Language, "Monthly time balance", "Månadens tidsbalans");

        // Paid overtime is compensated in money, so only regular hours move the balance.
        balance.Cell("B9").FormulaA1 = request.OvertimeMode == OvertimeCompensationMode.CompTime ? "=B6-B8" : "=B4-B8";
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

    private static void Style(IXLWorksheet sheet, int dayCount, int totalsRow) {
        int lastDayRow = HeaderRow + dayCount;
        sheet.Range("A1:G1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Range("A4:G4").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E3ECE7"));
        sheet.Range(HeaderRow, DayColumn, lastDayRow, ProjectColumn).Style
            .Border.SetBottomBorder(XLBorderStyleValues.Thin)
            .Border.SetBottomBorderColor(XLColor.FromHtml("#D9DAD3"));
        sheet.Range(FirstDayRow, StartColumn, lastDayRow, LunchColumn).Style.NumberFormat.Format = "hh:mm";
        sheet.Range(FirstDayRow, HoursColumn, totalsRow + 1, HoursColumn).Style.NumberFormat.Format = "0.00";
        sheet.Range(FirstDayRow, OvertimeCalculationColumn, lastDayRow, OvertimeCalculationColumn).Style.NumberFormat.Format = "0.00";
        sheet.Range(totalsRow, LunchColumn, totalsRow + 1, HoursColumn).Style.Font.SetBold();
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Column(DayColumn).Width = 8;
        sheet.Columns(StartColumn, LunchColumn).Width = 12;
        sheet.Column(LunchColumn).Width = 25;
        sheet.Column(HoursColumn).Width = 18;
        sheet.Column(StatusColumn).Width = 14;
        sheet.Column(ProjectColumn).Width = 24;
        sheet.Column(OvertimeCalculationColumn).Hide();
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
    }

    private static string ColumnRange(int column, int dayCount) {
        string letter = XLHelper.GetColumnLetterFromNumber(column);
        return $"{letter}{FirstDayRow}:{letter}{HeaderRow + dayCount}";
    }

    private static string MonthTitle(ReportExportRequest request, CultureInfo culture) =>
        new DateTime(request.Year, request.Month, 1).ToString("MMMM yyyy", culture);

    private static string Text(ExportLanguagePreference language, string english, string swedish) =>
        IsEnglish(language) ? english : swedish;

    private static CultureInfo CultureFor(ExportLanguagePreference language) =>
        CultureInfo.GetCultureInfo(IsEnglish(language) ? "en" : "sv-SE");

    private static bool IsEnglish(ExportLanguagePreference language) => language switch {
        ExportLanguagePreference.English => true,
        ExportLanguagePreference.Swedish => false,
        _ => !string.Equals(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "sv", StringComparison.Ordinal)
    };
}
