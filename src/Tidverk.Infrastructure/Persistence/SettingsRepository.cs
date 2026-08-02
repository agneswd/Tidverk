using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

public sealed class SettingsRepository(IDbContextFactory<TidverkDbContext> contextFactory, ILogger<SettingsRepository> logger) : ISettingsRepository {
    private const int SettingsRowId = 1;

    private static readonly Action<ILogger, string, Exception?> LogUnreadableWeekdays = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "UnreadableWorkingWeekdays"),
        "Stored working weekdays '{Stored}' could not be read; using the standard Monday-Friday week");

    private static readonly Action<ILogger, Exception?> LogUnreadableRateBands = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(2, "UnreadableRateBands"),
        "Stored overtime rate bands could not be read; continuing without them");

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) {
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AppSettingsEntity? entity = await context.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == SettingsRowId, cancellationToken);
        return entity is null ? AppSettings.Unconfigured : ToDomain(entity);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(settings);
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AppSettingsEntity entity = await context.Settings.SingleOrDefaultAsync(item => item.Id == SettingsRowId, cancellationToken)
            ?? new AppSettingsEntity { Id = SettingsRowId };
        if (context.Entry(entity).State == EntityState.Detached) {
            context.Settings.Add(entity);
        }

        CopyFrom(settings, entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    private AppSettings ToDomain(AppSettingsEntity entity) => new(
        entity.EmployeeName,
        entity.EmployerName,
        entity.DefaultProject,
        new HourlySalary(entity.HourlyRate),
        new ExpectedHoursSettings(entity.ExpectedHoursPerWorkday, ParseWorkingWeekdays(entity.ExpectedWorkingWeekdays), entity.ExcludePublicHolidays),
        entity.DefaultStartTime,
        entity.DefaultEndTime,
        new Minutes(entity.DefaultLunchMinutes),
        new TaxSettings(entity.TaxMode, entity.TaxYear, entity.TaxTableNumber, entity.TaxColumn, entity.ManualTaxValue),
        entity.ThemePreference,
        entity.OpeningBalanceMinutes,
        entity.MonthViewPreference,
        entity.LanguagePreference,
        entity.CurrencyPreference,
        entity.InterfaceScalePercent,
        entity.ExportLanguagePreference,
        new OvertimeCompensationSettings(
            entity.OvertimeCompensationMode,
            entity.OvertimePremiumPercent,
            entity.OvertimeDailyThresholdHours,
            ParseRateBands(entity.OvertimeRateBandsJson),
            entity.OvertimeThresholdMode,
            entity.OvertimeDefaultRateType,
            entity.ObOvertimeCombination),
        new SalarySettings(
            entity.SalaryType,
            new HourlySalary(entity.HourlyRate),
            entity.MonthlySalary,
            entity.EmploymentPercent));

    private static void CopyFrom(AppSettings settings, AppSettingsEntity entity) {
        entity.EmployeeName = settings.EmployeeName;
        entity.EmployerName = settings.EmployerName;
        entity.DefaultProject = settings.DefaultProject;
        entity.HourlyRate = settings.HourlySalary.Amount;
        entity.SalaryType = settings.Salary.Type;
        entity.MonthlySalary = settings.Salary.MonthlySalary;
        entity.EmploymentPercent = settings.Salary.EmploymentPercent;
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
        entity.ExportLanguagePreference = settings.ExportLanguagePreference;
        entity.OvertimeCompensationMode = settings.OvertimeCompensation.Mode;
        entity.OvertimePremiumPercent = settings.OvertimeCompensation.PremiumPercent;
        entity.OvertimeDailyThresholdHours = settings.OvertimeCompensation.DailyThresholdHours;
        entity.OvertimeThresholdMode = settings.OvertimeCompensation.ThresholdMode;
        entity.OvertimeDefaultRateType = settings.OvertimeCompensation.DefaultRateType;
        entity.ObOvertimeCombination = settings.OvertimeCompensation.ObOvertimeCombination;
        entity.OvertimeRateBandsJson = JsonSerializer.Serialize(settings.OvertimeCompensation.RateBands);
    }

    /// <summary>Falls back to the standard week: unreadable preferences must not stop the application from opening.</summary>
    private IReadOnlyCollection<DayOfWeek> ParseWorkingWeekdays(string stored) {
        DayOfWeek[] weekdays = stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) ? day : -1)
            .Where(day => Enum.IsDefined((DayOfWeek)day))
            .Select(day => (DayOfWeek)day)
            .ToArray();
        if (weekdays.Length > 0) {
            return weekdays;
        }

        LogUnreadableWeekdays(logger, stored, null);
        return ExpectedHoursSettings.Standard.WorkingWeekdays;
    }

    private IReadOnlyList<OvertimeRateBand> ParseRateBands(string json) {
        try {
            return JsonSerializer.Deserialize<OvertimeRateBand[]>(json) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException) {
            LogUnreadableRateBands(logger, exception);
            return [];
        }
    }
}
