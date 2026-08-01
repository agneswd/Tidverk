using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tidverk.App.Services;
using Tidverk.App.ViewModels;
using Tidverk.Core;
using Tidverk.Infrastructure;
using Tidverk.Infrastructure.Logging;
using Tidverk.Infrastructure.Persistence;
using Tidverk.Infrastructure.Tax;

namespace Tidverk.App;

public partial class App : Application {
    private static readonly Action<ILogger, Exception?> LogStarted =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "Started"), "Tidverk started");
    private static readonly Action<ILogger, Exception?> LogStopped =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "Stopped"), "Tidverk stopped");
    private static readonly Action<ILogger, Exception?> LogStartupFailed =
        LoggerMessage.Define(LogLevel.Critical, new EventId(3, "StartupFailed"), "Tidverk failed to start");

    private ServiceProvider? services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            Start(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The database and the first month load after the lifetime has started: doing that work here
    /// would block the dispatcher before the window is shown.
    /// </summary>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop) {
        ServiceProvider provider = BuildServices();
        services = provider;
        MainWindowViewModel viewModel = provider.GetRequiredService<MainWindowViewModel>();
        ILogger<App> logger = provider.GetRequiredService<ILogger<App>>();
        desktop.MainWindow = new MainWindow(viewModel);

        desktop.Startup += async (_, _) => {
            try {
                await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync().ConfigureAwait(true);
                await viewModel.InitializeAsync().ConfigureAwait(true);
                LogStarted(logger, null);
            }
            catch (Exception exception) {
                LogStartupFailed(logger, exception);
                viewModel.ShowStartupFailure();
            }
        };
        desktop.Exit += (_, _) => {
            LogStopped(logger, null);
            provider.Dispose();
        };
    }

    private static ServiceProvider BuildServices() {
        AppPaths paths = new();
        paths.EnsureDirectories();

        ServiceCollection collection = new();
        collection.AddSingleton(paths);
        collection.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new RollingFileLoggerProvider(paths)));
        collection.AddPooledDbContextFactory<TidverkDbContext>(options => options.UseSqlite($"Data Source={paths.DatabaseFile}"));

        collection.AddSingleton<IClock, SystemClock>();
        collection.AddSingleton<ISwedishHolidayService, SwedishHolidayService>();
        collection.AddSingleton<IWorkEntryRepository, WorkEntryRepository>();
        collection.AddSingleton<ISettingsRepository, SettingsRepository>();
        collection.AddSingleton<IMonthRepository, MonthRepository>();
        collection.AddSingleton<IProjectRepository, ProjectRepository>();
        collection.AddSingleton<OpeningBalanceEstimator>();
        collection.AddSingleton<MonthlyWorkspaceService>();
        collection.AddSingleton<DatabaseBackupService>();
        collection.AddSingleton<DatabaseInitializer>();
        collection.AddSingleton<IPrimaryIncomeTaxTable>(_ => new JsonTaxTableProvider(Path.Combine(AppContext.BaseDirectory, "Tax", "Data")));
        collection.AddSingleton<ITaxCalculator, TaxCalculator>();

        collection.AddSingleton<ILocalizationService, LocalizationService>();
        collection.AddSingleton<IThemeService, ThemeService>();
        collection.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        collection.AddSingleton<IDataFolderService, DesktopDataFolderService>();
        collection.AddSingleton<DataOperations>();
        collection.AddSingleton<MainWindowViewModel>();
        return collection.BuildServiceProvider();
    }
}
