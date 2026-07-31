namespace Tidverk.App.ViewModels;

public abstract record AppPage(MainWindowViewModel Shell);

public sealed record MonthWorkspacePage(MainWindowViewModel Shell) : AppPage(Shell);

public sealed record SettingsPage(MainWindowViewModel Shell) : AppPage(Shell);

public enum SettingsSection {
    WorkDefaults,
    SalaryAndTax,
    Appearance,
    Data
}
