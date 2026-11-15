namespace BeninConnect.BackgroundJobs.Scheduling;

/// <summary>
/// Définition d'une tâche planifiée (un fichier par tâche dans Jobs/). Key est la clé de l'IScheduledTask
/// enregistrée par le module concerné : la logique reste dans le module, l'horaire est décidé ici.
/// </summary>
public interface IBackgroundJob
{
    string Key { get; }

    string Name { get; }

    /// <summary>Module qui exécute la tâche (affiché dans la console).</summary>
    string Module { get; }

    string Description { get; }

    JobSchedule Schedule { get; }
}

/// <summary>Toutes les N minutes (après un délai au démarrage), chaque jour ou chaque semaine à une heure UTC.</summary>
public sealed record JobSchedule
{
    private JobSchedule()
    {
    }

    public TimeSpan? Interval { get; private init; }
    public TimeSpan InitialDelay { get; private init; }
    public TimeOnly? AtUtc { get; private init; }
    public DayOfWeek? Day { get; private init; }

    public static JobSchedule Every(TimeSpan interval, TimeSpan? initialDelay = null) =>
        new() { Interval = interval < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : interval, InitialDelay = initialDelay ?? TimeSpan.FromMinutes(1) };

    public static JobSchedule DailyAt(TimeOnly atUtc) => new() { AtUtc = atUtc };

    public static JobSchedule WeeklyAt(DayOfWeek day, TimeOnly atUtc) => new() { AtUtc = atUtc, Day = day };

    /// <summary>Première exécution après le démarrage (une heure quotidienne déjà passée n'est pas rattrapée).</summary>
    public DateTimeOffset First(DateTimeOffset start) => Interval is not null ? start + InitialDelay : Next(start);

    /// <summary>Exécution suivante, strictement après « after ».</summary>
    public DateTimeOffset Next(DateTimeOffset after)
    {
        if (Interval is { } interval)
        {
            return after + interval;
        }

        var utc = after.ToUniversalTime();
        var next = new DateTimeOffset(DateOnly.FromDateTime(utc.UtcDateTime), AtUtc!.Value, TimeSpan.Zero);
        while (next <= utc || (Day is { } day && next.DayOfWeek != day))
        {
            next = next.AddDays(1);
        }

        return next;
    }

    public string Describe()
    {
        if (Interval is { } interval)
        {
            return interval.TotalMinutes switch
            {
                1 => "Chaque minute",
                < 60 => $"Toutes les {interval.TotalMinutes:0} minutes",
                60 => "Toutes les heures",
                var m when m % 60 == 0 => $"Toutes les {m / 60:0} heures",
                var m => $"Toutes les {m:0} minutes",
            };
        }

        var at = $"{AtUtc!.Value:HH\\:mm} UTC";
        return Day is { } day ? $"Chaque {DayNames[(int)day]} à {at}" : $"Chaque jour à {at}";
    }

    private static readonly string[] DayNames = ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"];
}
