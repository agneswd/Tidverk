using Avalonia.Data.Converters;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.Converters;

public static class DisplayConverters {
    private static string Text(string key, string fallback) => LocalizationService.Current?.Get(key) ?? fallback;

    public static IValueConverter TaxMode { get; } = new FuncValueConverter<TaxMode, string>(value => value switch {
        Tidverk.Core.TaxMode.Disabled => Text("TaxModeDisabled", "Disabled"),
        Tidverk.Core.TaxMode.PrimaryIncomeTaxTable => Text("TaxModePrimary", "Swedish tax table"),
        Tidverk.Core.TaxMode.SecondaryIncomeThirtyPercent => Text("TaxModeSecondary", "Secondary income - 30%"),
        Tidverk.Core.TaxMode.ManualMonthlyDeduction => Text("TaxModeManual", "Manual monthly deduction"),
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

    public static IValueConverter ExportLanguage { get; } = new FuncValueConverter<ExportLanguagePreference, string>(value => value switch {
        ExportLanguagePreference.English => "English",
        _ => "Svenska"
    });

    public static IValueConverter OvertimeMode { get; } = new FuncValueConverter<OvertimeCompensationMode, string>(value => value switch {
        OvertimeCompensationMode.Paid => Text("OvertimeModePaid", "Paid overtime"),
        _ => Text("OvertimeModeCompTime", "Comp time")
    });

    public static IValueConverter OvertimeDayCategory { get; } = new FuncValueConverter<OvertimeDayCategory, string>(value => value switch {
        Tidverk.Core.OvertimeDayCategory.ScheduledWorkdays => Text("OvertimeDaysScheduled", "Scheduled workdays"),
        Tidverk.Core.OvertimeDayCategory.NonWorkdays => Text("OvertimeDaysNonWorkdays", "Non-workdays"),
        Tidverk.Core.OvertimeDayCategory.PublicHolidays => Text("OvertimeDaysPublicHolidays", "Public holidays"),
        Tidverk.Core.OvertimeDayCategory.Monday => Text("WeekdayMonday", "Monday"),
        Tidverk.Core.OvertimeDayCategory.Tuesday => Text("WeekdayTuesday", "Tuesday"),
        Tidverk.Core.OvertimeDayCategory.Wednesday => Text("WeekdayWednesday", "Wednesday"),
        Tidverk.Core.OvertimeDayCategory.Thursday => Text("WeekdayThursday", "Thursday"),
        Tidverk.Core.OvertimeDayCategory.Friday => Text("WeekdayFriday", "Friday"),
        Tidverk.Core.OvertimeDayCategory.Saturday => Text("WeekdaySaturday", "Saturday"),
        Tidverk.Core.OvertimeDayCategory.Sunday => Text("WeekdaySunday", "Sunday"),
        _ => Text("OvertimeDaysAll", "All days")
    });

    public static IValueConverter Scale { get; } = new FuncValueConverter<int, string>(value => $"{value}%");
}
