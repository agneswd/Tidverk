using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.Infrastructure.Tests;

/// <summary>
/// Settings that cannot be read must not stop the application from opening: the timesheet itself is
/// still intact, so the repository falls back for the damaged field only.
/// </summary>
public sealed class SettingsFallbackTests : IDisposable {
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"tidverk-settings-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("9,12")]
    public async Task Unreadable_working_weekdays_fall_back_to_the_standard_week(string stored) {
        SettingsRepository repository = await CorruptStoredSettingsAsync(entity => entity.ExpectedWorkingWeekdays = stored);

        AppSettings settings = await repository.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedHoursSettings.Standard.WorkingWeekdays, settings.ExpectedHours.WorkingWeekdays);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("""[{"Name":"","DayCategory":0,"StartTime":"17:00:00","EndTime":"18:00:00","PremiumPercent":50}]""")]
    [InlineData("""[{"Name":"Evening","DayCategory":0,"StartTime":"17:00:00","EndTime":"18:00:00","PremiumPercent":9000}]""")]
    public async Task Unreadable_rate_bands_are_dropped_rather_than_thrown(string stored) {
        SettingsRepository repository = await CorruptStoredSettingsAsync(entity => entity.OvertimeRateBandsJson = stored);

        AppSettings settings = await repository.GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(settings.OvertimeCompensation.RateBands);
    }

    [Fact]
    public async Task Valid_weekdays_survive_a_round_trip() {
        SettingsRepository repository = await CorruptStoredSettingsAsync(entity => entity.ExpectedWorkingWeekdays = "6, 0");

        AppSettings settings = await repository.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], settings.ExpectedHours.WorkingWeekdays);
    }

    public void Dispose() {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) {
            Directory.Delete(directory, true);
        }
    }

    private async Task<SettingsRepository> CorruptStoredSettingsAsync(Action<AppSettingsEntity> corrupt) {
        AppPaths paths = new(directory);
        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={paths.DatabaseFile}")
            .Options;
        PooledDbContextFactory<TidverkDbContext> factory = new(options);
        await new DatabaseInitializer(factory, paths, new DatabaseBackupService(paths), NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        SettingsRepository repository = new(factory, NullLogger<SettingsRepository>.Instance);
        await repository.SaveAsync(
            new AppSettings(
                "Elias",
                "Employer",
                "Rungard",
                new HourlySalary(202m),
                ExpectedHoursSettings.Standard,
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new Minutes(30),
                TaxSettings.Disabled),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        TidverkDbContext context = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false)) {
            AppSettingsEntity entity = await context.Settings.SingleAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            corrupt(entity);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        return repository;
    }
}
