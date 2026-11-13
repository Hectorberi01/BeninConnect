using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Services.Domain;

/// <summary>Catégorie de service (référentiel géré dans la console). Deux niveaux : catégorie et sous-catégorie.</summary>
public sealed partial class ServiceCategory
{
    private ServiceCategory()
    {
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    /// <summary>Nom d'icône MaterialCommunityIcons (application et console).</summary>
    public string Icon { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Métier réglementé : déclaration sur l'honneur demandée, justificatif pour le badge « Pro vérifié ».</summary>
    public bool RequiresQualification { get; private set; }

    public static ServiceCategory Create(string slug, string name, string icon, Guid? parentId, int sortOrder, bool requiresQualification, Guid? id = null)
    {
        var category = new ServiceCategory { Id = id ?? Guid.CreateVersion7(), IsActive = true };
        category.Update(slug, name, icon, parentId, sortOrder, true, requiresQualification);
        return category;
    }

    public void Update(string slug, string name, string icon, Guid? parentId, int sortOrder, bool isActive, bool requiresQualification)
    {
        var s = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(s))
        {
            throw new DomainException("Identifiant : 2 à 50 caractères, lettres minuscules, chiffres et tirets.");
        }

        var n = (name ?? string.Empty).Trim();
        if (n.Length is < 2 or > 60)
        {
            throw new DomainException("Nom de la catégorie : 2 à 60 caractères.");
        }

        if (parentId == Id)
        {
            throw new DomainException("Une catégorie ne peut pas être sa propre catégorie parente.");
        }

        Slug = s;
        Name = n;
        Icon = string.IsNullOrWhiteSpace(icon) ? "briefcase-outline" : ServiceRules.Clean(icon, 60);
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = isActive;
        RequiresQualification = requiresQualification;
    }

    /// <summary>Identifiant stable des catégories d'origine (même valeur sur tous les serveurs).</summary>
    public static Guid SeedId(string slug) => new(MD5.HashData(Encoding.UTF8.GetBytes($"beninconnect/services/{slug}")));

    [GeneratedRegex("^[a-z0-9-]{2,50}$")]
    private static partial Regex SlugPattern();
}

/// <summary>Catégories d'origine, créées si le référentiel est vide.</summary>
public static class ServiceCatalog
{
    private sealed record Seed(string Slug, string Name, string Icon, bool Qualified = false);

    private static readonly (Seed Parent, Seed[] Children)[] Tree =
    [
        (new("beaute", "Beauté", "face-woman-shimmer"),
        [
            new("coiffure", "Coiffure", "hair-dryer", true), new("tresses-locks", "Tresses et locks", "spa"),
            new("manucure", "Manucure et ongles", "hand-back-left"), new("maquillage", "Maquillage", "lipstick"),
            new("esthetique", "Soins esthétiques", "face-woman", true)
        ]),
        (new("maison", "Maison", "home-outline"),
        [
            new("menage", "Ménage", "broom"), new("peinture", "Peinture", "format-paint"), new("plomberie", "Plomberie", "pipe-wrench"),
            new("electricite", "Électricité", "flash", true), new("bricolage", "Bricolage et montage", "hammer-screwdriver"),
            new("jardinage", "Jardinage", "flower-outline")
        ]),
        (new("transport", "Transport", "truck-outline"),
        [
            new("demenagement", "Déménagement", "truck-delivery", true), new("livraison", "Livraison et courses", "package-variant"),
            new("chauffeur", "Chauffeur", "car")
        ]),
        (new("cours", "Cours", "school-outline"),
        [
            new("soutien-scolaire", "Soutien scolaire", "book-open-variant"), new("langues", "Langues", "translate"),
            new("musique", "Musique", "music")
        ]),
        (new("evenements", "Événements", "party-popper"),
        [
            new("traiteur", "Traiteur", "silverware-fork-knife"), new("patisserie", "Pâtisserie", "cake-variant"),
            new("photographie", "Photographie et vidéo", "camera"), new("animation", "DJ et animation", "music-circle"),
            new("decoration", "Décoration", "balloon")
        ]),
        (new("services-pratiques", "Services pratiques", "toolbox-outline"),
        [
            new("traduction", "Traduction", "translate-variant"), new("couture", "Couture et retouches", "needle"),
            new("informatique", "Informatique et dépannage", "laptop"), new("demarches", "Démarches administratives", "file-document-outline")
        ]),
        (new("aide-personne", "Aide à la personne", "hand-heart-outline"),
        [
            new("garde-enfants", "Garde d'enfants", "baby-face-outline", true), new("aide-domicile", "Aide aux seniors", "human-cane")
        ])
    ];

    public static IEnumerable<ServiceCategory> Create()
    {
        var order = 0;
        foreach (var (parent, children) in Tree)
        {
            var p = ServiceCategory.Create(parent.Slug, parent.Name, parent.Icon, null, order++ * 100, parent.Qualified, ServiceCategory.SeedId(parent.Slug));
            yield return p;
            var child = 0;
            foreach (var c in children)
            {
                yield return ServiceCategory.Create(c.Slug, c.Name, c.Icon, p.Id, p.SortOrder + ++child, c.Qualified, ServiceCategory.SeedId(c.Slug));
            }
        }
    }
}
