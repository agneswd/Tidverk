using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tidverk.Infrastructure.Logging;

/// <summary>
/// Appends log lines to one file per UTC day and keeps the last week. Writing a log line must never
/// take the application down, so file errors are reported on the console and otherwise ignored.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider {
    private const int RetainedLogCount = 7;

    private readonly Lock gate = new();
    private readonly string logFile;

    public RollingFileLoggerProvider(AppPaths paths) {
        ArgumentNullException.ThrowIfNull(paths);
        paths.EnsureDirectories();
        logFile = Path.Combine(paths.LogDirectory, $"tidverk-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        PruneOldLogs(paths.LogDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() {
    }

    /// <summary>Ordered by filename: the date in the name is more reliable than file timestamps.</summary>
    private static void PruneOldLogs(string logDirectory) {
        try {
            IEnumerable<FileInfo> expired = new DirectoryInfo(logDirectory)
                .GetFiles("tidverk-*.log")
                .OrderByDescending(file => file.Name, StringComparer.Ordinal)
                .Skip(RetainedLogCount);
            foreach (FileInfo log in expired) {
                log.Delete();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Tidverk could not prune old log files: {exception.Message}");
        }
    }

    private void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception) {
        StringBuilder line = new();
        line.Append(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:O} [{level}] {category} {eventId.Id}: {message}");
        if (exception is not null) {
            line.AppendLine().Append(exception);
        }

        line.Append(Environment.NewLine);
        try {
            lock (gate) {
                File.AppendAllText(logFile, line.ToString());
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Tidverk could not write to the log file: {failure.Message}");
        }
    }

    private sealed class FileLogger(RollingFileLoggerProvider provider, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel)) {
                provider.Write(category, logLevel, eventId, formatter(state, exception), exception);
            }
        }
    }
}
