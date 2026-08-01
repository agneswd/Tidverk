using System.Collections.Frozen;
using Tidverk.Core;

namespace Tidverk.App.Services;

/// <summary>
/// Maps domain values to resource keys. Keeping the mapping here means the domain never carries
/// display text and the view models never spell out resource keys inline.
/// </summary>
public static class ResourceKeys {
    private static readonly FrozenDictionary<string, string> HolidayKeys = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["New Year's Day"] = "HolidayNewYear",
        ["Epiphany"] = "HolidayEpiphany",
        ["Good Friday"] = "HolidayGoodFriday",
        ["Easter Sunday"] = "HolidayEasterSunday",
        ["Easter Monday"] = "HolidayEasterMonday",
        ["May Day"] = "HolidayMayDay",
        ["Ascension Day"] = "HolidayAscension",
        ["Whit Sunday"] = "HolidayWhitSunday",
        ["National Day"] = "HolidayNationalDay",
        ["Midsummer Day"] = "HolidayMidsummer",
        ["All Saints' Day"] = "HolidayAllSaints",
        ["Christmas Day"] = "HolidayChristmas",
        ["Boxing Day"] = "HolidayBoxing",
        ["Sunday"] = "HolidaySunday"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Returns null for a date with no holiday, and the untranslated name for an unknown one.</summary>
    public static string? HolidayName(ILocalizationService localization, string? invariantName) {
        ArgumentNullException.ThrowIfNull(localization);
        if (invariantName is null) {
            return null;
        }

        return HolidayKeys.TryGetValue(invariantName, out string? key) ? localization.Get(key) : invariantName;
    }

    public static string TaxUnavailable(ILocalizationService localization, TaxUnavailableReason reason) {
        ArgumentNullException.ThrowIfNull(localization);
        return localization.Get(reason switch {
            TaxUnavailableReason.ManualDeductionNotConfigured => "TaxManualNotConfigured",
            TaxUnavailableReason.TaxYearNotBundled => "TaxYearUnavailable",
            _ => "Unavailable"
        });
    }
}
