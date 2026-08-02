using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.App.Services;
using Tidverk.App.ViewModels;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.Tests;

/// <summary>
/// Builds a shell backed entirely by in-memory doubles, with the clock pinned to 31 July 2026 so
/// month, holiday and "missing day" behaviour is reproducible.
/// </summary>
internal sealed class ShellFixture {
    public static readonly DateOnly Today = new(2026, 7, 31);

    public InMemoryWorkEntries Entries { get; } = new();

    public InMemorySettings Settings { get; } = new();

    public InMemoryMonths Months { get; } = new();

    public InMemoryProjects Projects { get; } = new();

    public RecordingTheme Theme { get; } = new();

    public RecordingDataFolder DataFolder { get; } = new();

    public StubFileDialog FileDialogs { get; } = new();

    public LocalizationService Localization { get; } = EnglishLocalization();

    public SwedishHolidayService Holidays { get; } = new();

    public static LocalizationService EnglishLocalization() {
        LocalizationService localization = new();
        localization.Apply(LanguagePreference.English);
        return localization;
    }

    public MainWindowViewModel CreateViewModel(
        UpdateService? updates = null,
        IWorkEntryRepository? workEntries = null) {
        AppPaths paths = TempPaths();
        IWorkEntryRepository activeEntries = workEntries ?? Entries;
        MonthlyWorkspaceService workspace = new(
            activeEntries,
            Months,
            Holidays,
            new OpeningBalanceEstimator(activeEntries, Months, Holidays),
            new FixedClock(),
            new TaxCalculator());
        DataOperations dataOperations = new(FileDialogs, DataFolder, new DatabaseBackupService(paths), paths);
        return new(workspace, Settings, Projects, Localization, Theme, dataOperations, NullLogger<MainWindowViewModel>.Instance, updates);
    }

    private static AppPaths TempPaths() =>
        new(Path.Combine(Path.GetTempPath(), $"tidverk-app-tests-{Guid.NewGuid():N}"));

    internal sealed class FixedClock : IClock {
        public DateOnly Today => ShellFixture.Today;

        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }

    internal sealed class InMemoryWorkEntries : IWorkEntryRepository {
        public Dictionary<DateOnly, WorkEntry> Items { get; } = [];

        public int MonthQueryCount { get; private set; }

        public Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) {
            MonthQueryCount++;
            return Task.FromResult<IReadOnlyList<WorkEntry>>(Items.Values
                .Where(item => item.Date.Year == year && item.Date.Month == month)
                .ToArray());
        }

        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(date));

        public Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) {
            Items[entry.Date] = entry;
            return Task.CompletedTask;
        }

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) {
            Items[date] = WorkEntry.CreateIncomplete(date);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemorySettings : ISettingsRepository {
        public AppSettings Value { get; set; } = new(
            "Elias",
            "Employer",
            "Rungard",
            new HourlySalary(202m),
            ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new Minutes(30),
            TaxSettings.Disabled);

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) {
            Value = settings;
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryMonths : IMonthRepository {
        public Dictionary<(int Year, int Month), MonthRecord> Items { get; } = [];

        public Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault((year, month)) ?? new MonthRecord(year, month, suggestedOpeningBalance));

        public Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default) {
            Items[(month.Year, month.Month)] = month;
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryProjects : IProjectRepository {
        public List<string> DefaultsSet { get; } = [];

        public Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) {
            DefaultsSet.Add(name);
            return Task.FromResult(new Project(Guid.NewGuid(), name, true, true));
        }
    }

    internal sealed class RecordingTheme : IThemeService {
        public ThemePreference Applied { get; private set; }

        public void Apply(ThemePreference preference) => Applied = preference;
    }

    internal sealed class StubFileDialog : IFileDialogService {
        public string? ExcelPath { get; set; }

        public string? DatabasePath { get; set; }

        public Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExcelPath);

        public Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabasePath);
    }

    internal sealed class RecordingDataFolder : IDataFolderService {
        public string? OpenedPath { get; private set; }

        public void Open(string path) => OpenedPath = path;
    }
}
