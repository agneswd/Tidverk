using Microsoft.EntityFrameworkCore;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

public sealed class MonthRepository(IDbContextFactory<TidverkDbContext> contextFactory) : IMonthRepository {
    public async Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        MonthEntity? entity = await context.Months
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Year == year && item.Month == month, cancellationToken);
        return entity is null
            ? new MonthRecord(year, month, suggestedOpeningBalance)
            : new MonthRecord(year, month, entity.OpeningBalanceMinutes, entity.ExpectedMinutesOverride, entity.OpeningBalanceWasEdited);
    }

    public async Task SaveAsync(MonthRecord month, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(month);
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        MonthEntity entity = await context.Months.SingleOrDefaultAsync(item => item.Year == month.Year && item.Month == month.Month, cancellationToken)
            ?? new MonthEntity { Year = month.Year, Month = month.Month };
        if (context.Entry(entity).State == EntityState.Detached) {
            context.Months.Add(entity);
        }

        entity.OpeningBalanceMinutes = month.OpeningBalanceMinutes;
        entity.ExpectedMinutesOverride = month.ExpectedMinutesOverride;
        entity.OpeningBalanceWasEdited = month.OpeningBalanceWasEdited;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(int year, int month, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.WorkEntries
            .Where(item => item.Date.Year == year && item.Date.Month == month)
            .ExecuteDeleteAsync(cancellationToken);
        await context.Months
            .Where(item => item.Year == year && item.Month == month)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
