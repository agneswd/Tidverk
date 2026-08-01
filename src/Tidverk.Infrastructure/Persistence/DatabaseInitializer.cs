using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Tidverk.Infrastructure.Persistence;

/// <summary>Brings the local database up to the current schema, taking a backup before it changes anything.</summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<TidverkDbContext> contextFactory,
    AppPaths paths,
    DatabaseBackupService backups,
    ILogger<DatabaseInitializer> logger) {
    private static readonly Action<ILogger, int, Exception?> LogApplyingMigrations = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(1, "ApplyingMigrations"),
        "Applying {Count} pending Tidverk database migration(s)");

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        paths.EnsureDirectories();
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        int pendingCount = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        if (pendingCount == 0) {
            return;
        }

        LogApplyingMigrations(logger, pendingCount, null);
        if (File.Exists(paths.DatabaseFile)) {
            await backups.CreateAsync("before-migration", cancellationToken);
        }

        await context.Database.MigrateAsync(cancellationToken);
    }
}
