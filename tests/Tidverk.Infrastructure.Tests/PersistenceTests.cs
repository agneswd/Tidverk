using Microsoft.Data.Sqlite;
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
    public async Task Ob_overtime_column_defaults_to_excluding_ob() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);
        await using TidverkDbContext context = await store.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Settings (
                Id, EmployeeName, EmployerName, DefaultProject, HourlyRate,
                ExpectedHoursPerWorkday, ExpectedWorkingWeekdays, ExcludePublicHolidays,
                DefaultStartTime, DefaultEndTime, DefaultLunchMinutes, ThemePreference,
                MonthViewPreference, TaxMode, TaxYear, TaxTableNumber, TaxColumn,
                OpeningBalanceMinutes)
            VALUES (
                1, 'Employee', 'Employer', 'Project', 200,
                8, '1,2,3,4,5', 1,
                '08:00:00', '16:30:00', 30, 0,
                0, 0, 0, 0, 0,
                0)
            """,
            TestContext.Current.CancellationToken);

        int stored = await context.Database
            .SqlQueryRaw<int>("SELECT ObOvertimeCombination AS Value FROM Settings WHERE Id = 1")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)ObOvertimeCombinationMode.ExcludeOb, stored);
    }

    [Fact]
    public async Task Work_entry_repository_creates_updates_and_resets() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);
        WorkEntryRepository repository = new(store.Factory, new FakeClock());
        DateOnly date = new(2026, 7, 1);

        await repository.SaveAsync(
            WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Route A", scheduledMinutesOverride: 0),
            TestContext.Current.CancellationToken);
        WorkEntry saved = Assert.Single(await repository.GetMonthAsync(2026, 7, TestContext.Current.CancellationToken));
        Assert.Equal(480, saved.WorkedMinutes.Value);
        Assert.Equal(0, saved.ScheduledMinutesOverride);

        await repository.SaveAsync(WorkEntry.CreateOff(date), TestContext.Current.CancellationToken);
        Assert.Equal(WorkEntryStatus.Off, (await repository.GetAsync(date, TestContext.Current.CancellationToken))!.Status);

        await repository.ResetAsync(date, TestContext.Current.CancellationToken);
        Assert.Equal(WorkEntryStatus.Incomplete, (await repository.GetAsync(date, TestContext.Current.CancellationToken))!.Status);
    }

    [Fact]
    public async Task Settings_and_months_round_trip() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);
        SettingsRepository settingsRepository = new(store.Factory, NullLogger<SettingsRepository>.Instance);
        MonthRepository monthRepository = new(store.Factory);
        AppSettings settings = CreateMonthlySalarySettings();

        await settingsRepository.SaveAsync(settings, TestContext.Current.CancellationToken);
        await monthRepository.SaveAsync(new MonthRecord(2026, 7, 60, 9_120, true), TestContext.Current.CancellationToken);

        AppSettings loaded = await settingsRepository.GetAsync(TestContext.Current.CancellationToken);
        MonthRecord month = await monthRepository.GetAsync(2026, 7, 0, TestContext.Current.CancellationToken);
        Assert.Equal("Alex Nilsson", loaded.EmployeeName);
        Assert.Equal(ThemePreference.Dark, loaded.ThemePreference);
        Assert.Equal(LanguagePreference.Swedish, loaded.LanguagePreference);
        Assert.Equal(CurrencyPreference.EUR, loaded.CurrencyPreference);
        Assert.Equal(ExportLanguagePreference.English, loaded.ExportLanguagePreference);
        Assert.Equal(OvertimeCompensationMode.Paid, loaded.OvertimeCompensation.Mode);
        Assert.Equal(72m, loaded.OvertimeCompensation.DefaultRateValue);
        Assert.Equal(7.5m, loaded.OvertimeCompensation.DailyThresholdHours);
        Assert.Equal(OvertimeThresholdMode.ScheduledHours, loaded.OvertimeCompensation.ThresholdMode);
        Assert.Equal(CompensationRateType.FullTimeMonthlySalaryDivisor, loaded.OvertimeCompensation.DefaultRateType);
        Assert.Equal(ObOvertimeCombinationMode.IncludeOb, loaded.OvertimeCompensation.ObOvertimeCombination);
        Assert.Single(loaded.OvertimeCompensation.RateBands);
        Assert.Equal("Evening", loaded.OvertimeCompensation.RateBands[0].Name);
        Assert.Equal(94m, loaded.OvertimeCompensation.RateBands[0].RateValue);
        Assert.Equal(SalaryType.Monthly, loaded.Salary.Type);
        Assert.Equal(12_123m, loaded.Salary.MonthlySalary);
        Assert.Equal(50m, loaded.Salary.EmploymentPercent);
        Assert.Equal(125, loaded.InterfaceScalePercent);
        Assert.Equal(60, month.OpeningBalanceMinutes);
        Assert.Equal(9_120, month.ExpectedMinutesOverride);
        Assert.True(month.OpeningBalanceWasEdited);
    }

    private static AppSettings CreateMonthlySalarySettings() => new(
            "Alex Nilsson",
            "Employer",
            "Route A",
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
                72m,
                7.5m,
                [new(
                    "Evening",
                    OvertimeDayCategory.ScheduledWorkdays,
                    new TimeOnly(17, 0),
                    new TimeOnly(21, 0),
                    0m,
                    rateType: CompensationRateType.FullTimeMonthlySalaryDivisor,
                    rateValue: 94m)],
                OvertimeThresholdMode.ScheduledHours,
                CompensationRateType.FullTimeMonthlySalaryDivisor,
                ObOvertimeCombinationMode.IncludeOb),
            salarySettings: new SalarySettings(SalaryType.Monthly, new HourlySalary(0m), 12_123m, 50m));

    [Fact]
    public async Task Backup_service_copies_database() {
        TestStore store = CreateStore();
        await store.Initializer.InitializeAsync(TestContext.Current.CancellationToken);

        string? backup = await store.Backups.CreateAsync("manual", TestContext.Current.CancellationToken);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
    }

    public void Dispose() {
        SqliteConnection.ClearAllPools();
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
