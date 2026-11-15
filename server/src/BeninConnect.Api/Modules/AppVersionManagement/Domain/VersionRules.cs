using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.AppVersionManagement.Domain;

public enum UpdateStatus
{
    /// <summary>Aucune version de référence pour cette plateforme.</summary>
    Unknown,
    UpToDate,
    UpdateAvailable,
    UpdateRequired
}

public static partial class VersionRules
{
    public const int MaxBuildLength = 32;
    public const int MaxOsLength = 40;
    public static readonly string[] Platforms = ["ios", "android"];

    [GeneratedRegex(@"^\d{1,4}(\.\d{1,4}){0,3}$")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^[0-9A-Za-z.\-]{1,32}$")]
    private static partial Regex BuildRegex();

    [GeneratedRegex(@"^[a-z0-9.\-]{2,60}$")]
    private static partial Regex AppCodeRegex();

    public static bool IsVersion(string? value) => value is not null && VersionRegex().IsMatch(value);

    public static string Version(string? value, string label = "Version") =>
        value?.Trim() is { } v && IsVersion(v) ? v : throw new DomainException($"{label} invalide (ex. 1.2.0).");

    public static string? OptionalVersion(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? null : Version(value, label);

    public static string Platform(string? value) =>
        value?.Trim().ToLowerInvariant() is { } p && Platforms.Contains(p) ? p : throw new DomainException("Plateforme invalide (ios ou android).");

    public static string AppCode(string? value) =>
        value?.Trim().ToLowerInvariant() is { } c && AppCodeRegex().IsMatch(c) ? c : throw new DomainException("Code d'application invalide.");

    public static string Build(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "0" : BuildRegex().IsMatch(value.Trim()) ? value.Trim() : throw new DomainException("Numéro de build invalide.");

    public static string? Text(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];

    /// <summary>Comparaison numérique : 1.10.0 &gt; 1.9.3 ; les parties absentes valent 0 (1.2 = 1.2.0).</summary>
    public static int Compare(string a, string b)
    {
        var x = a.Split('.');
        var y = b.Split('.');
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var diff = (i < x.Length ? int.Parse(x[i]) : 0).CompareTo(i < y.Length ? int.Parse(y[i]) : 0);
            if (diff != 0)
            {
                return diff;
            }
        }

        return 0;
    }

    public static UpdateStatus StatusOf(string version, string? latest, string? minimum)
    {
        if (!IsVersion(version) || (latest is null && minimum is null))
        {
            return UpdateStatus.Unknown;
        }

        if (minimum is not null && Compare(version, minimum) < 0)
        {
            return UpdateStatus.UpdateRequired;
        }

        return latest is not null && Compare(version, latest) < 0 ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
    }

    public static bool IsOutdated(this UpdateStatus status) => status is UpdateStatus.UpdateAvailable or UpdateStatus.UpdateRequired;
}
