using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Notifications.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

public interface IVoyagesDb
{
    DbSet<Trip> Trips { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<TripReview> Reviews { get; }

    DbSet<RouteAlert> Alerts { get; }

    DbSet<TripReport> Reports { get; }

    DbSet<VoyagesSettings> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Section « Voyages » de la configuration.</summary>
public sealed class VoyagesOptions
{
    public const string Section = "Voyages";

    /// <summary>Départs, rappels de la veille et masquage des voyageurs suspendus (faux dans les tests).</summary>
    public bool WorkerEnabled { get; set; } = true;
}

public static class VoyagesData
{
    /// <summary>Module activé par l'équipe (absent = désactivé).</summary>
    public static async Task<bool> IsEnabledAsync(IVoyagesDb db, CancellationToken ct) =>
        await db.Settings.AsNoTracking().Where(s => s.Id == VoyagesSettings.SingletonId).Select(s => s.Enabled).FirstOrDefaultAsync(ct);

    /// <summary>Catégorie « System » : une catégorie « Voyages » désactivable demande une évolution du module Notifications (V2).</summary>
    public static Task NotifyAsync(IEventBus bus, Guid userId, string title, string body, Guid? conversationId, CancellationToken ct) =>
        bus.PublishAsync(new NotificationRequested(userId, "System", title, Text.Excerpt(body, 160),
            conversationId is null ? "None" : "Conversation", conversationId), ct);

    /// <summary>Notification qui ouvre le profil du voyageur (ses trajets y sont listés).</summary>
    public static Task NotifyProfileAsync(IEventBus bus, Guid userId, string title, string body, Guid profileId, CancellationToken ct) =>
        bus.PublishAsync(new NotificationRequested(userId, "System", title, Text.Excerpt(body, 160), "Profile", profileId), ct);

    /// <summary>« Afi Kpodo » → « Afi K. » : le nom complet et les coordonnées ne sont jamais exposés.</summary>
    public static string ShortName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Membre",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}."
        };
    }

    public static string Route(Trip t) => $"{t.FromCity} → {t.ToCity}";

    public static string Day(DateOnly d) => d.ToString("dd/MM");

    public static async Task<IReadOnlyDictionary<Guid, PersonDto>> PeopleAsync(IIdentityModule identity, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var users = await identity.GetUsersAsync(ids.Distinct(), ct);
        return users.ToDictionary(kv => kv.Key, kv => new PersonDto(kv.Key, ShortName($"{kv.Value.FirstName} {kv.Value.LastName}"),
            kv.Value.AvatarColor, kv.Value.AvatarImage));
    }

    public static PersonDto Person(IReadOnlyDictionary<Guid, PersonDto> people, Guid id) => people.GetValueOrDefault(id) ?? new PersonDto(id, "Membre", "#2E7D5B", null);

    /// <summary>Note moyenne reçue comme voyageur et badge « Voyageur de confiance ».</summary>
    public static async Task<IReadOnlyDictionary<Guid, TravelerScore>> ScoresAsync(IVoyagesDb db, IEnumerable<Guid> travelerIds, CancellationToken ct)
    {
        var ids = travelerIds.Distinct().ToList();
        var reviews = await db.Reviews.AsNoTracking().Where(r => ids.Contains(r.TargetId) && r.AboutTraveler && !r.IsHidden)
            .Select(r => new { r.TargetId, r.Rating }).ToListAsync(ct);
        var deliveries = await db.Bookings.AsNoTracking().Where(b => ids.Contains(b.TravelerId) && b.Status == BookingStatus.Delivered)
            .GroupBy(b => b.TravelerId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return ids.ToDictionary(id => id, id =>
        {
            var mine = reviews.Where(r => r.TargetId == id).Select(r => r.Rating).ToList();
            var trusted = mine.Count(r => r >= VoyageRules.TrustedMinRating) >= VoyageRules.TrustedDeliveries;
            return new TravelerScore(mine.Count == 0 ? 0 : Math.Round(mine.Average(), 1), mine.Count, deliveries.GetValueOrDefault(id), trusted);
        });
    }
}

public sealed record TravelerScore(double RatingAverage, int RatingCount, int Deliveries, bool Trusted);

/// <summary>
/// Règles affichées dans l'application et la console.
/// TODO JURIDIQUE : faire relire ces textes et la section des CGU (docs/modules/VOYAGES.md) avant l'ouverture.
/// </summary>
public static class VoyagesLegal
{
    public const string Intermediation =
        "BéninConnect met en relation des voyageurs et des expéditeurs. La plateforme n'est ni transporteur ni commissionnaire : " +
        "elle ne transporte rien et aucun paiement ne transite par l'application. Le prix est indicatif et se règle entre membres.";

    public const string TravelerResponsibility =
        "Le voyageur est personnellement responsable de ce qu'il transporte et déclare en douane. Respectez les règles douanières " +
        "des pays de départ et d'arrivée.";

    public static readonly IReadOnlyList<string> ProhibitedItems =
    [
        "Stupéfiants et substances illicites",
        "Armes, munitions, objets tranchants",
        "Argent liquide",
        "Médicaments sans ordonnance",
        "Contrefaçons",
        "Produits périssables",
        "Objets de valeur non déclarés",
        "Colis fermés dont le contenu n'a pas été vérifié"
    ];

    public static readonly IReadOnlyList<string> TravelerAdvice =
    [
        "Ouvrez et vérifiez chaque colis à la remise : n'acceptez jamais un colis fermé.",
        "Prenez une photo du contenu au moment de la remise.",
        "Refusez en cas de doute, sans avoir à vous justifier.",
        "À la livraison, demandez le code à 6 chiffres au destinataire : c'est la preuve de la remise."
    ];

    public static readonly IReadOnlyList<(string Label, string Url)> CustomsLinks =
    [
        ("Douane française : franchises des voyageurs", "https://www.douane.gouv.fr/fiche/franchises-douanieres-et-fiscales-en-quantites-et-en-valeurs"),
        ("Douanes du Bénin", "https://douanes.gouv.bj")
    ];
}
