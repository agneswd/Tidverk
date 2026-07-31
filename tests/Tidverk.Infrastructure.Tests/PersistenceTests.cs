using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.Infrastructure.Tests;

public sealed class PersistenceTests : IDisposable {
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"tidverk-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Migrations_create_all_application_tables() {
        TestStore store = CreateStore();

        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);

        await using TidverkDbContext context = await store.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        string[] tables = await context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'").ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains("WorkEntries", tables);
        Assert.Contains("Settings", tables);
        Assert.Contains("Months", tables);
        Assert.Contains("Projects", tables);
    }

    [Fact]
    public async Task Work_entry_repository_creates_updates_and_resets() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);
        WorkEntryRepository repository = new(store.Factory, new FakeClock());
        DateOnly date = new(2026, 7, 1);

        await repository.SaveAsync(WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Rungard"), TestContext.Current.CancellationToken);
        WorkEntry saved = Assert.Single(await repository.GetMonthAsync(2026, 7, TestContext.Current.CancellationToken));
        Assert.Equal(480, saved.WorkedMinutes.Value);

        await repository.SaveAsync(WorkEntry.CreateOff(date), TestContext.Current.CancellationToken);
        Assert.Equal(WorkEntryStatus.Off, (await repository.GetAsync(date, TestContext.Current.CancellationToken))!.Status);

        await repository.ResetAsync(date, TestContext.Current.CancellationToken);
        Assert.Equal(WorkEntryStatus.Incomplete, (await repository.GetAsync(date, TestContext.Current.CancellationToken))!.Status);
    }

    [Fact]
    public async Task Settings_and_months_round_trip() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);
        SettingsRepository settingsRepository = new(store.Factory);
        MonthRepository monthRepository = new(store.Factory);
        AppSettings settings = new(
            "Elias Andreasson",
            "Employer",
            "Rungard",
            new HourlySalary(202m),
            ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new Minutes(30),
            new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1),
            ThemePreference.Dark,
            60,
            languagePreference: LanguagePreference.Swedish,
            currencyPreference: CurrencyPreference.EUR,
            interfaceScalePercent: 125,
            exportLanguagePreference: ExportLanguagePreference.English,
            overtimeCompensation: new OvertimeCompensationSettings(
                OvertimeCompensationMode.Paid,
                75m,
                7.5m,
                [new("Evening", OvertimeDayCategory.ScheduledWorkdays, new TimeOnly(17, 0), new TimeOnly(21, 0), 50m)]));

        await settingsRepository.SaveAsync(settings, TestContext.Current.CancellationToken);
        await monthRepository.SaveAsync(new MonthRecord(2026, 7, 60, 9_120, true), TestContext.Current.CancellationToken);

        AppSettings loaded = await settingsRepository.GetAsync(TestContext.Current.CancellationToken);
        MonthRecord month = await monthRepository.GetAsync(2026, 7, 0, TestContext.Current.CancellationToken);
        Assert.Equal("Elias Andreasson", loaded.EmployeeName);
        Assert.Equal(ThemePreference.Dark, loaded.ThemePreference);
        Assert.Equal(LanguagePreference.Swedish, loaded.LanguagePreference);
        Assert.Equal(CurrencyPreference.EUR, loaded.CurrencyPreference);
        Assert.Equal(ExportLanguagePreference.English, loaded.ExportLanguagePreference);
        Assert.Equal(OvertimeCompensationMode.Paid, loaded.OvertimeCompensation.Mode);
        Assert.Equal(75m, loaded.OvertimeCompensation.PremiumPercent);
        Assert.Equal(7.5m, loaded.OvertimeCompensation.DailyThresholdHours);
        Assert.Single(loaded.OvertimeCompensation.RateBands);
        Assert.Equal("Evening", loaded.OvertimeCompensation.RateBands[0].Name);
        Assert.Equal(125, loaded.InterfaceScalePercent);
        Assert.Equal(60, month.OpeningBalanceMinutes);
        Assert.Equal(9_120, month.ExpectedMinutesOverride);
        Assert.True(month.OpeningBalanceWasEdited);
    }

    [Fact]
    public async Task Backup_service_copies_database() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);

        string? backup = await store.Backups.CreateAsync("manual", TestContext.Current.CancellationToken);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
    }

    public void Dispose() {
        if (Directory.Exists(directory)) {
            Directory.Delete(directory, true);
        }
    }

    private TestStore CreateStore() {
        AppPaths paths = new(directory);
        DbContextOptions<TidverkDbContext> options = new DbContextOptionsBuilder<TidverkDbContext>()
            .UseSqlite($"Data Source={paths.DatabaseFile}")
            .Options;
        PooledDbContextFactory<TidverkDbContext> factory = new(options);
        DatabaseBackupService backups = new(paths);
        DatabaseInitializer initializer = new(factory, paths, backups, NullLogger<DatabaseInitializer>.Instance);
        return new(factory, initializer, backups);
    }

    private sealed record TestStore(
        IDbContextFactory<TidverkDbContext> Factory,
        DatabaseInitializer Initializer,
        DatabaseBackupService Backups);

    private sealed class FakeClock : IClock {
        public DateOnly Today => new(2026, 7, 31);

        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }
}
