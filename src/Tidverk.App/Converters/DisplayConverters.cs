using Avalonia.Data.Converters;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.Converters;

/// <summary>
/// Turns domain enums into the text the combo boxes show. XAML constructs converters itself, so these
/// read the active localization from <see cref="LocalizationService.Current"/> and fall back to
/// English when it has not been set, which is the case in the previewer.
/// </summary>
public static class DisplayConverters {
    public static IValueConverter TaxMode { get; } = new FuncValueConverter<TaxMode, string>(value => value switch {
        Core.TaxMode.Disabled => Text("TaxModeDisabled", "Disabled"),
        Core.TaxMode.PrimaryIncomeTaxTable => Text("TaxModePrimary", "Swedish tax table"),
        Core.TaxMode.SecondaryIncomeThirtyPercent => Text("TaxModeSecondary", "Secondary income - 30%"),
        Core.TaxMode.ManualMonthlyDeduction => Text("TaxModeManual", "Manual monthly deduction"),
        _ => value.ToString()
    });

    public static IValueConverter Theme { get; } = new FuncValueConverter<ThemePreference, string>(value => value switch {
        ThemePreference.Light => Text("ThemeLight", "Light"),
        ThemePreference.Dark => Text("ThemeDark", "Dark"),
        _ => Text("ThemeSystem", "System")
    });

    public static IValueConverter Language { get; } = new FuncValueConverter<LanguagePreference, string>(value => value switch {
        LanguagePreference.English => Text("LanguageEnglish", "English"),
        LanguagePreference.Swedish => Text("LanguageSwedish", "Swedish"),
        _ => Text("LanguageSystem", "System")
    });

    /// <summary>Each export language is named in its own language, so these two are never translated.</summary>
    public static IValueConverter ExportLanguage { get; } = new FuncValueConverter<ExportLanguagePreference, string>(value => value switch {
        ExportLanguagePreference.English => "English",
        ExportLanguagePreference.Swedish => "Svenska",
        _ => Text("LanguageSystem", "System")
    });

    public static IValueConverter OvertimeMode { get; } = new FuncValueConverter<OvertimeCompensationMode, string>(value => value switch {
        OvertimeCompensationMode.Paid => Text("OvertimeModePaid", "Paid overtime"),
        _ => Text("OvertimeModeCompTime", "Comp time")
    });

    public static IValueConverter SalaryType { get; } = new FuncValueConverter<SalaryType, string>(value => value switch {
        Core.SalaryType.Monthly => Text("SalaryTypeMonthly", "Monthly salary"),
        _ => Text("SalaryTypeHourly", "Hourly wage")
    });

    public static IValueConverter OvertimeThresholdMode { get; } = new FuncValueConverter<OvertimeThresholdMode, string>(value => value switch {
        Core.OvertimeThresholdMode.ScheduledHours => Text("OvertimeThresholdScheduled", "Follow work schedule"),
        _ => Text("OvertimeThresholdFixed", "Fixed hours per day")
    });

    public static IValueConverter CompensationRuleType { get; } = new FuncValueConverter<CompensationRuleType, string>(value => value switch {
        Core.CompensationRuleType.Ob => "OB",
        _ => Text("Overtime", "Overtime")
    });

    public static IValueConverter CompensationRateType { get; } = new FuncValueConverter<CompensationRateType, string>(value => value switch {
        Core.CompensationRateType.FixedHourlyAmount => Text("RateTypeFixed", "Fixed amount/hour"),
        Core.CompensationRateType.FullTimeMonthlySalaryDivisor => Text("RateTypeDivisor", "Full-time monthly salary / divisor"),
        _ => Text("RateTypePremium", "Hourly premium (%)")
    });

    /// <summary>Names the unit the rule's value is entered in, so "50" cannot be read as kronor.</summary>
    public static IValueConverter RateValueLabel { get; } = new FuncValueConverter<CompensationRateType, string>(value => value switch {
        Core.CompensationRateType.FixedHourlyAmount => Text("RateValueAmount", "Amount per hour"),
        Core.CompensationRateType.FullTimeMonthlySalaryDivisor => Text("RateValueDivisor", "Divisor"),
        _ => Text("RateValuePercent", "Premium (%)")
    });

    public static IValueConverter RateTypeHelp { get; } = new FuncValueConverter<CompensationRateType, string>(value => value switch {
        Core.CompensationRateType.FixedHourlyAmount => Text("RateTypeHelpFixed", "A set amount per hour, paid on top of ordinary pay."),
        Core.CompensationRateType.FullTimeMonthlySalaryDivisor => Text("RateTypeHelpDivisor", "Full-time monthly salary divided by this number gives the hourly amount."),
        _ => Text("RateTypeHelpPremium", "A percentage added on top of your hourly rate.")
    });

    public static IValueConverter OvertimeThresholdHelp { get; } = new FuncValueConverter<OvertimeThresholdMode, string>(value => value switch {
        Core.OvertimeThresholdMode.ScheduledHours => Text("OvertimeThresholdScheduledHelp", "Overtime starts once you pass the hours your schedule expects that day."),
        _ => Text("OvertimeThresholdFixedHelp", "Overtime starts after the same number of hours every day.")
    });

    public static IValueConverter ObOvertimeCombination { get; } = new FuncValueConverter<ObOvertimeCombinationMode, string>(value => value switch {
        Core.ObOvertimeCombinationMode.IncludeOb => Text("ObOvertimeIncluded", "Include OB during overtime"),
        _ => Text("ObOvertimeExcluded", "Exclude OB during overtime")
    });

    public static IValueConverter OvertimeDayCategory { get; } = new FuncValueConverter<OvertimeDayCategory, string>(value => value switch {
        Core.OvertimeDayCategory.ScheduledWorkdays => Text("OvertimeDaysScheduled", "Scheduled workdays"),
        Core.OvertimeDayCategory.NonWorkdays => Text("OvertimeDaysNonWorkdays", "Non-workdays"),
        Core.OvertimeDayCategory.PublicHolidays => Text("OvertimeDaysPublicHolidays", "Public holidays"),
        Core.OvertimeDayCategory.ScheduledWeekdays => Text("OvertimeDaysScheduledWeekdays", "Scheduled weekdays"),
        Core.OvertimeDayCategory.Weekends => Text("OvertimeDaysWeekends", "Weekends"),
        Core.OvertimeDayCategory.MajorHolidays => Text("OvertimeDaysMajorHolidays", "Major holidays"),
        Core.OvertimeDayCategory.Monday => Text("WeekdayMonday", "Monday"),
        Core.OvertimeDayCategory.Tuesday => Text("WeekdayTuesday", "Tuesday"),
        Core.OvertimeDayCategory.Wednesday => Text("WeekdayWednesday", "Wednesday"),
        Core.OvertimeDayCategory.Thursday => Text("WeekdayThursday", "Thursday"),
        Core.OvertimeDayCategory.Friday => Text("WeekdayFriday", "Friday"),
        Core.OvertimeDayCategory.Saturday => Text("WeekdaySaturday", "Saturday"),
        Core.OvertimeDayCategory.Sunday => Text("WeekdaySunday", "Sunday"),
        _ => Text("OvertimeDaysAll", "All days")
    });

    public static IValueConverter Scale { get; } = new FuncValueConverter<int, string>(value => $"{value}%");

    /// <summary>
    /// Four metric cards side by side stop fitting their own numbers once the workspace is narrow, so
    /// they fall back to a two-by-two grid rather than clipping the amounts.
    /// </summary>
    public static IValueConverter MetricColumns { get; } = new FuncValueConverter<double, int>(width => width < 800 ? 2 : 4);

    private static string Text(string key, string fallback) => LocalizationService.Current?.Get(key) ?? fallback;
}
