using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Export;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.Services;

/// <summary>Desktop file operations used by the data settings and report dialog.</summary>
public sealed class DataOperations(
    IFileDialogService fileDialogs,
    IDataFolderService dataFolders,
    DatabaseBackupService backups,
    AppPaths paths) {
    public string DataDirectory => paths.DataDirectory;

    public async Task<bool> ExportAsync(
        ReportExportRequest request,
        string suggestedName,
        SpreadsheetFormat format,
        CancellationToken cancellationToken = default) {
        string? path = await fileDialogs.ChooseSpreadsheetFileAsync(suggestedName, format, cancellationToken);
        if (path is null) {
            return false;
        }

        if (format == SpreadsheetFormat.Ods) {
            await OdsReportExporter.ExportAsync(request, path, cancellationToken);
        }
        else {
            await ExcelReportExporter.ExportAsync(request, path, cancellationToken);
        }
        return true;
    }

    public Task<string?> CreateBackupAsync(CancellationToken cancellationToken = default) =>
        backups.CreateAsync("manual", cancellationToken);

    public Task<string?> ChooseRestoreFileAsync(CancellationToken cancellationToken = default) =>
        fileDialogs.ChooseDatabaseFileAsync(cancellationToken);

    public Task RestoreAsync(string path, CancellationToken cancellationToken = default) =>
        backups.RestoreAsync(path, cancellationToken);

    public void OpenDataFolder() => dataFolders.Open(paths.DataDirectory);
}
