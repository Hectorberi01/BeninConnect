using Avalonia;
using Avalonia.Headless;
using BeninConnect.App.Core.Services;

[assembly: AvaloniaTestApplication(typeof(BeninConnect.UiTests.TestAppBuilder))]

namespace BeninConnect.UiTests;

public static class TestAppBuilder
{
    public static AppOptions Options { get; } = new()
    {
        SimulatedLatency = TimeSpan.Zero,
        SplashDuration = TimeSpan.Zero,
        PreferencesFilePath = null
    };

    public static AppBuilder BuildAvaloniaApp()
    {
        App.App.Options = Options;
        return AppBuilder.Configure<App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
