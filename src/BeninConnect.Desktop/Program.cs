using Avalonia;
using BeninConnect.App;

namespace BeninConnect.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UsePlatformDetect()
            .UseBeninConnectFonts()
            .LogToTrace();
}
