using Microsoft.EntityFrameworkCore;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

public sealed class WorkEntryRepository(IDbContextFactory<TidverkDbContext> contextFactory, IClock clock) : IWorkEntryRepository {
    public async Task<IReadOnlyList<WorkEntry>> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        List<WorkEntryEntity> entities = await context.WorkEntries
            .AsNoTracking()
            .Where(item => item.Date.Year == year && item.Date.Month == month)
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        return entities.ConvertAll(ToDomain);
    }

    public async Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        WorkEntryEntity? entity = await context.WorkEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveAsync(WorkEntry entry, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        IReadOnlyList<string> errors = entry.Validate();
        if (errors.Count > 0) {
            throw new DomainValidationException(string.Join(" ", errors));
        }

        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        WorkEntryEntity? entity = await context.WorkEntries.SingleOrDefaultAsync(item => item.Date == entry.Date, cancellationToken);
        if (entity is null) {
            entity = new WorkEntryEntity { Date = entry.Date, CreatedAt = clock.UtcNow };
            context.WorkEntries.Add(entity);
        }

        entity.Status = entry.Status;
        entity.StartTime = entry.StartTime;
        entity.EndTime = entry.EndTime;
        entity.LunchMinutes = entry.LunchMinutes.Value;
        entity.ProjectName = entry.ProjectName;
        entity.Notes = entry.Notes;
        entity.ScheduledMinutesOverride = entry.ScheduledMinutesOverride;
        entity.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        SaveAsync(WorkEntry.CreateIncomplete(date), cancellationToken);

    /// <summary>A worked row that lost its times is read back as incomplete rather than failing the whole month.</summary>
    private static WorkEntry ToDomain(WorkEntryEntity entity) => entity.Status switch {
        WorkEntryStatus.Worked when entity.StartTime is not null && entity.EndTime is not null => WorkEntry.CreateWorked(
            entity.Date,
            entity.StartTime.Value,
            entity.EndTime.Value,
            entity.LunchMinutes,
            entity.ProjectName,
            entity.Notes,
            entity.ScheduledMinutesOverride),
        WorkEntryStatus.Off => WorkEntry.CreateOff(entity.Date, entity.Notes),
        _ => WorkEntry.CreateIncomplete(entity.Date)
    };
}
