using Tidverk.Core;

namespace Tidverk.Infrastructure.Export;

public enum SpreadsheetFormat { Xlsx, Ods }

/// <summary>Everything the workbook needs. Salary and tax are deliberately absent from exports.</summary>
public sealed record ReportExportRequest(
    int Year,
    int Month,
    string EmployeeName,
    string EmployerName,
    IReadOnlyList<WorkEntry> Entries,
    MonthlySummary Summary,
    ExportLanguagePreference Language = ExportLanguagePreference.Swedish,
    OvertimeCompensationMode OvertimeMode = OvertimeCompensationMode.CompTime,
    decimal DailyOvertimeThresholdHours = 8m,
    ExpectedHoursSettings? ExpectedHours = null,
    OvertimeCompensationSettings? OvertimeSettings = null);

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
    /// <summary>Collapses anything a file system could reject, so the suggested name is always usable.</summary>
    public static string Create(string employeeName, int year, int month, string extension = "xlsx") {
        ArgumentNullException.ThrowIfNull(employeeName);
        char[] invalid = Path.GetInvalidFileNameChars();
        string safeName = string.Concat(employeeName.Trim()
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character));
        safeName = string.Join('_', safeName.Split('_', StringSplitOptions.RemoveEmptyEntries));
        return $"Tidverk_{safeName}_{year:D4}-{month:D2}.{extension}";
    }
}
