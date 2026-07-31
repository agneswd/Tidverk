namespace Tidverk.Infrastructure;

public sealed class AppPaths {
    public AppPaths(string? dataDirectory = null) {
        DataDirectory = dataDirectory ?? GetDefaultDataDirectory();
        DatabaseFile = Path.Combine(DataDirectory, "tidverk.db");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        BackupDirectory = Path.Combine(DataDirectory, "backups");
        ExportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Tidverk");
    }

    public string DataDirectory { get; }

    public string DatabaseFile { get; }

    public string LogDirectory { get; }

    public string BackupDirectory { get; }

    public string ExportDirectory { get; }

    public void EnsureDirectories() {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(ExportDirectory);
    }

    private static string GetDefaultDataDirectory() {
        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string root = string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdgDataHome;
        return Path.Combine(root, "Tidverk");
    }
}
