using System.Text.RegularExpressions;

namespace BeninConnect.Shared.Application;

/// <summary>Règles de validation partagées (mêmes messages que l'application mobile).</summary>
public static partial class Rules
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    /// <summary>Image embarquée dans l'app (Posts/…, Covers/…) ou fichier envoyé (uploads/…).</summary>
    public static IEnumerable<(string, string)> ImagePath(string field, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !ImagePathRegex().IsMatch(value))
        {
            yield return (field, "Image invalide.");
        }
    }

    public static IEnumerable<(string, string)> VideoPath(string field, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !VideoPathRegex().IsMatch(value))
        {
            yield return (field, "Vidéo invalide.");
        }
    }

    [GeneratedRegex(@"^uploads/[a-f0-9]{32}\.(mp4|mov)$")]
    private static partial Regex VideoPathRegex();

    [GeneratedRegex(@"^((Posts|Covers|Events|Profile)/[A-Za-z0-9_-]+\.(jpg|jpeg|png)|uploads/[a-f0-9]{32}\.(jpg|png|webp|heic))$")]
    private static partial Regex ImagePathRegex();

    public static IEnumerable<(string, string)> Required(string field, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return (field, $"{label} est obligatoire.");
        }
    }

    public static IEnumerable<(string, string)> Length(string field, string? value, string label, int min, int max)
    {
        var length = value?.Trim().Length ?? 0;
        if (length == 0)
        {
            yield return (field, $"{label} est obligatoire.");
        }
        else if (length < min)
        {
            yield return (field, $"{label} doit contenir au moins {min} caractères.");
        }
        else if (length > max)
        {
            yield return (field, $"{label} ne doit pas dépasser {max} caractères.");
        }
    }

    public static IEnumerable<(string, string)> Email(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex().IsMatch(value.Trim()))
        {
            yield return (field, "L'adresse email n'est pas valide.");
        }
    }

    public static IEnumerable<(string, string)> Password(string field, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8)
        {
            yield return (field, "Le mot de passe doit contenir au moins 8 caractères.");
        }
        else if (!value.Any(char.IsDigit) || !value.Any(char.IsLetter))
        {
            yield return (field, "Le mot de passe doit contenir des lettres et au moins un chiffre.");
        }
    }
}
