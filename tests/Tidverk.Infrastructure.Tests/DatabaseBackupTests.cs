using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.Infrastructure.Tests;

public sealed class DatabaseBackupTests : IDisposable {
    private const int RetainedBackupCount = 5;

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"tidverk-backup-{Guid.NewGuid():N}");

    [Fact]
    public async Task Only_the_newest_backups_are_kept() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);

        for (int index = 0; index < RetainedBackupCount + 3; index++) {
            // The filename carries a whole-second timestamp, so the reason keeps each name distinct.
            await backups.CreateAsync($"manual-{index}", TestContext.Current.CancellationToken);
        }

        string[] remaining = Directory.GetFiles(paths.BackupDirectory, "tidverk-*.db");
        Assert.Equal(RetainedBackupCount, remaining.Length);
        Assert.All(remaining, file => Assert.DoesNotContain("manual-0.db", file, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_backup_is_a_readable_database_with_the_same_rows() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);

        string backup = (await backups.CreateAsync("manual", TestContext.Current.CancellationToken))!;

        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={backup}")
            .Options;
        await using TidverkDbContext context = new(options);
        WorkEntryEntity entry = await context.WorkEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 7, 1), entry.Date);
    }

    [Fact]
    public async Task Restoring_a_missing_file_fails_before_touching_the_database() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => backups.RestoreAsync(Path.Combine(directory, "does-not-exist.db"), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(paths.BackupDirectory, "tidverk-*.db"));
    }

    [Fact]
    public async Task Restoring_an_unrelated_sqlite_database_preserves_the_current_database() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);
        string unrelated = Path.Combine(directory, "unrelated.db");
        await using (SqliteConnection connection = new($"Data Source={unrelated}")) {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE OtherData (Id INTEGER PRIMARY KEY)";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            () => backups.RestoreAsync(unrelated, TestContext.Current.CancellationToken));

        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={paths.DatabaseFile}")
            .Options;
        await using TidverkDbContext context = new(options);
        Assert.Equal(new DateOnly(2026, 7, 1), (await context.WorkEntries.SingleAsync(TestContext.Current.CancellationToken)).Date);
    }

    [Fact]
    public async Task A_valid_tidverk_backup_replaces_the_current_database() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);
        string backup = (await backups.CreateAsync("source", TestContext.Current.CancellationToken))!;
        WorkEntryRepository entries = CreateEntries(paths);
        await entries.SaveAsync(
            WorkEntry.CreateWorked(new DateOnly(2026, 7, 2), new TimeOnly(8, 0), new TimeOnly(16, 30), 30),
            TestContext.Current.CancellationToken);

        await backups.RestoreAsync(backup, TestContext.Current.CancellationToken);

        Assert.NotNull(await entries.GetAsync(new DateOnly(2026, 7, 1), TestContext.Current.CancellationToken));
        Assert.Null(await entries.GetAsync(new DateOnly(2026, 7, 2), TestContext.Current.CancellationToken));
    }

    public void Dispose() {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) {
            Directory.Delete(directory, true);
        }
    }

    private static async Task<DatabaseBackupService> CreateStoreAsync(AppPaths paths) {
        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={paths.DatabaseFile}")
            .Options;
        PooledDbContextFactory<TidverkDbContext> factory = new(options);
        DatabaseBackupService backups = new(paths);
        await new DatabaseInitializer(factory, paths, backups, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        WorkEntryRepository entries = new(factory, new FixedClock());
        await entries.SaveAsync(
            WorkEntry.CreateWorked(new DateOnly(2026, 7, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        return backups;
    }

    private static WorkEntryRepository CreateEntries(AppPaths paths) {
        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={paths.DatabaseFile}")
            .Options;
        return new WorkEntryRepository(new PooledDbContextFactory<TidverkDbContext>(options), new FixedClock());
    }

    private sealed class FixedClock : IClock {
        public DateOnly Today => new(2026, 7, 31);

        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }
}
