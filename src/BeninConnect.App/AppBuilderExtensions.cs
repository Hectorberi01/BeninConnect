using Avalonia;

namespace BeninConnect.App;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Enregistre la police Inter embarquée. Elle est appliquée via la ressource ContentControlThemeFontFamily (App.axaml) :
    /// ne PAS remplacer la police par défaut du FontManager, iOS plante alors au démarrage.
    /// </summary>
    public static AppBuilder UseBeninConnectFonts(this AppBuilder builder) =>
        builder.WithInterFont();
}
