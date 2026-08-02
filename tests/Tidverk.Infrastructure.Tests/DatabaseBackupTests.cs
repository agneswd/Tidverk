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
            await backups.CreateAsync("manual", TestContext.Current.CancellationToken);
        }

        string[] remaining = Directory.GetFiles(paths.BackupDirectory, "tidverk-*.db");
        Assert.Equal(RetainedBackupCount, remaining.Length);
        Assert.Equal(RetainedBackupCount, remaining.Distinct(StringComparer.Ordinal).Count());
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
    public async Task Restoring_a_database_with_only_tidverk_table_names_preserves_the_current_database() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);
        string decoy = Path.Combine(directory, "decoy.db");
        await using (SqliteConnection connection = new($"Data Source={decoy}")) {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT NOT NULL);
                CREATE TABLE Months (Decoy INTEGER);
                CREATE TABLE Projects (Decoy INTEGER);
                CREATE TABLE Settings (Decoy INTEGER);
                CREATE TABLE WorkEntries (Decoy INTEGER);
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            () => backups.RestoreAsync(decoy, TestContext.Current.CancellationToken));

        await using TidverkDbContext current = new(CreateOptions(paths.DatabaseFile));
        Assert.Equal(
            new DateOnly(2026, 7, 1),
            (await current.WorkEntries.SingleAsync(TestContext.Current.CancellationToken)).Date);
    }

    [Fact]
    public async Task Restoring_an_older_tidverk_database_migrates_the_candidate_before_replacing_current_data() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);
        string older = Path.Combine(directory, "older.db");
        await using (TidverkDbContext oldContext = new(CreateOptions(older))) {
            await oldContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await oldContext.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE Settings DROP COLUMN ObOvertimeCombination;
                DELETE FROM __EFMigrationsHistory
                WHERE MigrationId = '202608020001_ObOvertimeCombination';
                """,
                TestContext.Current.CancellationToken);
            await oldContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO WorkEntries (
                    Date, Status, StartTime, EndTime, LunchMinutes, ProjectName, Notes,
                    CreatedAt, UpdatedAt, ScheduledMinutesOverride)
                VALUES (
                    '2026-07-03', 1, '08:00:00', '16:30:00', 30, 'Older', NULL,
                    '2026-07-03T08:00:00+00:00', '2026-07-03T08:00:00+00:00', NULL)
                """,
                TestContext.Current.CancellationToken);
        }

        await backups.RestoreAsync(older, TestContext.Current.CancellationToken);

        await using TidverkDbContext restored = new(CreateOptions(paths.DatabaseFile));
        WorkEntryEntity entry = await restored.WorkEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 7, 3), entry.Date);
        Assert.Contains(
            "202608020001_ObOvertimeCombination",
            await restored.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Restoring_a_backup_from_a_newer_schema_preserves_the_current_database() {
        AppPaths paths = new(directory);
        DatabaseBackupService backups = await CreateStoreAsync(paths);
        string future = (await backups.CreateAsync("future", TestContext.Current.CancellationToken))!;
        await using (SqliteConnection connection = new($"Data Source={future}")) {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES ('209901010001_FutureSchema', '99.0.0')
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            () => backups.RestoreAsync(future, TestContext.Current.CancellationToken));

        await using TidverkDbContext current = new(CreateOptions(paths.DatabaseFile));
        Assert.Equal(
            new DateOnly(2026, 7, 1),
            (await current.WorkEntries.SingleAsync(TestContext.Current.CancellationToken)).Date);
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
        DbContextOptions<TidverkDbContext> options = CreateOptions(paths.DatabaseFile);
        return new WorkEntryRepository(new PooledDbContextFactory<TidverkDbContext>(options), new FixedClock());
    }

    private static DbContextOptions<TidverkDbContext> CreateOptions(string path) =>
        new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

    private sealed class FixedClock : IClock {
        public DateOnly Today => new(2026, 7, 31);

        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }
}
