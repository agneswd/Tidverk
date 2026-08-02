using Tidverk.App.ViewModels;
using Tidverk.Core;
using Tidverk.Infrastructure.Export;

namespace Tidverk.App.Tests;

/// <summary>
/// Export, backup, restore and the data folder. Every failure here has to surface as a message the
/// user can read, because a command that faults leaves its exception on a task nobody observes.
/// </summary>
public sealed class ShellDataTests {
    [Fact]
    public async Task Export_writes_the_chosen_workbook_and_closes_the_report() {
        ShellFixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Route A");
        string path = Path.Combine(Path.GetTempPath(), $"tidverk-{Guid.NewGuid():N}.xlsx");
        fixture.FileDialogs.ExcelPath = path;
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenReportCommand.Execute(null);

        try {
            await viewModel.ExportCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
            Assert.False(viewModel.IsReportOpen);
            Assert.False(viewModel.HasError);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Export_writes_the_selected_open_document_format() {
        ShellFixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Route A");
        string path = Path.Combine(Path.GetTempPath(), $"tidverk-{Guid.NewGuid():N}.ods");
        fixture.FileDialogs.ExcelPath = path;
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.SelectedExportFormat = SpreadsheetFormat.Ods;
        viewModel.OpenReportCommand.Execute(null);

        try {
            await viewModel.ExportCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
            Assert.False(viewModel.IsReportOpen);
            Assert.False(viewModel.HasError);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_cancelled_export_dialog_leaves_the_report_open_without_an_error() {
        ShellFixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        viewModel.OpenReportCommand.Execute(null);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsReportOpen);
        Assert.False(viewModel.HasError);
    }

    /// <summary>Writing to a directory path cannot succeed; the shell must report it, not throw.</summary>
    [Fact]
    public async Task An_unwritable_export_path_is_reported_rather_than_thrown() {
        ShellFixture fixture = new();
        DateOnly date = new(2026, 7, 1);
        fixture.Entries.Items[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30);
        fixture.FileDialogs.ExcelPath = Path.GetTempPath();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task Backing_up_without_a_database_reports_that_instead_of_failing() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        await viewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal("No database exists yet.", viewModel.BackupStatus);
    }

    [Fact]
    public async Task Restoring_a_file_that_is_not_a_database_reports_instead_of_throwing() {
        ShellFixture fixture = new();
        string path = Path.Combine(Path.GetTempPath(), $"tidverk-not-a-db-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(path, "definitely not sqlite", TestContext.Current.CancellationToken);
        fixture.FileDialogs.DatabasePath = path;
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        try {
            await viewModel.ChooseRestoreCommand.ExecuteAsync(null);
            Assert.True(viewModel.IsRestoreConfirmationOpen);

            await viewModel.ConfirmRestoreCommand.ExecuteAsync(null);

            Assert.False(viewModel.IsRestoreConfirmationOpen);
            Assert.Equal("The database could not be restored from that file.", viewModel.BackupStatus);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Cancelling_a_restore_forgets_the_chosen_file() {
        ShellFixture fixture = new();
        fixture.FileDialogs.DatabasePath = Path.Combine(Path.GetTempPath(), "tidverk-never-restored.db");
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        await viewModel.ChooseRestoreCommand.ExecuteAsync(null);

        viewModel.CancelRestoreCommand.Execute(null);
        await viewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRestoreConfirmationOpen);
        Assert.Empty(viewModel.BackupStatus);
    }

    [Fact]
    public async Task Opening_the_data_folder_uses_the_application_data_directory() {
        ShellFixture fixture = new();
        MainWindowViewModel viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.OpenDataFolderCommand.Execute(null);

        Assert.Equal(viewModel.DataDirectory, fixture.DataFolder.OpenedPath);
    }
}
