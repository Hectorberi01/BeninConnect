using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using BeninConnect.App;

namespace BeninConnect.Android;

[Activity(
    Label = "BéninConnect",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseBeninConnectFonts();
}
