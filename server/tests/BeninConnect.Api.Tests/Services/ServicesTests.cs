using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests.Services;

/// <summary>Base dédiée : les réglages modifiés ici ne touchent pas les autres tests.</summary>
public sealed class ServicesFactory : ApiFactory;

public sealed class ServicesTests(ServicesFactory factory) : IClassFixture<ServicesFactory>
{
    private sealed record TokenBody(string Token);

    private sealed record Category(Guid Id, string Slug, string Name, int ActiveProviders, List<Category> Children);

    private sealed record Offering(Guid Id, string Title, string HiddenReason, string? AlertKeyword);

    private sealed record Mine(Guid Id, string Status, string? ReviewNote, List<Offering> Offerings, string VerificationLevel);

    private sealed record Card(Guid Id, string DisplayName, double? DistanceKm, decimal? PriceFrom, string Currency, double RatingAverage, int RatingCount);

    private sealed record CardPage(List<Card> Items, string? NextCursor, int Total);

    private sealed record Detail(Guid Id, string DisplayName, string? Phone, double RatingAverage, int RatingCount, List<Offering> Offerings);

    private sealed record Request(Guid Id, string Status, Guid? ConversationId, string? ProviderPhone, bool CanReview, Guid? ReviewId);

    private sealed record ReviewView(Guid Id, int Rating, string AuthorName, string? ProviderReply);

    private sealed record Page<T>(List<T> Items, int TotalCount);

    private sealed record AdminProvider(Guid Id, string DisplayName, string Status, string VerificationLevel);

    private sealed record AdminOffering(Offering Offering, string ProviderName);

    private sealed record AdminDocument(JsonElement Document, Guid ProviderId, string VerificationLevel);

    private async Task<HttpClient> AdminAsync(string email = "superadmin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<HttpClient> MemberAsync(string first = "Koffi")
    {
        var http = factory.CreateClient();
        var email = $"services.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = first, lastName = "Services", email, country = "France", city = "Paris", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private static async Task<T> Ok<T>(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<Guid> CategoryAsync(HttpClient http, string slug)
    {
        var tree = await http.GetFromJsonAsync<List<Category>>("api/services/categories");
        return tree!.SelectMany(c => c.Children.Prepend(c)).Single(c => c.Slug == slug).Id;
    }

    private static object Profile(string name, double lat, double lng, string city, string? phone = "+33 6 11 22 33 44", string? siret = null) => new
    {
        profile = new
        {
            displayName = name, bio = "Professionnelle depuis 10 ans.", phone, phoneIsPublic = false, contactPreference = "InApp",
            legalStatus = siret is null ? "Individual" : "MicroEntrepreneur", siret, country = "FR", city, postalCode = "75011", latitude = lat,
            longitude = lng, serviceRadiusKm = 20, servesAtHome = true, hasPremises = false
        },
        qualificationDeclared = true
    };

    /// <summary>Prestataire publié : profil, une offre, envoi en revue, validation par l'équipe.</summary>
    private async Task<(HttpClient Http, Guid ProviderId, Guid OfferingId)> ProviderAsync(string name, double lat, double lng, string city, string slug = "tresses-locks",
        decimal price = 60, bool approve = true)
    {
        var http = await MemberAsync();
        var category = await CategoryAsync(http, slug);
        await Ok<Mine>(http.PutAsJsonAsync("api/services/me", Profile(name, lat, lng, city)));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("api/services/me/submit", null)).StatusCode); // aucune offre
        var offering = await Ok<Offering>(http.PostAsJsonAsync("api/services/me/offerings",
            new { categoryId = category, title = $"Prestation {name}", description = "Sur rendez-vous.", priceType = "From", priceAmount = price,
                durationMinutes = 120, images = Array.Empty<string>(), isActive = true }));
        var mine = await Ok<Mine>(http.PostAsync("api/services/me/submit", null));
        Assert.Equal("PendingReview", mine.Status);
        if (approve)
        {
            await Ok<JsonElement>((await AdminAsync()).PostAsync($"api/admin/services/providers/{mine.Id}/approve", null));
        }

        return (http, mine.Id, offering.Id);
    }

    [Fact]
    public async Task Geographic_search_filters_by_radius_and_sorts_by_distance()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var near = await ProviderAsync($"Paris {tag}", 48.8606, 2.3376, "Paris");
        var farther = await ProviderAsync($"Vincennes {tag}", 48.8475, 2.4390, "Vincennes");
        var lyon = await ProviderAsync($"Lyon {tag}", 45.7640, 4.8357, "Lyon");
        var draft = await ProviderAsync($"Brouillon {tag}", 48.8606, 2.3376, "Paris", approve: false);
        var client = await MemberAsync();

        var page = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&lat=48.8566&lng=2.3522&radiusKm=30&sort=Distance");
        Assert.Equal([near.ProviderId, farther.ProviderId], page!.Items.Select(i => i.Id));
        Assert.All(page.Items, i => Assert.NotNull(i.DistanceKm));
        Assert.Equal(60, page.Items[0].PriceFrom);

        var everywhere = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}");
        Assert.Equal(3, everywhere!.Total);
        Assert.DoesNotContain(everywhere.Items, i => i.Id == draft.ProviderId); // en attente : invisible
        var inLyon = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&city=lyon");
        Assert.Equal([lyon.ProviderId], inLyon!.Items.Select(i => i.Id));

        // Catégorie parente « Beauté » : comprend « Tresses et locks »
        var beauty = await CategoryAsync(client, "beaute");
        var byParent = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&categoryId={beauty}");
        Assert.Equal(3, byParent!.Total);
        var cheap = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&priceMax=50");
        Assert.Equal(0, cheap!.Total);

        // Pagination par curseur
        var first = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&take=2");
        var second = await client.GetFromJsonAsync<CardPage>($"api/services/search?q={tag}&take=2&cursor={first!.NextCursor}");
        Assert.Single(second!.Items);
        Assert.Null(second.NextCursor);

        // Coordonnées exactes jamais renvoyées
        var json = await client.GetStringAsync($"api/services/providers/{near.ProviderId}");
        Assert.DoesNotContain("48.8606", json);
        Assert.DoesNotContain("latitude", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/services/providers/{draft.ProviderId}")).StatusCode);
    }

    [Fact]
    public async Task Request_accept_complete_and_verified_review()
    {
        var provider = await ProviderAsync("Afi Tresses", 48.85, 2.35, "Paris");
        var client = await MemberAsync("Sena");

        var detail = await client.GetFromJsonAsync<Detail>($"api/services/providers/{provider.ProviderId}");
        Assert.Null(detail!.Phone); // numéro caché avant acceptation
        Assert.Equal(HttpStatusCode.BadRequest,
            (await provider.Http.PostAsJsonAsync("api/services/requests", new { offeringId = provider.OfferingId, message = "Pour moi-même, test" })).StatusCode);

        var request = await Ok<Request>(client.PostAsJsonAsync("api/services/requests",
            new { offeringId = provider.OfferingId, message = "Bonjour, êtes-vous disponible samedi matin ?", city = "Paris" }));
        Assert.Equal("Sent", request.Status);
        Assert.NotNull(request.ConversationId);
        var conversations = await client.GetFromJsonAsync<List<JsonElement>>("api/conversations");
        Assert.Contains(conversations!, c => c.GetProperty("conversation").GetProperty("id").GetGuid() == request.ConversationId);
        var duplicate = await client.PostAsJsonAsync("api/services/requests", new { offeringId = provider.OfferingId, message = "Encore une demande identique" });
        Assert.True(duplicate.StatusCode == HttpStatusCode.Conflict, await duplicate.Content.ReadAsStringAsync());

        var received = await provider.Http.GetFromJsonAsync<List<Request>>("api/services/requests?role=Provider");
        Assert.Contains(received!, r => r.Id == request.Id);
        Assert.Equal("Viewed", (await provider.Http.GetFromJsonAsync<Request>($"api/services/requests/{request.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"api/services/requests/{request.Id}/review", new { rating = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"api/services/requests/{request.Id}/accept", null)).StatusCode);

        var accepted = await Ok<Request>(provider.Http.PostAsync($"api/services/requests/{request.Id}/accept", null));
        Assert.Equal("Accepted", accepted.Status);
        Assert.Equal("+33 6 11 22 33 44", (await client.GetFromJsonAsync<Detail>($"api/services/providers/{provider.ProviderId}"))!.Phone);

        var completed = await Ok<Request>(client.PostAsync($"api/services/requests/{request.Id}/complete", null));
        Assert.True(completed.CanReview);
        var review = await Ok<ReviewView>(client.PostAsJsonAsync($"api/services/requests/{request.Id}/review", new { rating = 5, comment = "Travail soigné." }));
        Assert.Equal("Sena S.", review.AuthorName);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"api/services/requests/{request.Id}/review", new { rating = 4 })).StatusCode);
        await Ok<ReviewView>(provider.Http.PostAsJsonAsync($"api/services/reviews/{review.Id}/reply", new { reply = "Merci beaucoup !" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.Http.PostAsJsonAsync($"api/services/reviews/{review.Id}/reply", new { reply = "Encore" })).StatusCode);

        detail = await client.GetFromJsonAsync<Detail>($"api/services/providers/{provider.ProviderId}");
        Assert.Equal(5, detail!.RatingAverage);
        Assert.Equal(1, detail.RatingCount);

        // Masquer un avis : motif documenté obligatoire, la note est recalculée
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"api/admin/services/reviews/{review.Id}/hide", new { reason = "non" })).StatusCode);
        await Ok<JsonElement>(admin.PostAsJsonAsync($"api/admin/services/reviews/{review.Id}/hide", new { reason = "Contenu sans rapport avec la prestation." }));
        Assert.Equal(0, (await client.GetFromJsonAsync<Detail>($"api/services/providers/{provider.ProviderId}"))!.RatingCount);
    }

    [Fact]
    public async Task Moderation_alert_keywords_reports_documents_and_daily_limit()
    {
        var admin = await AdminAsync();
        var provider = await ProviderAsync("Ménage Express", 48.85, 2.35, "Paris", "menage", 15);

        // Mot-clé d'alerte : offre masquée et placée dans la file de la console
        var category = await CategoryAsync(provider.Http, "menage");
        var flagged = await Ok<Offering>(provider.Http.PostAsJsonAsync("api/services/me/offerings",
            new { categoryId = category, title = "Grand ménage", description = "Paiement d'avance par Western Union uniquement.", priceType = "Fixed",
                priceAmount = 80, images = Array.Empty<string>(), isActive = true }));
        Assert.Equal("AlertKeyword", flagged.HiddenReason);
        var queue = await admin.GetFromJsonAsync<Page<AdminOffering>>("api/admin/services/offerings?view=Review");
        Assert.Contains(queue!.Items, o => o.Offering.Id == flagged.Id);
        await Ok<JsonElement>(admin.PostAsync($"api/admin/services/offerings/{flagged.Id}/show", null));

        // Refus d'un profil : retour en brouillon avec le motif
        var other = await ProviderAsync("Profil à revoir", 48.85, 2.35, "Paris", approve: false);
        await Ok<JsonElement>(admin.PostAsJsonAsync($"api/admin/services/providers/{other.ProviderId}/reject", new { reason = "Ajoutez une photo de vos réalisations." }));
        var mine = await other.Http.GetFromJsonAsync<JsonElement>("api/services/me");
        Assert.Equal("Draft", mine.GetProperty("profile").GetProperty("status").GetString());
        Assert.Equal("Ajoutez une photo de vos réalisations.", mine.GetProperty("profile").GetProperty("reviewNote").GetString());

        // 3 signalements : offre masquée en attendant la revue
        for (var i = 0; i < 3; i++)
        {
            var reporter = await MemberAsync();
            (await reporter.PostAsJsonAsync("api/services/reports", new { targetType = "Offering", targetId = provider.OfferingId, reason = "Annonce trompeuse" }))
                .EnsureSuccessStatusCode();
        }

        var reports = await admin.GetFromJsonAsync<Page<JsonElement>>("api/admin/services/reports");
        Assert.True(reports!.Items.Count(r => r.GetProperty("targetId").GetGuid() == provider.OfferingId) == 3);
        var client = await MemberAsync();
        Assert.Empty((await client.GetFromJsonAsync<Detail>($"api/services/providers/{provider.ProviderId}"))!.Offerings.Where(o => o.Id == provider.OfferingId));

        // Justificatif : SIRET + justificatif validé = « Pro vérifié »
        await Ok<JsonElement>(provider.Http.PutAsJsonAsync("api/services/me", Profile("Ménage Express", 48.85, 2.35, "Paris", siret: "12345678901234")));
        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent("%PDF-1.4\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF"u8.ToArray());
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "kbis.pdf");
        form.Add(new StringContent("Registration"), "kind");
        var document = await Ok<JsonElement>(provider.Http.PostAsync("api/services/me/documents", form));
        var pending = await admin.GetFromJsonAsync<Page<AdminDocument>>("api/admin/services/documents");
        Assert.Contains(pending!.Items, d => d.Document.GetProperty("id").GetGuid() == document.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"api/admin/services/documents/{document.GetProperty("id").GetGuid()}/file")).StatusCode);
        var decided = await Ok<AdminDocument>(admin.PostAsync($"api/admin/services/documents/{document.GetProperty("id").GetGuid()}/approve", null));
        Assert.Equal("Pro", decided.VerificationLevel);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await AdminAsync("compta@beninconnect.test")).GetAsync("api/admin/services")).StatusCode);

        // Anti-spam : limite de demandes par jour
        await Ok<JsonElement>(admin.PutAsJsonAsync("api/admin/services/settings",
            new { alertKeywords = new[] { "western union" }, reportThreshold = 3, dailyRequestLimit = 1 }));
        var p2 = await ProviderAsync("Peintre Pro", 48.85, 2.35, "Paris", "peinture", 25);
        var p3 = await ProviderAsync("Peintre Deux", 48.85, 2.35, "Paris", "peinture", 30);
        await Ok<Request>(client.PostAsJsonAsync("api/services/requests", new { offeringId = p2.OfferingId, message = "Devis pour un salon de 20 m²" }));
        var limited = await client.PostAsJsonAsync("api/services/requests", new { offeringId = p3.OfferingId, message = "Devis pour une chambre" });
        Assert.Equal(HttpStatusCode.BadRequest, limited.StatusCode);
        Assert.Contains("1 demandes par jour", await limited.Content.ReadAsStringAsync());

        var overview = await admin.GetFromJsonAsync<JsonElement>("api/admin/services");
        Assert.True(overview.GetProperty("activeProviders").GetInt32() >= 3);
        var stats = await admin.GetFromJsonAsync<JsonElement>("api/admin/services/stats");
        Assert.Equal(12, stats.GetProperty("requestsByWeek").GetArrayLength());
        Assert.Contains(stats.GetProperty("byCategory").EnumerateArray(), c => c.GetProperty("name").GetString() == "Peinture");
    }

    [Fact]
    public async Task Categories_are_managed_in_the_console()
    {
        var admin = await AdminAsync();
        var tree = await admin.GetFromJsonAsync<List<Category>>("api/admin/services/categories");
        Assert.True(tree!.Count >= 7);
        Assert.True(tree.Sum(c => c.Children.Count) >= 25);
        var beauty = tree.Single(c => c.Slug == "beaute");
        var created = await Ok<Category>(admin.PostAsJsonAsync("api/admin/services/categories",
            new { slug = "barbier", name = "Barbier", icon = "content-cut", parentId = beauty.Id, sortOrder = 10, isActive = true, requiresQualification = true }));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("api/admin/services/categories",
            new { slug = "barbier", name = "Doublon", icon = "", parentId = (Guid?)null, sortOrder = 0, isActive = true, requiresQualification = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"api/admin/services/categories/{beauty.Id}",
            new { slug = "beaute", name = "Beauté", icon = "face-woman-shimmer", parentId = created.Id, sortOrder = 0, isActive = true, requiresQualification = false })).StatusCode);
        await Ok<Category>(admin.PutAsJsonAsync($"api/admin/services/categories/{created.Id}",
            new { slug = "barbier", name = "Barbier", icon = "content-cut", parentId = beauty.Id, sortOrder = 10, isActive = false, requiresQualification = true }));
        var member = await MemberAsync();
        var visible = await member.GetFromJsonAsync<List<Category>>("api/services/categories");
        Assert.DoesNotContain(visible!.SelectMany(c => c.Children), c => c.Slug == "barbier");
    }
}
