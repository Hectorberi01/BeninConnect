using System.Globalization;
using System.Text;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Voyages.Domain;

public static class VoyageRules
{
    public const int MaxDaysAhead = 90;
    public const decimal MinCapacityKg = 0.5m;
    public const decimal MaxCapacityKg = 100m;
    public const decimal MinBookingKg = 0.5m;
    public const decimal MaxBookingKg = 50m;
    public const int MaxTripsPerDay = 5;
    public const int MaxBookingRequestsPerDay = 10;
    public const int MaxAlertsPerMember = 5;
    public const int DeliveryCodeAttempts = 5;
    public const int ReportThreshold = 3;
    public const int TrustedDeliveries = 3;
    public const int TrustedMinRating = 4;

    public static readonly string[] Currencies = ["EUR", "XOF"];

    /// <summary>Prix indicatif au kilo, au plus (1 EUR ≈ 656 XOF).</summary>
    public static decimal MaxPricePerKg(string currency) => currency == "XOF" ? 130_000m : 200m;

    public static string Country(string? value, string label)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"{label} : pays invalide (code à 2 lettres).");
        }

        return code;
    }

    public static string City(string? value, string label)
    {
        var city = (value ?? string.Empty).Trim();
        if (city.Length is < 2 or > 80)
        {
            throw new DomainException($"{label} : ville de 2 à 80 caractères.");
        }

        return city;
    }

    /// <summary>Clé de recherche : minuscules, sans accents ni espaces superflus (« Porto-Novo » = « porto novo »).</summary>
    public static string CityKey(string city)
    {
        var decomposed = city.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string? OptionalText(string? value, int max, string label)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > max)
        {
            throw new DomainException($"{label} : {max} caractères au plus.");
        }

        return text.Length == 0 ? null : text;
    }

    public static string Reason(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length is >= 3 and <= 500 ? text : throw new DomainException("Motif : 3 à 500 caractères.");
    }
}
