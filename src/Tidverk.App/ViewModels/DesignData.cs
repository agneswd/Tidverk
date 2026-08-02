using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.App.Services;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.ViewModels;

/// <summary>
/// In-memory stand-ins so the XAML previewer can render a populated window without touching the real
/// database, file system or dialogs.
/// </summary>
internal static class DesignData {
    private static readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "tidverk-design-preview"));
    private static readonly DesignWorkEntries WorkEntries = new();
    private static readonly DesignMonths Months = new();
    private static readonly SwedishHolidayService Holidays = new();

    public static ISettingsRepository Settings { get; } = new DesignSettings();

    public static IProjectRepository Projects { get; } = new DesignProjects();

    public static ILocalizationService Localization { get; } = new LocalizationService();

    public static IThemeService Themes { get; } = new DesignTheme();

    public static MonthlyWorkspaceService Workspace { get; } = new(
        WorkEntries,
        Months,
        Holidays,
        new OpeningBalanceEstimator(WorkEntries, Months, Holidays),
        new DesignClock(),
        new TaxCalculator(new DesignTaxTable()));

    public static DataOperations DataOperations { get; } = new(
        new DesignFileDialogs(),
        new DesignDataFolders(),
        new DatabaseBackupService(Paths),
        Paths);

    public static NullLogger<MainWindowViewModel> Logger => NullLogger<MainWindowViewModel>.Instance;

    private sealed class DesignWorkEntries : IWorkEntryRepository {
        private readonly Dictionary<DateOnly, WorkEntry> entries = CreateEntries();

        public Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkEntry>>(entries.Values
                .Where(entry => entry.Date.Year == year && entry.Date.Month == month)
                .ToArray());

        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(entries.GetValueOrDefault(date));

        public Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) {
            entries[entry.Date] = entry;
            return Task.CompletedTask;
        }

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) {
            entries[date] = WorkEntry.CreateIncomplete(date);
            return Task.CompletedTask;
        }

        private static Dictionary<DateOnly, WorkEntry> CreateEntries() {
            Dictionary<DateOnly, WorkEntry> result = [];
            IEnumerable<DateOnly> weekdays = ExpectedHoursCalculator.GetDates(2026, 7)
                .Where(date => date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
                .Take(19);
            foreach (DateOnly date in weekdays) {
                result[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Route A");
            }

            result[new DateOnly(2026, 7, 26)] = WorkEntry.CreateOff(new DateOnly(2026, 7, 26));
            return result;
        }
    }

    private sealed class DesignSettings : ISettingsRepository {
        private AppSettings settings = new(
            "Alex Nilsson",
            "Employer",
            "Route A",
            new HourlySalary(202m),
            ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new Minutes(30),
            new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1));

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(AppSettings value, CancellationToken cancellationToken = default) {
            settings = value;
            return Task.CompletedTask;
        }
    }

    private sealed class DesignMonths : IMonthRepository {
        public Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MonthRecord(year, month, suggestedOpeningBalance, year == 2026 && month == 7 ? 8_640 : null));

        public Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DesignProjects : IProjectRepository {
        public Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Project(Guid.Parse("9233d871-f030-4185-80a5-5f8749c0e3f6"), name, true, true));
    }

    private sealed class DesignClock : IClock {
        public DateOnly Today => new(2026, 7, 31);

        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class DesignTaxTable : IPrimaryIncomeTaxTable {
        public bool HasYear(int taxYear) => true;

        public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) => 6_079m;
    }

    private sealed class DesignFileDialogs : IFileDialogService {
        public Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class DesignTheme : IThemeService {
        public void Apply(ThemePreference preference) {
        }
    }

    private sealed class DesignDataFolders : IDataFolderService {
        public void Open(string path) {
        }
    }
}
