using Avalonia.Data;
using Tidverk.App.Services;

namespace Tidverk.App.Converters;

public static class ValidationErrorConverter {
    public static Func<object, object> Instance { get; } = error => error switch {
        Exception => Text(),
        BindingNotification { Error: not null } => Text(),
        _ => error
    };

    private static string Text() => LocalizationService.Current?.Get("InvalidValue") ?? "Enter a valid value.";
}
