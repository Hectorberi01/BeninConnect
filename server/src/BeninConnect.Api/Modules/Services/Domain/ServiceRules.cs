using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BeninConnect.Modules.Services.Domain;

public static partial class ServiceRules
{
    public const int MaxImages = 8;
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const int ReviewEditDays = 7;

    // Note bayésienne : 5 avis fictifs à la moyenne a priori (un 5/5 sur 1 avis ne passe pas devant 4,8/5 sur 40)
    public const double PriorWeight = 5;
    public const double PriorMean = 4.0;

    private static readonly HashSet<string> XofCountries = ["BJ", "TG", "SN", "CI", "BF", "ML", "NE", "GW"];

    /// <summary>Franc CFA (UEMOA) pour l'Afrique de l'Ouest, euro sinon.</summary>
    public static string CurrencyFor(string country) => XofCountries.Contains(country.ToUpperInvariant()) ? "XOF" : "EUR";

    public static double Bayesian(double average, int count) =>
        count <= 0 ? PriorMean : (PriorWeight * PriorMean + average * count) / (PriorWeight + count);

    /// <summary>Distance en km entre deux points (formule de Haversine).</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Rectangle autour d'un point : pré-filtre en base avant le calcul exact.</summary>
    public static (double MinLat, double MaxLat, double MinLng, double MaxLng) BoundingBox(double lat, double lng, double radiusKm)
    {
        var dLat = radiusKm / 111.0;
        var dLng = radiusKm / (111.0 * Math.Max(0.01, Math.Cos(lat * Math.PI / 180)));
        return (lat - dLat, lat + dLat, lng - dLng, lng + dLng);
    }

    /// <summary>Score par défaut : note bayésienne 60 %, proximité 25 %, taux de réponse 15 %.</summary>
    public static double Score(double ratingAverage, int ratingCount, double? distanceKm, double radiusKm, double responseRate)
    {
        var rating = Bayesian(ratingAverage, ratingCount) / 5;
        var proximity = distanceKm is { } d ? 1 - Math.Min(1, d / Math.Max(1, radiusKm)) : 0.5;
        return 0.6 * rating + 0.25 * proximity + 0.15 * Math.Clamp(responseRate, 0, 1);
    }

    /// <summary>Minuscules, sans accents ni ponctuation.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
        }

        return Spaces().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>Premier mot-clé d'alerte trouvé (mots entiers, sans tenir compte des accents).</summary>
    public static string? FindAlert(IEnumerable<string> keywords, params string?[] texts)
    {
        var haystack = $" {string.Join(' ', texts.Select(Normalize))} ";
        return keywords.FirstOrDefault(k => Normalize(k) is { Length: > 0 } n && haystack.Contains($" {n} ", StringComparison.Ordinal));
    }

    public static string Clean(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    public static string? CleanOrNull(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Clean(value, max);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
