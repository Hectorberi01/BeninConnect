using System.Text;
using BeninConnect.Modules.Notifications.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

public interface IServicesDb
{
    DbSet<ServiceCategory> Categories { get; }

    DbSet<ProviderProfile> Providers { get; }

    DbSet<ProviderDocument> Documents { get; }

    DbSet<ServiceOffering> Offerings { get; }

    DbSet<ServiceRequest> Requests { get; }

    DbSet<Review> Reviews { get; }

    DbSet<ServiceReport> Reports { get; }

    DbSet<ProviderFavorite> Favorites { get; }

    DbSet<ServicesSettings> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Section « Services » : réglages techniques (catégories, mots-clés et seuils se règlent dans la console).</summary>
public sealed class ServicesOptions
{
    public const string Section = "Services";

    /// <summary>Rappels d'avis automatiques (faux dans les tests).</summary>
    public bool WorkerEnabled { get; set; } = true;

    public int ReviewReminderHours { get; set; } = 24;

    /// <summary>Prestataires examinés au plus par recherche (tri et distance calculés ensuite).</summary>
    public int SearchCandidates { get; set; } = 1000;
}

/// <summary>Accès communs : catalogue créé au besoin, réglages, notifications.</summary>
public static class ServicesData
{
    public static async Task EnsureCatalogAsync(IServicesDb db, CancellationToken ct)
    {
        var changed = false;
        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(ServiceCatalog.Create());
            changed = true;
        }

        if (!await db.Settings.AnyAsync(ct))
        {
            db.Settings.Add(ServicesSettings.CreateDefault());
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Créés en même temps par une autre requête
            foreach (var entry in ((DbContext)db).ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    public static async Task<ServicesSettings> SettingsAsync(IServicesDb db, CancellationToken ct)
    {
        await EnsureCatalogAsync(db, ct);
        return await db.Settings.AsNoTracking().FirstAsync(ct);
    }

    /// <summary>Catégorie et ses sous-catégories.</summary>
    public static async Task<List<Guid>> WithChildrenAsync(IServicesDb db, Guid categoryId, CancellationToken ct) =>
        await db.Categories.Where(c => c.Id == categoryId || c.ParentId == categoryId).Select(c => c.Id).ToListAsync(ct);

    public static Task NotifyAsync(IEventBus bus, Guid userId, string title, string body, Guid? conversationId, CancellationToken ct) =>
        bus.PublishAsync(new NotificationRequested(userId, "System", title, Text.Excerpt(body, 160),
            conversationId is null ? "None" : "Conversation", conversationId), ct);

    /// <summary>Efface le prestataire et tout ce qui s'y rattache. Renvoie les images et justificatifs à libérer.</summary>
    public static async Task<(List<string> Images, List<string> Files)> DeleteProviderAsync(IServicesDb db, ProviderProfile profile, CancellationToken ct)
    {
        var offerings = await db.Offerings.Where(o => o.ProviderId == profile.Id).Select(o => new { o.Id, o.Images }).ToListAsync(ct);
        var images = offerings.SelectMany(o => o.Images).ToList();
        if (profile.PhotoPath is { } photo)
        {
            images.Add(photo);
        }

        var files = await db.Documents.Where(d => d.ProviderId == profile.Id).Select(d => d.FileName).ToListAsync(ct);
        var offeringIds = offerings.Select(o => o.Id).ToList();
        var reviewIds = await db.Reviews.Where(r => r.ProviderId == profile.Id).Select(r => r.Id).ToListAsync(ct);
        await db.Reports.Where(r => (r.TargetType == ReportTarget.Profile && r.TargetId == profile.Id)
            || (r.TargetType == ReportTarget.Offering && offeringIds.Contains(r.TargetId))
            || (r.TargetType == ReportTarget.Review && reviewIds.Contains(r.TargetId))).ExecuteDeleteAsync(ct);
        await db.Documents.Where(d => d.ProviderId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Reviews.Where(r => r.ProviderId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Requests.Where(r => r.ProviderId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Favorites.Where(f => f.ProviderId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Offerings.Where(o => o.ProviderId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Providers.Where(p => p.Id == profile.Id).ExecuteDeleteAsync(ct);
        return (images, files);
    }

    public static string EscapeLike(string q) => $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    /// <summary>Taux de réponse et délai médian, recalculés à chaque réponse du prestataire.</summary>
    public static async Task RefreshResponseStatsAsync(IServicesDb db, ProviderProfile provider, CancellationToken ct)
    {
        var requests = await db.Requests.AsNoTracking().Where(r => r.ProviderId == provider.Id)
            .Where(r => r.RespondedAt != null || r.Status != RequestStatus.Cancelled)
            .Select(r => new { r.CreatedAt, r.RespondedAt }).ToListAsync(ct);
        var minutes = requests.Where(r => r.RespondedAt != null).Select(r => (int)(r.RespondedAt!.Value - r.CreatedAt).TotalMinutes).Order().ToList();
        var median = minutes.Count == 0 ? (int?)null : minutes[minutes.Count / 2];
        provider.SetResponseStats(requests.Count == 0 ? 0 : (double)minutes.Count / requests.Count, median);
    }

    /// <summary>Moyenne et nombre d'avis publiés.</summary>
    public static async Task RefreshRatingAsync(IServicesDb db, Guid providerId, CancellationToken ct)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct);
        if (provider is null)
        {
            return;
        }

        var ratings = await db.Reviews.Where(r => r.ProviderId == providerId && r.Status == ReviewStatus.Published).Select(r => r.Rating).ToListAsync(ct);
        provider.SetRating(ratings.Count == 0 ? 0 : ratings.Average(), ratings.Count);
    }
}

/// <summary>Curseur opaque de pagination : position dans la liste triée.</summary>
public static class OffsetCursor
{
    public static string Encode(int offset) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"o:{offset}")).TrimEnd('=');

    public static int Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var b64 = cursor.PadRight(cursor.Length + (4 - cursor.Length % 4) % 4, '=');
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            return text.StartsWith("o:", StringComparison.Ordinal) && int.TryParse(text[2..], out var offset) && offset >= 0
                ? offset
                : throw new FormatException();
        }
        catch (FormatException)
        {
            throw new DomainException("Curseur invalide.");
        }
    }
}

/// <summary>
/// Textes d'information affichés par l'application et la console (mise en relation, avis, obligations des prestataires).
/// TODO JURIDIQUE : faire valider ces textes et les CGU du module par un juriste avant l'ouverture.
/// </summary>
public static class ServicesLegal
{
    public const string Intermediation =
        "BéninConnect met en relation des membres. La plateforme n'est pas partie au contrat de prestation : le prix, la date et le paiement " +
        "se règlent directement entre le client et le prestataire. Aucun paiement ne transite par l'application.";

    public const string Qualifications =
        "BéninConnect ne vérifie pas les qualifications des particuliers. Le badge « Pro vérifié » indique seulement qu'un justificatif " +
        "et un numéro d'entreprise (SIRET ou IFU) ont été contrôlés par l'équipe.";

    public const string Reviews =
        "Seuls les membres ayant réalisé une prestation via BéninConnect peuvent laisser un avis (« Avis vérifié »). Chaque avis est daté. " +
        "Un avis n'est jamais retiré parce qu'il est négatif : seule une décision de modération motivée peut le masquer.";

    // TODO JURIDIQUE : obligations DAC7 (déclaration des revenus des vendeurs) à étudier si un paiement passe un jour par la plateforme.
    public const string TaxNotice =
        "Les revenus tirés de vos prestations doivent être déclarés. Renseignez-vous sur vos obligations fiscales et sociales.";

    public static readonly IReadOnlyList<(string Label, string Url)> TaxLinks =
    [
        ("impots.gouv.fr", "https://www.impots.gouv.fr/particulier/questions/je-realise-des-revenus-grace-des-plateformes-en-ligne-que-dois-je-declarer"),
        ("urssaf.fr", "https://www.urssaf.fr/accueil/particulier/beneficiaire-economie-collaborative.html")
    ];
}
