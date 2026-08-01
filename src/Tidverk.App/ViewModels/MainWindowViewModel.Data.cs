using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Tidverk.Core;
using Tidverk.Infrastructure.Export;

namespace Tidverk.App.ViewModels;

/// <summary>Exporting the month and looking after the local database.</summary>
public sealed partial class MainWindowViewModel {
    private bool isReportOpen;
    private bool isRestoreConfirmationOpen;
    private string backupStatus = string.Empty;
    private string? pendingRestorePath;

    public bool IsReportOpen { get => isReportOpen; private set => SetProperty(ref isReportOpen, value); }

    public bool IsRestoreConfirmationOpen { get => isRestoreConfirmationOpen; private set => SetProperty(ref isRestoreConfirmationOpen, value); }

    public string BackupStatus { get => backupStatus; private set => SetProperty(ref backupStatus, value); }

    public string DataDirectory => dataOperations.DataDirectory;

    [RelayCommand]
    private void OpenReport() {
        if (IsMonthUnstarted) {
            return;
        }

        CurrentPage = monthWorkspacePage;
        IsReportOpen = true;
    }

    [RelayCommand]
    private void CloseReport() => IsReportOpen = false;

    [RelayCommand]
    private async Task ExportAsync() {
        if (summary is null) {
            return;
        }

        ReportExportRequest request = new(
            selectedMonth.Year,
            selectedMonth.Month,
            settings.EmployeeName,
            settings.EmployerName,
            monthEntries.Values.ToArray(),
            summary,
            settings.ExportLanguagePreference,
            settings.OvertimeCompensation.Mode,
            settings.OvertimeCompensation.DailyThresholdHours,
            settings.ExpectedHours,
            settings.OvertimeCompensation);
        string suggestedName = ExportFilename.Create(settings.EmployeeName, selectedMonth.Year, selectedMonth.Month);

        try {
            if (!await dataOperations.ExportAsync(request, suggestedName)) {
                return;
            }
            IsReportOpen = false;
        }
        catch (Exception exception) {
            logger.LogError(exception, "Excel export failed");
            ErrorText = localization.Get("ExportFailed");
        }
    }

    [RelayCommand]
    private async Task BackupAsync() {
        try {
            string? path = await dataOperations.CreateBackupAsync();
            BackupStatus = path is null
                ? localization.Get("NoBackupDatabase")
                : localization.Format("BackupCreated", Path.GetFileName(path));
        }
        catch (Exception exception) {
            logger.LogError(exception, "Creating a database backup failed");
            BackupStatus = localization.Get("BackupFailed");
        }
    }

    [RelayCommand]
    private async Task ChooseRestoreAsync() {
        try {
            pendingRestorePath = await dataOperations.ChooseRestoreFileAsync();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Choosing a database to restore failed");
            pendingRestorePath = null;
            BackupStatus = localization.Get("RestoreFailed");
        }

        IsRestoreConfirmationOpen = pendingRestorePath is not null;
    }

    /// <summary>Restoring swaps the file underneath the open connections, so the month is reloaded afterwards.</summary>
    [RelayCommand]
    private async Task ConfirmRestoreAsync() {
        if (pendingRestorePath is null) {
            return;
        }

        try {
            await dataOperations.RestoreAsync(pendingRestorePath);
            IsRestoreConfirmationOpen = false;
            BackupStatus = localization.Get("RestoreDone");
            settings = await settingsRepository.GetAsync();
            CopySettingsToForm();
            await LoadMonthAsync();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Restoring the database from {Path} failed", pendingRestorePath);
            IsRestoreConfirmationOpen = false;
            BackupStatus = localization.Get("RestoreFailed");
        }
        finally {
            pendingRestorePath = null;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() {
        try {
            dataOperations.OpenDataFolder();
        }
        catch (Exception exception) {
            logger.LogError(exception, "Opening the data folder failed");
            BackupStatus = localization.Get("DataFolderFailed");
        }
    }

    [RelayCommand]
    private void CancelRestore() {
        IsRestoreConfirmationOpen = false;
        pendingRestorePath = null;
    }
}
