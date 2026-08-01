using Avalonia;
using Avalonia.Styling;
using Tidverk.Core;

namespace Tidverk.App.Services;

public interface IThemeService {
    void Apply(ThemePreference preference);
}

public sealed class ThemeService : IThemeService {
    public void Apply(ThemePreference preference) {
        if (Application.Current is null) {
            return;
        }

        Application.Current.RequestedThemeVariant = preference switch {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
