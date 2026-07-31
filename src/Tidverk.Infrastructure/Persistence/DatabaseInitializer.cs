using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Tidverk.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<TidverkDbContext> contextFactory,
    AppPaths paths,
    DatabaseBackupService backups,
    ILogger<DatabaseInitializer> logger) {
    private static readonly Action<ILogger, Exception?> LogApplyingMigrations =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "ApplyingMigrations"), "Applying Tidverk database migrations");

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        paths.EnsureDirectories();
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        bool hasDatabase = File.Exists(paths.DatabaseFile);
        bool hasPendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        if (hasDatabase && hasPendingMigrations) {
            await backups.CreateAsync("before-migration", cancellationToken);
        }

        LogApplyingMigrations(logger, null);
        await context.Database.MigrateAsync(cancellationToken);
    }
}

public sealed class DatabaseBackupService(AppPaths paths) {
    private const int RetainedBackupCount = 5;

    public async Task<string?> CreateAsync(string reason, CancellationToken cancellationToken = default) {
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFile)) {
            return null;
        }

        string safeReason = string.Concat(reason.Where(character => char.IsLetterOrDigit(character) || character == '-'));
        string destination = Path.Combine(paths.BackupDirectory, $"tidverk-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeReason}.db");
        await CopyDatabaseAsync(paths.DatabaseFile, destination, cancellationToken);

        foreach (FileInfo oldBackup in new DirectoryInfo(paths.BackupDirectory).GetFiles("tidverk-*.db")
                     .OrderByDescending(file => file.CreationTimeUtc).Skip(RetainedBackupCount)) {
            oldBackup.Delete();
        }

        return destination;
    }

    public async Task RestoreAsync(string sourceFile, CancellationToken cancellationToken = default) {
        if (!File.Exists(sourceFile)) {
            throw new FileNotFoundException("Backup database not found.", sourceFile);
        }

        await CreateAsync("before-restore", cancellationToken);
        await CopyDatabaseAsync(sourceFile, paths.DatabaseFile, cancellationToken);
    }

    private static async Task CopyDatabaseAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken) {
        await using SqliteConnection source = new($"Data Source={sourceFile};Mode=ReadOnly");
        await using SqliteConnection destination = new($"Data Source={destinationFile}");
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }
}
