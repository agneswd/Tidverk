using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidverk.App.Services;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.ViewModels;

internal sealed record DesignServiceSet(
    IWorkEntryRepository WorkEntries,
    ISettingsRepository Settings,
    IMonthRepository Months,
    IProjectRepository Projects,
    ISwedishHolidayService Holidays,
    IClock Clock,
    ITaxCalculator Taxes,
    IFileDialogService FileDialogs,
    ILocalizationService Localization,
    IThemeService Themes,
    AppPaths Paths,
    DatabaseBackupService Backups,
    IDataFolderService DataFolders,
    ILogger<MainWindowViewModel> Logger);

internal static class DesignData {
    private static readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "tidverk-design-preview"));

    public static DesignServiceSet Services { get; } = new(
        new DesignWorkEntries(),
        new DesignSettings(),
        new DesignMonths(),
        new DesignProjects(),
        new SwedishHolidayService(),
        new DesignClock(),
        new TaxCalculator(new DesignTaxTable()),
        new DesignFileDialogs(),
        new LocalizationService(),
        new DesignTheme(),
        Paths,
        new DatabaseBackupService(Paths),
        new DesignDataFolders(),
        NullLogger<MainWindowViewModel>.Instance);

    private sealed class DesignWorkEntries : IWorkEntryRepository {
        private readonly Dictionary<DateOnly, WorkEntry> entries = CreateEntries();

        public Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkEntry>>(entries.Values.Where(entry => entry.Date.Year == year && entry.Date.Month == month).ToArray());

        public Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(entries.GetValueOrDefault(date));

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
            foreach (DateOnly date in ExpectedHoursCalculator.GetDates(2026, 7)
                         .Where(date => date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday).Take(19)) {
                result[date] = WorkEntry.CreateWorked(date, new TimeOnly(8, 0), new TimeOnly(16, 30), 30, "Rungard");
            }

            result[new DateOnly(2026, 7, 26)] = WorkEntry.CreateOff(new DateOnly(2026, 7, 26));
            return result;
        }
    }

    private sealed class DesignSettings : ISettingsRepository {
        private AppSettings settings = new(
            "Elias Andreasson", "Employer", "Rungard", new HourlySalary(202m), ExpectedHoursSettings.Standard,
            new TimeOnly(8, 0), new TimeOnly(16, 30), new Minutes(30), new TaxSettings(TaxMode.PrimaryIncomeTaxTable, 2026, 33, 1));

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
        public Task<IReadOnlyList<Project>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([new Project(Guid.Parse("9233d871-f030-4185-80a5-5f8749c0e3f6"), "Rungard", true, true)]);

        public Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Project(Guid.Parse("9233d871-f030-4185-80a5-5f8749c0e3f6"), name, true, true));
    }

    private sealed class DesignClock : IClock {
        public DateOnly Today => new(2026, 7, 31);
        public DateTimeOffset UtcNow => new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class DesignTaxTable : IPrimaryIncomeTaxTable {
        public decimal GetPreliminaryTax(int taxYear, int tableNumber, int column, decimal grossPay) => 6_079m;
    }

    private sealed class DesignFileDialogs : IFileDialogService {
        public Task<string?> ChooseExcelFileAsync(string suggestedName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> ChooseDatabaseFileAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class DesignTheme : IThemeService { public void Apply(ThemePreference preference) { } }
    private sealed class DesignDataFolders : IDataFolderService { public void Open(string path) { } }
}
