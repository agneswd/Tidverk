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

    public static IValueConverter Scale { get; } = new FuncValueConverter<int, string>(value => $"{value}%");
}
