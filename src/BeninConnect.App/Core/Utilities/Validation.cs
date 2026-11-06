using System.Text.RegularExpressions;

namespace BeninConnect.App.Core.Utilities;

/// <summary>Règles de validation locales partagées par les formulaires.</summary>
public static partial class Validation
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public static string? Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? $"{field} est obligatoire." : null;

    public static string? Length(string? value, string field, int min, int max)
    {
        var length = value?.Trim().Length ?? 0;
        if (length == 0)
        {
            return $"{field} est obligatoire.";
        }

        if (length < min)
        {
            return $"{field} doit contenir au moins {min} caractères.";
        }

        return length > max ? $"{field} ne doit pas dépasser {max} caractères." : null;
    }

    public static string? Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "L'adresse email est obligatoire.";
        }

        return EmailRegex().IsMatch(value.Trim()) ? null : "L'adresse email n'est pas valide.";
    }

    public static string? Password(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "Le mot de passe est obligatoire.";
        }

        if (value.Length < 8)
        {
            return "Le mot de passe doit contenir au moins 8 caractères.";
        }

        if (!value.Any(char.IsDigit) || !value.Any(char.IsLetter))
        {
            return "Le mot de passe doit contenir des lettres et au moins un chiffre.";
        }

        return null;
    }
}
