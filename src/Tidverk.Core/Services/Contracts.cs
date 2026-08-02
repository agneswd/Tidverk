namespace Tidverk.Core;

/// <summary>The current date and time, injected so calculations stay testable.</summary>
public interface IClock {
    DateOnly Today { get; }

    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock {
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IWorkEntryRepository {
    Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default);

    Task SaveRangeAsync(IReadOnlyList<WorkEntry> entries, CancellationToken cancellationToken = default);

    Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository {
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IMonthRepository {
    /// <summary>Loads the stored month, or a new one opening at <paramref name="suggestedOpeningBalance"/> when none exists.</summary>
    Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default);

    Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default);

    Task ResetAsync(int year, int month, CancellationToken cancellationToken = default);
}

public interface IProjectRepository {
    /// <summary>Creates the project if it is new and makes it the only default.</summary>
    Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default);
}
