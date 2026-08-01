using System.Diagnostics;

namespace Tidverk.App.Services;

public interface IDataFolderService {
    void Open(string path);
}

/// <summary>
/// Opens a folder in the desktop's own file manager. Each platform has its own launcher; Windows and
/// macOS were previously handed the Linux one, which silently did nothing.
/// </summary>
public sealed class DesktopDataFolderService : IDataFolderService {
    public void Open(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ProcessStartInfo start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(path) { UseShellExecute = true }
            : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", [path]) { UseShellExecute = false };
        using Process? process = Process.Start(start);
    }
}
