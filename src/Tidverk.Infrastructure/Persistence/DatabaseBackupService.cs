using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tidverk.Infrastructure.Persistence;

/// <summary>
/// Copies the SQLite file through the online backup API, which is safe while the application still
/// holds connections open. Backups are named with a UTC timestamp so they sort chronologically.
/// </summary>
public sealed class DatabaseBackupService(AppPaths paths) {
    private const int RetainedBackupCount = 5;
    private const string BackupPattern = "tidverk-*.db";
    private const int RequiredTableCount = 5;

    /// <summary>Returns the backup path, or null when there is no database to copy yet.</summary>
    public async Task<string?> CreateAsync(string reason, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFile)) {
            return null;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);
        string destination = Path.Combine(
            paths.BackupDirectory,
            $"tidverk-{timestamp}-{Guid.NewGuid():N}-{SanitizeReason(reason)}.db");
        await CopyDatabaseAsync(paths.DatabaseFile, destination, cancellationToken);
        PruneOldBackups();
        return destination;
    }

    public async Task RestoreAsync(string sourceFile, CancellationToken cancellationToken = default) {
        if (!File.Exists(sourceFile)) {
            throw new FileNotFoundException("Backup database not found.", sourceFile);
        }

        paths.EnsureDirectories();
        string candidate = Path.Combine(paths.BackupDirectory, $".restore-{Guid.NewGuid():N}.db");
        try {
            await CopyDatabaseAsync(sourceFile, candidate, cancellationToken);
            await PrepareTidverkDatabaseAsync(candidate, cancellationToken);
            string? safetyBackup = await CreateAsync("before-restore", cancellationToken);
            try {
                SqliteConnection.ClearAllPools();
                await CopyDatabaseAsync(candidate, paths.DatabaseFile, cancellationToken);
            }
            catch {
                if (safetyBackup is not null) {
                    SqliteConnection.ClearAllPools();
                    await CopyDatabaseAsync(safetyBackup, paths.DatabaseFile, CancellationToken.None);
                }

                throw;
            }
            finally {
                SqliteConnection.ClearAllPools();
            }
        }
        finally {
            SqliteConnection.ClearAllPools();
            File.Delete(candidate);
        }
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
        await using SqliteConnection source = new(new SqliteConnectionStringBuilder {
            DataSource = sourceFile,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await using SqliteConnection destination = new(new SqliteConnectionStringBuilder {
            DataSource = destinationFile,
            Pooling = false
        }.ToString());
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task ValidateTidverkDatabaseAsync(string path, CancellationToken cancellationToken) {
        await using SqliteConnection connection = new($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        object? integrity = await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(Convert.ToString(integrity, CultureInfo.InvariantCulture), "ok", StringComparison.Ordinal)) {
            throw new InvalidDataException("The selected database failed SQLite's integrity check.");
        }

        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('__EFMigrationsHistory', 'Months', 'Projects', 'Settings', 'WorkEntries')
            """;
        long tableCount = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (tableCount != RequiredTableCount) {
            throw new InvalidDataException("The selected file is not a Tidverk database.");
        }
    }

    /// <summary>
    /// Migrates a private candidate before the live database is touched, then asks the current model
    /// to read every table. Unknown migration IDs reject backups made by a newer Tidverk version.
    /// </summary>
    private static async Task PrepareTidverkDatabaseAsync(string path, CancellationToken cancellationToken) {
        await ValidateTidverkDatabaseAsync(path, cancellationToken);

        try {
            DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder {
                    DataSource = path,
                    Pooling = false
                }.ToString())
                .Options;
            await using TidverkDbContext context = new(options);
            HashSet<string> knownMigrations = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            IEnumerable<string> appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (appliedMigrations.Any(migration => !knownMigrations.Contains(migration))) {
                throw new InvalidDataException("The selected backup was created by a newer Tidverk version.");
            }

            await context.Database.MigrateAsync(cancellationToken);

            // A database can imitate the expected table names. Executing current-model queries also
            // verifies the columns and their mappings before the candidate replaces user data.
            await context.Months.AsNoTracking().Take(1).ToArrayAsync(cancellationToken);
            await context.Projects.AsNoTracking().Take(1).ToArrayAsync(cancellationToken);
            await context.Settings.AsNoTracking().Take(1).ToArrayAsync(cancellationToken);
            await context.WorkEntries.AsNoTracking().Take(1).ToArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (InvalidDataException) {
            throw;
        }
        catch (Exception exception) {
            throw new InvalidDataException("The selected file is not a compatible Tidverk database.", exception);
        }

        await ValidateTidverkDatabaseAsync(path, cancellationToken);
    }
}
