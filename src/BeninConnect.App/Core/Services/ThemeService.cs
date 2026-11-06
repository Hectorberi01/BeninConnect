using Avalonia;
using Avalonia.Styling;

namespace BeninConnect.App.Core.Services;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public interface IThemeService
{
    AppTheme Current { get; }

    void Initialize();

    void Apply(AppTheme theme);
}

public sealed class ThemeService(IPreferencesService preferences, AppOptions options) : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Light;

    public void Initialize()
    {
        var stored = preferences.Get(PreferenceKeys.Theme);
        Current = Enum.TryParse<AppTheme>(stored, out var theme) ? theme : AppTheme.Light;
        ApplyToApplication();
    }

    public void Apply(AppTheme theme)
    {
        Current = theme;
        preferences.Set(PreferenceKeys.Theme, theme.ToString());
        ApplyToApplication();
    }

    private void ApplyToApplication()
    {
        if (!options.ApplyThemeToApplication || Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = Current switch
        {
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
}
