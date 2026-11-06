using Avalonia;
using Avalonia.iOS;
using BeninConnect.App;
using Foundation;

namespace BeninConnect.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711
public partial class AppDelegate : AvaloniaAppDelegate<App.App>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseBeninConnectFonts();
}
