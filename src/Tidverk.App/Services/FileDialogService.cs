using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Tidverk.App.Services;

using Tidverk.Infrastructure.Export;

/// <summary>File choosers, behind an interface so view models stay free of Avalonia types.</summary>
public interface IFileDialogService {
    /// <summary>Returns the chosen path, or null when there is no window or the user cancelled.</summary>
    Task<string?> ChooseSpreadsheetFileAsync(string suggestedName, SpreadsheetFormat format, CancellationToken cancellationToken = default);

    Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default);
}

public sealed class AvaloniaFileDialogService(ILocalizationService localization) : IFileDialogService {
    public async Task<string?> ChooseSpreadsheetFileAsync(string suggestedName, SpreadsheetFormat format, CancellationToken cancellationToken = default) {
        if (MainWindowStorage() is not IStorageProvider storage) {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = localization.Get("ExportReport"),
            SuggestedFileName = suggestedName,
            DefaultExtension = format == SpreadsheetFormat.Ods ? "ods" : "xlsx",
            FileTypeChoices = format == SpreadsheetFormat.Ods
                ? [new FilePickerFileType("OpenDocument spreadsheet") { Patterns = ["*.ods"] }]
                : [new FilePickerFileType("Excel workbook") { Patterns = ["*.xlsx"] }],
            ShowOverwritePrompt = true
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }

    public async Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) {
        if (MainWindowStorage() is not IStorageProvider storage) {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = localization.Get("RestoreDatabase"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SQLite database") { Patterns = ["*.db"] }]
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static IStorageProvider? MainWindowStorage() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.StorageProvider
            : null;
}
