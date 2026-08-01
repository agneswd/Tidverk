using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tidverk.Infrastructure.Persistence;

/// <summary>
/// Copies the SQLite file through the online backup API, which is safe while the application still
/// holds connections open. Backups are named with a UTC timestamp so they sort chronologically.
/// </summary>
public sealed class DatabaseBackupService(AppPaths paths) {
    private const int RetainedBackupCount = 5;
    private const string BackupPattern = "tidverk-*.db";

    /// <summary>Returns the backup path, or null when there is no database to copy yet.</summary>
    public async Task<string?> CreateAsync(string reason, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFile)) {
            return null;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string destination = Path.Combine(paths.BackupDirectory, $"tidverk-{timestamp}-{SanitizeReason(reason)}.db");
        await CopyDatabaseAsync(paths.DatabaseFile, destination, cancellationToken);
        PruneOldBackups();
        return destination;
    }

    public async Task RestoreAsync(string sourceFile, CancellationToken cancellationToken = default) {
        if (!File.Exists(sourceFile)) {
            throw new FileNotFoundException("Backup database not found.", sourceFile);
        }

        await CreateAsync("before-restore", cancellationToken);
        await CopyDatabaseAsync(sourceFile, paths.DatabaseFile, cancellationToken);
    }

    /// <summary>Keeps the filename free of separators and anything a path could misread.</summary>
    private static string SanitizeReason(string reason) {
        string safe = string.Concat(reason.Where(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
        return safe.Length == 0 ? "backup" : safe;
    }

    /// <summary>Ordered by filename because creation timestamps are not reliable across platforms.</summary>
    private void PruneOldBackups() {
        IEnumerable<FileInfo> expired = new DirectoryInfo(paths.BackupDirectory)
            .GetFiles(BackupPattern)
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .Skip(RetainedBackupCount);
        foreach (FileInfo backup in expired) {
            backup.Delete();
        }
    }

    private static async Task CopyDatabaseAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken) {
        await using SqliteConnection source = new($"Data Source={sourceFile};Mode=ReadOnly");
        await using SqliteConnection destination = new($"Data Source={destinationFile}");
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }
}
