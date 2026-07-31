using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Tidverk.Core;

namespace Tidverk.App.Services;

public interface IThemeService {
    void Apply(ThemePreference preference);
}

public sealed class ThemeService : IThemeService {
    public void Apply(ThemePreference preference) {
        if (Application.Current is null) {
            return;
        }

        Application.Current.RequestedThemeVariant = preference switch {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}

public interface IFileDialogService {
    Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default);

    Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default);
}

public sealed class AvaloniaFileDialogService(ILocalizationService localization) : IFileDialogService {
    public async Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default) {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) {
            return null;
        }

        FilePickerFileType excelType = new("Excel workbook") { Patterns = ["*.xlsx"] };
        IStorageFile? file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = localization.Get("ExportReport"),
            SuggestedFileName = suggestedName,
            DefaultExtension = "xlsx",
            FileTypeChoices = [excelType],
            ShowOverwritePrompt = true
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }

    public async Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = localization.Get("RestoreDatabase"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SQLite database") { Patterns = ["*.db"] }]
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}

public interface IDataFolderService {
    void Open(string path);
}

public sealed class LinuxDataFolderService : IDataFolderService {
    public void Open(string path) {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", path) {
            UseShellExecute = false
        });
    }
}
