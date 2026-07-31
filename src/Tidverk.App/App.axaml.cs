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
    private ServiceProvider? services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            services = BuildServices();
            MainWindowViewModel viewModel = services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(viewModel);
            ILogger<App> logger = services.GetRequiredService<ILogger<App>>();
            desktop.Startup += async (_, _) => {
                await services.GetRequiredService<DatabaseInitializer>().InitializeAsync().ConfigureAwait(true);
                await viewModel.InitializeAsync().ConfigureAwait(true);
                LogStarted(logger, null);
            };
            desktop.Exit += (_, _) => {
                LogStopped(logger, null);
                services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices() {
        ServiceCollection collection = new();
        AppPaths paths = new();
        paths.EnsureDirectories();
        collection.AddSingleton(paths);
        collection.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new RollingFileLoggerProvider(paths)));
        collection.AddPooledDbContextFactory<TidverkDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabaseFile}"));
        collection.AddSingleton<IClock, SystemClock>();
        collection.AddSingleton<ISwedishHolidayService, SwedishHolidayService>();
        collection.AddSingleton<IWorkEntryRepository, WorkEntryRepository>();
        collection.AddSingleton<ISettingsRepository, SettingsRepository>();
        collection.AddSingleton<IMonthRepository, MonthRepository>();
        collection.AddSingleton<IProjectRepository, ProjectRepository>();
        collection.AddSingleton<DatabaseBackupService>();
        collection.AddSingleton<DatabaseInitializer>();
        collection.AddSingleton<IPrimaryIncomeTaxTable>(_ => new JsonTaxTableProvider(Path.Combine(AppContext.BaseDirectory, "Tax", "Data")));
        collection.AddSingleton<ITaxCalculator>(provider => new TaxCalculator(provider.GetRequiredService<IPrimaryIncomeTaxTable>()));
        collection.AddSingleton<ILocalizationService, LocalizationService>();
        collection.AddSingleton<IThemeService, ThemeService>();
        collection.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        collection.AddSingleton<IDataFolderService, LinuxDataFolderService>();
        collection.AddSingleton<MainWindowViewModel>();
        return collection.BuildServiceProvider();
    }
}
