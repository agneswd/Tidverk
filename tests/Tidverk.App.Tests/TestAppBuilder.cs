using Avalonia;
using Avalonia.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Tidverk.App.Tests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tidverk.App.Tests;

public static class TestAppBuilder {
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions {
        ShouldRenderOnUIThread = true,
        UseHeadlessDrawing = false,
    });
}
