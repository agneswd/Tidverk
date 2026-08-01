namespace Tidverk.Core;

public enum ThemePreference {
    System,
    Light,
    Dark
}

public enum MonthViewPreference {
    Ledger,
    Calendar
}

public enum LanguagePreference {
    System,
    English,
    Swedish
}

public enum CurrencyPreference {
    SEK,
    EUR,
    USD,
    GBP,
    NOK,
    DKK
}

/// <summary>Explicit values: the stored ordinals predate <see cref="System"/> and must not shift.</summary>
public enum ExportLanguagePreference {
    Swedish = 0,
    English = 1,
    System = 2
}
