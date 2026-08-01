namespace Tidverk.App.ViewModels;

/// <summary>
/// A page of the shell. Avalonia data templates map each record to its view, so navigation is a
/// matter of assigning <see cref="MainWindowViewModel.CurrentPage"/>. Both pages bind back to the
/// same shell, which is why they carry it.
/// </summary>
public abstract record AppPage(MainWindowViewModel Shell);

public sealed record MonthWorkspacePage(MainWindowViewModel Shell) : AppPage(Shell);

public sealed record SettingsPage(MainWindowViewModel Shell) : AppPage(Shell);

public enum SettingsSection {
    Employment,
    Appearance,
    Data
}
