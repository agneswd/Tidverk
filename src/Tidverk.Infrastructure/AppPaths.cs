namespace Tidverk.Infrastructure;

/// <summary>
/// Where Tidverk keeps its data. Everything lives under one directory so the whole application state
/// can be copied or removed in one step.
/// </summary>
public sealed class AppPaths {
    public AppPaths(string? dataDirectory = null) {
        DataDirectory = dataDirectory ?? GetDefaultDataDirectory();
        DatabaseFile = Path.Combine(DataDirectory, "tidverk.db");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        BackupDirectory = Path.Combine(DataDirectory, "backups");
    }

    public string DataDirectory { get; }

    public string DatabaseFile { get; }

    public string LogDirectory { get; }

    public string BackupDirectory { get; }

    public void EnsureDirectories() {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }

    /// <summary>%LOCALAPPDATA% on Windows, and the XDG data directory elsewhere.</summary>
    private static string GetDefaultDataDirectory() {
        if (OperatingSystem.IsWindows()) {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tidverk");
        }

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string root = string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdgDataHome;
        return Path.Combine(root, "Tidverk");
    }
}
