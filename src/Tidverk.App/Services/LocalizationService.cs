using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Avalonia;
using Tidverk.Core;

namespace Tidverk.App.Services;

public interface ILocalizationService {
    CultureInfo Culture { get; }

    string Get(string key);

    string Format(string key, params object[] arguments);

    void Apply(LanguagePreference preference);
}

public sealed class LocalizationService : ILocalizationService {
    private static readonly ResourceManager Resources = new("Tidverk.App.Resources.Strings", Assembly.GetExecutingAssembly());

    public LocalizationService() => Current = this;

    public static ILocalizationService? Current { get; private set; }

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public string Format(string key, params object[] arguments) => string.Format(Culture, Get(key), arguments);

    public void Apply(LanguagePreference preference) {
        Culture = preference switch {
            LanguagePreference.English => CultureInfo.GetCultureInfo("en"),
            LanguagePreference.Swedish => CultureInfo.GetCultureInfo("sv-SE"),
            _ when string.Equals(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "sv", StringComparison.Ordinal) => CultureInfo.GetCultureInfo("sv-SE"),
            _ => CultureInfo.GetCultureInfo("en")
        };
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        if (Application.Current is null) {
            return;
        }

        ResourceSet resourceSet = Resources.GetResourceSet(Culture, true, true)
            ?? throw new InvalidOperationException($"No localization resources exist for {Culture.Name}.");
        foreach (DictionaryEntry entry in resourceSet) {
            if (entry.Key is string key && entry.Value is string value) {
                Application.Current.Resources[$"L_{key}"] = value;
            }
        }
    }
}
