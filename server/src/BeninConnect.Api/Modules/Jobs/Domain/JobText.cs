using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BeninConnect.Modules.Jobs.Domain;

public static partial class JobText
{
    /// <summary>Minuscules, sans accents ni ponctuation, espaces réduits.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return Spaces().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>Empreinte inter-sources : titre + entreprise + ville normalisés.</summary>
    public static string Fingerprint(string title, string? company, string? city) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Normalize(title)}|{Normalize(company)}|{Normalize(city)}")));

    /// <summary>HTML → texte brut : paragraphes et puces gardés en retours à la ligne.</summary>
    public static string PlainText(string? html, int max = 10_000)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = Blocks().Replace(html, "\n");
        text = Bullets().Replace(text, "\n• ");
        text = WebUtility.HtmlDecode(Tags().Replace(text, string.Empty));
        text = string.Join('\n', text.Split('\n').Select(l => Spaces().Replace(l, " ").Trim()));
        text = BlankLines().Replace(text, "\n\n").Replace("\n\n• ", "\n• ", StringComparison.Ordinal).Trim();
        return text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
    }

    public static string Cut(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    public static string? CutOrNull(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Cut(value, max);

    /// <summary>Premier mot-clé d'alerte trouvé dans le texte (comparaison normalisée).</summary>
    public static string? FindAlert(IEnumerable<string> keywords, params string?[] texts)
    {
        var haystack = $" {string.Join(' ', texts.Select(Normalize))} ";
        return keywords.FirstOrDefault(k => Normalize(k) is { Length: > 0 } n && haystack.Contains($" {n} ", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"<\s*(br|/p|/div|/h[1-6]|/li|/ul|/ol|/tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Blocks();

    [GeneratedRegex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Bullets();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
