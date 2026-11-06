using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace BeninConnect.App.Core.Converters;

public static class AppConverters
{
    /// <summary>"Posts/post_01.jpg" → Bitmap chargé depuis les assets embarqués (hors ligne).</summary>
    public static readonly IValueConverter AssetBitmap = new AssetBitmapConverter();

    /// <summary>"#087F5B" → SolidColorBrush.</summary>
    public static readonly IValueConverter HexToBrush =
        new FuncValueConverter<string?, IBrush?>(hex =>
            hex is not null && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : null);

    /// <summary>Nombre → texte compact (1 200 → « 1,2 k »).</summary>
    public static readonly IValueConverter CompactNumber =
        new FuncValueConverter<int, string>(n => n switch
        {
            >= 1_000_000 => (n / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " M",
            >= 1_000 => (n / 1_000d).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " k",
            _ => n.ToString(CultureInfo.InvariantCulture)
        });
}

internal sealed class AssetBitmapConverter : IValueConverter
{
    private const string Root = "avares://BeninConnect.App/Assets/Images/";
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Cache.GetOrAdd(path, static p =>
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(Root + p));
                return Bitmap.DecodeToWidth(stream, 900);
            }
            catch (Exception)
            {
                return null;
            }
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
