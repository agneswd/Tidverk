using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Tidverk.App.Tests.TestAppBuilder))]

namespace Tidverk.App.Tests;

public static class TestAppBuilder {
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions {
        ShouldRenderOnUIThread = true,
        UseHeadlessDrawing = false,
    });
}
