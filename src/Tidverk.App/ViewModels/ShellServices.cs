using Microsoft.Extensions.Logging;
using Tidverk.App.Services;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Persistence;

namespace Tidverk.App.ViewModels;

/// <summary>
/// Everything <see cref="MainWindowViewModel"/> depends on, gathered into one record so the container,
/// the design-time data and the tests all build the shell the same way.
/// </summary>
public sealed record ShellServices(
    IWorkEntryRepository WorkEntries,
    ISettingsRepository Settings,
    IMonthRepository Months,
    IProjectRepository Projects,
    ISwedishHolidayService Holidays,
    OpeningBalanceEstimator OpeningBalances,
    IClock Clock,
    ITaxCalculator Taxes,
    IFileDialogService FileDialogs,
    ILocalizationService Localization,
    IThemeService Themes,
    AppPaths Paths,
    DatabaseBackupService Backups,
    IDataFolderService DataFolders,
    ILogger<MainWindowViewModel> Logger);
