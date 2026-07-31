using Microsoft.Extensions.Logging;

namespace Tidverk.Infrastructure.Logging;

public sealed class RollingFileLoggerProvider : ILoggerProvider {
    private readonly Lock gate = new();
    private readonly string logFile;

    public RollingFileLoggerProvider(AppPaths paths) {
        paths.EnsureDirectories();
        logFile = Path.Combine(paths.LogDirectory, $"tidverk-{DateTime.UtcNow:yyyyMMdd}.log");
        foreach (FileInfo oldLog in new DirectoryInfo(paths.LogDirectory).GetFiles("tidverk-*.log")
                     .OrderByDescending(file => file.CreationTimeUtc).Skip(7)) {
            oldLog.Delete();
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() {
    }

    private void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception) {
        string line = $"{DateTimeOffset.Now:O} [{level}] {category} {eventId.Id}: {message}";
        if (exception is not null) {
            line += $" - {exception.GetType().Name}: {exception.Message}";
        }

        lock (gate) {
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
    }

    private sealed class FileLogger(RollingFileLoggerProvider provider, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (IsEnabled(logLevel)) {
                provider.Write(category, logLevel, eventId, formatter(state, exception), exception);
            }
        }
    }
}
