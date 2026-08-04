using System.Globalization;
using FreeDataExportsv2;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Export;

/// <summary>Writes the same employer report as the XLSX export in OpenDocument format.</summary>
public static class OdsReportExporter {
    public static async Task ExportAsync(ReportExportRequest request, string path, CancellationToken cancellationToken = default) {
        ExportValidationResult validation = ReportExportValidator.Validate(request);
        if (!validation.CanExport) {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        OdsFile workbook = new() { Creator = "Tidverk", LastModifiedBy = "Tidverk" };
        CellOptions title = new() { DataType = DataType.String, FontSize = 16, Bold = true };
        CellOptions heading = new() { DataType = DataType.String, Bold = true, BackgroundColor = "FFE3ECE7" };
        CellOptions boldText = new() { DataType = DataType.String, Bold = true };
        CellOptions boldNumber = new() { DataType = DataType.Number, Bold = true };

        WriteReport(workbook.AddWorksheet(MonthTitle(request)), request, title, heading, boldText, boldNumber);
        WriteBalance(workbook.AddWorksheet(Text(request, "Time balance", "Tidsbalans")), request, title, boldText);
        await workbook.SaveAsync(path).WaitAsync(cancellationToken);
    }

    private static void WriteReport(
        XlsxWorksheet report,
        ReportExportRequest request,
        CellOptions title,
        CellOptions heading,
        CellOptions boldText,
        CellOptions boldNumber) {
        AddRow(report, [(Text(request, "Tidverk - Time report", "Tidverk - Tidrapport"), title)]);
        AddRow(report, [(request.EmployeeName, null), (string.Empty, null), (string.Empty, null), (request.EmployerName, null)]);
        AddRow(report, []);
        AddRow(report, [
            (Text(request, "Day", "Dag"), heading),
            ("Start", heading),
            (Text(request, "Stop", "Slut"), heading),
            ("Lunch", heading),
            (Text(request, "Hours", "Timmar"), heading),
            ("Status", heading),
            (Text(request, "Project", "Projekt"), heading)
        ]);

        WriteDays(report, request);
        WriteReportTotals(report, request, boldText, boldNumber);
        report.ColumnWidths("1.2cm", "2.2cm", "2.2cm", "5.2cm", "2.8cm", "3cm", "5cm");
    }

    private static void WriteDays(XlsxWorksheet report, ReportExportRequest request) {
        Dictionary<DateOnly, WorkEntry> entries = request.Entries.ToDictionary(entry => entry.Date);
        for (int day = 1; day <= DateTime.DaysInMonth(request.Year, request.Month); day++) {
            DateOnly date = new(request.Year, request.Month, day);
            if (!entries.TryGetValue(date, out WorkEntry? entry)) {
                AddRow(report, [(day, DataType.Number)]);
                continue;
            }

            AddRow(report, entry.Status switch {
                WorkEntryStatus.Worked => [
                    (day, DataType.Number),
                    (entry.StartTime!.Value.ToString("HH:mm", CultureInfo.InvariantCulture), null),
                    (entry.EndTime!.Value.ToString("HH:mm", CultureInfo.InvariantCulture), null),
                    (entry.LunchMinutes.Value, DataType.Number),
                    (entry.WorkedHours, DataType.Number),
                    (string.Empty, null),
                    (entry.ProjectName ?? string.Empty, null)
                ],
                WorkEntryStatus.Off => [
                    (day, DataType.Number),
                    (string.Empty, null),
                    (string.Empty, null),
                    (string.Empty, null),
                    (string.Empty, null),
                    (Text(request, "Day off", "Ledig"), null)
                ],
                _ => [(day, DataType.Number)]
            });
        }
    }

    private static void WriteReportTotals(XlsxWorksheet report, ReportExportRequest request, CellOptions boldText, CellOptions boldNumber) {
        AddRow(report, []);
        AddRow(report, [
            (string.Empty, null), (string.Empty, null), (string.Empty, null),
            (request.UsesMonthlyHourlyPayBasis
                ? Text(request, "Total paid hours", "Totalt betalda timmar")
                : Text(request, "Total regular hours", "Totalt ordinarie timmar"), boldText),
            (request.PaidOrdinaryHours, boldNumber)
        ]);
        AddRow(report, [
            (string.Empty, null), (string.Empty, null), (string.Empty, null),
            (request.UsesMonthlyHourlyPayBasis
                ? Text(request, "Comp time earned", "Intjänad komptid")
                : Text(request, "Total overtime", "Total övertid"), boldText),
            (request.OvertimeOrCompTimeHours, boldNumber)
        ]);
        if (HasOb(request)) {
            AddRow(report, [
                (string.Empty, null), (string.Empty, null), (string.Empty, null),
                (Text(request, "Total OB hours", "Totala OB-timmar"), boldText),
                (request.Summary.ObHours, boldNumber)
            ]);
        }
    }

    private static void WriteBalance(
        XlsxWorksheet balance,
        ReportExportRequest request,
        CellOptions title,
        CellOptions boldText) {
        AddRow(balance, [(Text(request, "Time balance - personal tracking", "Tidsbalans - personlig uppföljning"), title)]);
        AddRow(balance, [(Text(request, "Month", "Månad"), null), (MonthTitle(request), null)]);
        AddRow(balance, []);
        AddBalanceRow(
            balance,
            request.UsesMonthlyHourlyPayBasis
                ? Text(request, "Paid hours", "Betalda timmar")
                : Text(request, "Regular hours", "Ordinarie timmar"),
            request.PaidOrdinaryHours,
            boldText);
        AddBalanceRow(
            balance,
            request.UsesMonthlyHourlyPayBasis
                ? Text(request, "Comp time earned", "Intjänad komptid")
                : Text(request, "Overtime", "Övertid"),
            request.OvertimeOrCompTimeHours,
            boldText);
        AddBalanceRow(balance, Text(request, "Worked hours", "Arbetade timmar"), request.Summary.WorkedHours, boldText);
        if (HasOb(request)) {
            AddBalanceRow(balance, Text(request, "OB hours", "OB-timmar"), request.Summary.ObHours, boldText);
        }
        AddBalanceRow(balance, Text(request, "Expected hours", "Förväntade timmar"), request.Summary.ExpectedHours, boldText);
        AddBalanceRow(balance, Text(request, "Monthly time balance", "Månadens tidsbalans"), request.Summary.MonthlyDifferenceMinutes / 60m, boldText);
        AddBalanceRow(balance, Text(request, "Opening time balance", "Ingående tidsbalans"), request.Summary.OpeningBalanceMinutes / 60m, boldText);
        AddBalanceRow(balance, Text(request, "Closing time balance", "Utgående tidsbalans"), request.Summary.ClosingBalanceMinutes / 60m, boldText);
        balance.ColumnWidths("6cm", "3.5cm");
    }

    private static void AddBalanceRow(XlsxWorksheet sheet, string label, decimal value, CellOptions labelStyle) =>
        AddRow(sheet, [(label, labelStyle), (value, DataType.Number)]);

    private static void AddRow(XlsxWorksheet sheet, IReadOnlyList<(object Value, object? Format)> cells) {
        XlsxRowBuilder row = sheet.AddRow();
        foreach ((object value, object? format) in cells) {
            if (format is CellOptions options) {
                row.AddCell(value, options);
            }
            else {
                row.AddCell(value, format is DataType dataType ? dataType : DataType.String);
            }
        }
    }

    private static bool HasOb(ReportExportRequest request) =>
        request.Summary.ObHours != 0m ||
        request.OvertimeSettings?.RateBands.Any(rule => rule.CompensationType == CompensationRuleType.Ob) == true;

    private static string MonthTitle(ReportExportRequest request) {
        CultureInfo culture = CultureInfo.GetCultureInfo(IsEnglish(request.Language) ? "en" : "sv-SE");
        return culture.TextInfo.ToTitleCase(new DateTime(request.Year, request.Month, 1).ToString("MMMM yyyy", culture));
    }

    private static string Text(ReportExportRequest request, string english, string swedish) =>
        IsEnglish(request.Language) ? english : swedish;

    private static bool IsEnglish(ExportLanguagePreference language) => language switch {
        ExportLanguagePreference.English => true,
        ExportLanguagePreference.Swedish => false,
        _ => !string.Equals(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "sv", StringComparison.Ordinal)
    };
}
