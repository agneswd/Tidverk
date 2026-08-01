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

/// <summary>
/// Resolves resource strings and republishes them as <c>L_</c> application resources so XAML can bind
/// them with <c>DynamicResource</c> and pick up a language change without reloading the window.
/// </summary>
public sealed class LocalizationService : ILocalizationService {
    private const string ResourcePrefix = "L_";
    private static readonly ResourceManager Resources = new("Tidverk.App.Resources.Strings", Assembly.GetExecutingAssembly());
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>
    /// Value converters are created by XAML and cannot take constructor dependencies, so they read the
    /// active service from here. Nothing else should.
    /// </summary>
    public static ILocalizationService? Current { get; private set; }

    public LocalizationService() => Current = this;

    public CultureInfo Culture { get; private set; } = English;

    /// <summary>Returns the key itself when a string is missing, which makes the gap obvious in the UI.</summary>
    public string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public string Format(string key, params object[] arguments) => string.Format(Culture, Get(key), arguments);

    public void Apply(LanguagePreference preference) {
        Culture = preference switch {
            LanguagePreference.English => English,
            LanguagePreference.Swedish => Swedish,
            _ => IsSwedishSystem() ? Swedish : English
        };
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        if (Application.Current is null) {
            return;
        }

        ResourceSet resourceSet = Resources.GetResourceSet(Culture, createIfNotExists: true, tryParents: true)
            ?? throw new InvalidOperationException($"No localization resources exist for {Culture.Name}.");
        foreach (DictionaryEntry entry in resourceSet) {
            if (entry.Key is string key && entry.Value is string value) {
                Application.Current.Resources[ResourcePrefix + key] = value;
            }
        }
    }

    private static bool IsSwedishSystem() =>
        string.Equals(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "sv", StringComparison.Ordinal);
}
