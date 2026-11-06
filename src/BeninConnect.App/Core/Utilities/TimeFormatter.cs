using System.Globalization;

namespace BeninConnect.App.Core.Utilities;

/// <summary>Formats de date en français, sans dépendre des données ICU de la plateforme.</summary>
public static class TimeFormatter
{
    private static readonly string[] ShortMonths =
        ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    private static readonly string[] LongMonths =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    private static readonly string[] Days =
        ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"];

    public static string Relative(DateTimeOffset time, DateTimeOffset now)
    {
        var delta = now - time;
        if (delta < TimeSpan.FromMinutes(1))
        {
            return "À l'instant";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"Il y a {(int)delta.TotalMinutes} min";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"Il y a {(int)delta.TotalHours} h";
        }

        if (delta < TimeSpan.FromDays(2))
        {
            return "Hier";
        }

        if (delta < TimeSpan.FromDays(7))
        {
            return $"Il y a {(int)delta.TotalDays} j";
        }

        return ShortDate(time, now);
    }

    public static string ShortDate(DateTimeOffset date, DateTimeOffset now) =>
        date.Year == now.Year
            ? $"{date.Day} {ShortMonths[date.Month - 1]}"
            : $"{date.Day} {ShortMonths[date.Month - 1]} {date.Year}";

    public static string LongDate(DateTimeOffset date) =>
        $"{Capitalize(Days[(int)date.DayOfWeek])} {date.Day} {LongMonths[date.Month - 1]} {date.Year}";

    public static string Clock(DateTimeOffset date) => date.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string MonthShort(DateTimeOffset date) => ShortMonths[date.Month - 1].TrimEnd('.').ToUpperInvariant();

    public static string MonthYear(DateTimeOffset date) => $"{Capitalize(LongMonths[date.Month - 1])} {date.Year}";

    /// <summary>Format de liste de conversations : heure, « Hier », jour de la semaine ou date.</summary>
    public static string Chat(DateTimeOffset date, DateTimeOffset now)
    {
        if (date.Date == now.Date)
        {
            return Clock(date);
        }

        if (date.Date == now.Date.AddDays(-1))
        {
            return "Hier";
        }

        if (now - date < TimeSpan.FromDays(7))
        {
            return Capitalize(Days[(int)date.DayOfWeek]);
        }

        return date.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
    }

    public static string EventSchedule(DateTimeOffset start, DateTimeOffset end) =>
        start.Date == end.Date
            ? $"{LongDate(start)} · {Clock(start)} – {Clock(end)}"
            : $"Du {start.Day} {LongMonths[start.Month - 1]} au {end.Day} {LongMonths[end.Month - 1]} · {Clock(start)}";

    public static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
