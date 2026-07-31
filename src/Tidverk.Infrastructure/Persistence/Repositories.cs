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
        return entities.Select(Map).ToArray();
    }

    public async Task<WorkEntry?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        WorkEntryEntity? entity = await context.WorkEntries.AsNoTracking().SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
        return entity is null ? null : Map(entity);
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
        entity.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(DateOnly date, CancellationToken cancellationToken = default) {
        await SaveAsync(WorkEntry.CreateIncomplete(date), cancellationToken);
    }

    private static WorkEntry Map(WorkEntryEntity entity) => entity.Status switch {
        WorkEntryStatus.Worked when entity.StartTime is not null && entity.EndTime is not null => WorkEntry.CreateWorked(
            entity.Date,
            entity.StartTime.Value,
            entity.EndTime.Value,
            entity.LunchMinutes,
            entity.ProjectName,
            entity.Notes),
        WorkEntryStatus.Off => WorkEntry.CreateOff(entity.Date, entity.Notes),
        _ => WorkEntry.CreateIncomplete(entity.Date)
    };
}

public sealed class SettingsRepository(IDbContextFactory<TidverkDbContext> contextFactory) : ISettingsRepository {
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AppSettingsEntity? entity = await context.Settings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (entity is null) {
            return AppSettings.Unconfigured;
        }

        DayOfWeek[] weekdays = entity.ExpectedWorkingWeekdays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => (DayOfWeek)int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        TaxSettings tax = new(entity.TaxMode, entity.TaxYear, entity.TaxTableNumber, entity.TaxColumn, entity.ManualTaxValue);
        return new(
            entity.EmployeeName,
            entity.EmployerName,
            entity.DefaultProject,
            new HourlySalary(entity.HourlyRate),
            new ExpectedHoursSettings(entity.ExpectedHoursPerWorkday, weekdays, entity.ExcludePublicHolidays),
            entity.DefaultStartTime,
            entity.DefaultEndTime,
            new Minutes(entity.DefaultLunchMinutes),
            tax,
            entity.ThemePreference,
            entity.OpeningBalanceMinutes,
            entity.MonthViewPreference,
            entity.LanguagePreference,
            entity.CurrencyPreference,
            entity.InterfaceScalePercent);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(settings);
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AppSettingsEntity entity = await context.Settings.SingleOrDefaultAsync(item => item.Id == 1, cancellationToken)
            ?? new AppSettingsEntity { Id = 1 };
        if (context.Entry(entity).State == EntityState.Detached) {
            context.Settings.Add(entity);
        }

        entity.EmployeeName = settings.EmployeeName;
        entity.EmployerName = settings.EmployerName;
        entity.DefaultProject = settings.DefaultProject;
        entity.HourlyRate = settings.HourlySalary.Amount;
        entity.ExpectedHoursPerWorkday = settings.ExpectedHours.HoursPerWorkday;
        entity.ExpectedWorkingWeekdays = string.Join(',', settings.ExpectedHours.WorkingWeekdays.Select(day => (int)day));
        entity.ExcludePublicHolidays = settings.ExpectedHours.ExcludePublicHolidays;
        entity.DefaultStartTime = settings.DefaultStartTime;
        entity.DefaultEndTime = settings.DefaultEndTime;
        entity.DefaultLunchMinutes = settings.DefaultLunchMinutes.Value;
        entity.ThemePreference = settings.ThemePreference;
        entity.MonthViewPreference = settings.MonthViewPreference;
        entity.TaxMode = settings.TaxSettings.Mode;
        entity.TaxYear = settings.TaxSettings.TaxYear;
        entity.TaxTableNumber = settings.TaxSettings.TableNumber;
        entity.TaxColumn = settings.TaxSettings.Column;
        entity.ManualTaxValue = settings.TaxSettings.ManualMonthlyDeduction;
        entity.OpeningBalanceMinutes = settings.OpeningBalanceMinutes;
        entity.LanguagePreference = settings.LanguagePreference;
        entity.CurrencyPreference = settings.CurrencyPreference;
        entity.InterfaceScalePercent = settings.InterfaceScalePercent;
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class MonthRepository(IDbContextFactory<TidverkDbContext> contextFactory) : IMonthRepository {
    public async Task<MonthRecord> GetAsync(int year, int month, int suggestedOpeningBalance, CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        MonthEntity? entity = await context.Months.AsNoTracking().SingleOrDefaultAsync(item => item.Year == year && item.Month == month, cancellationToken);
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
}

public sealed class ProjectRepository(IDbContextFactory<TidverkDbContext> contextFactory) : IProjectRepository {
    public async Task<IReadOnlyList<Project>> GetActiveAsync(CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Projects.AsNoTracking().Where(item => item.IsActive).OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name).Select(item => new Project(item.Id, item.Name, item.IsActive, item.IsDefault)).ToListAsync(cancellationToken);
    }

    public async Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        string trimmedName = name.Trim();
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        ProjectEntity? entity = await context.Projects.SingleOrDefaultAsync(item => item.Name == trimmedName, cancellationToken);
        if (entity is null) {
            entity = new ProjectEntity { Id = Guid.NewGuid(), Name = trimmedName, IsActive = true };
            context.Projects.Add(entity);
        }

        await context.Projects.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDefault, false), cancellationToken);
        entity.IsDefault = true;
        await context.SaveChangesAsync(cancellationToken);
        return new(entity.Id, entity.Name, entity.IsActive, true);
    }
}
