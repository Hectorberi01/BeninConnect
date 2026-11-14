using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeninConnect.Modules.Voyages.Application;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests.Voyages;

/// <summary>Base dédiée : les trajets publiés ici ne se mélangent pas aux autres tests.</summary>
public sealed class VoyagesFactory : ApiFactory;

public sealed class VoyagesTests(VoyagesFactory factory) : IClassFixture<VoyagesFactory>, IAsyncLifetime
{
    /// <summary>Le module est masqué par défaut : l'équipe l'active avant chaque test.</summary>
    public async Task InitializeAsync() =>
        (await (await AdminAsync("admin@beninconnect.test")).PutAsJsonAsync("api/admin/trips/settings", new { enabled = true })).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record TokenBody(string Token);

    private sealed record IdBody(Guid Id);

    private sealed record Person(Guid Id, string Name);

    private sealed record Card(Guid Id, Person Traveler, string FromCity, string ToCity, decimal CapacityKg, decimal RemainingKg, string Status,
        double TravelerRating, int TravelerReviewCount, bool TrustedTraveler);

    private sealed record CardPage(List<Card> Items, string? NextCursor, int Total);

    private sealed record Detail(Card Trip, bool CanBook, Booking? MyBooking, string HiddenReason);

    private sealed record Booking(Guid Id, Guid TripId, string Status, Guid? ConversationId, bool HasCode, bool CodeLocked, int AttemptsLeft, bool CanReview,
        Person Sender, Person Traveler);

    private sealed record MyTrip(Detail Detail, List<Booking> Bookings, int PendingCount);

    private sealed record CodeBody(string Code, string ShareText);

    private sealed record Stats(int DeliveriesAsTraveler, int ShipmentsDelivered, double RatingAverage, int RatingCount, bool TrustedTraveler, List<Card> UpcomingTrips);

    private sealed record Notification(string Title, string Body);

    private sealed record Page<T>(List<T> Items, int TotalCount);

    private sealed record AdminTrip(Guid Id, string HiddenReason, int ReportCount);

    private sealed record AdminBooking(Guid Id, string Status);

    private static string Day(int days) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days).ToString("yyyy-MM-dd");

    private async Task<(HttpClient Http, Guid Id)> MemberAsync(string first = "Afi")
    {
        var http = factory.CreateClient();
        var email = $"voyages.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = first, lastName = "Voyage", email, country = "France", city = "Paris", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return (http, (await http.GetFromJsonAsync<IdBody>("api/auth/me"))!.Id);
    }

    private async Task<HttpClient> AdminAsync(string email = "moderation@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private static async Task<T> Ok<T>(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<string> Error(Task<HttpResponseMessage> call, HttpStatusCode expected)
    {
        var response = await call;
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode} {body}");
        return body;
    }

    private static object TripBody(string fromCity = "Paris", string toCity = "Cotonou", int inDays = 20, decimal kg = 8, string from = "FR", string to = "BJ") => new
    {
        fromCountry = from, fromCity, toCountry = to, toCity, departureDate = Day(inDays), capacityKg = kg, pricePerKg = 10, currency = "EUR",
        acceptedCategories = new[] { "Documents", "Clothes" }, dropOffZones = "Paris 11e, Gare du Nord", notes = "Valise en soute."
    };

    private static object BookingBody(decimal kg = 3, string category = "Clothes", bool certified = true) =>
        new { weightKg = kg, category, description = "Vêtements pour la famille, deux sacs.", certified };

    private static async Task<List<Notification>> NotificationsAsync(HttpClient http) =>
        (await http.GetFromJsonAsync<List<Notification>>("api/notifications"))!;

    private static async Task<bool> NotifiedAsync(HttpClient http, string title)
    {
        for (var i = 0; i < 40; i++, await Task.Delay(100))
        {
            if ((await NotificationsAsync(http)).Any(n => n.Title == title))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Status(bool Enabled);

    [Fact]
    public async Task Module_is_hidden_until_an_administrator_shows_it()
    {
        var (member, _) = await MemberAsync();
        var admin = await AdminAsync("admin@beninconnect.test");
        await Error((await AdminAsync()).PutAsJsonAsync("api/admin/trips/settings", new { enabled = false }), HttpStatusCode.Forbidden); // modérateur
        await Ok<JsonElement>(admin.PutAsJsonAsync("api/admin/trips/settings", new { enabled = false }));
        try
        {
            Assert.False((await member.GetFromJsonAsync<Status>("api/trips/status"))!.Enabled);
            await Error(member.GetAsync("api/trips"), HttpStatusCode.NotFound);
            await Error(member.PostAsJsonAsync("api/trips", TripBody()), HttpStatusCode.NotFound);
            await Error(member.GetAsync("api/bookings/mine"), HttpStatusCode.NotFound);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("api/admin/trips/overview")).StatusCode); // la console reste accessible
        }
        finally
        {
            await Ok<JsonElement>(admin.PutAsJsonAsync("api/admin/trips/settings", new { enabled = true }));
        }

        Assert.True((await member.GetFromJsonAsync<Status>("api/trips/status"))!.Enabled);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("api/trips")).StatusCode);
    }

    [Fact]
    public async Task Publish_search_book_deliver_with_code_and_review()
    {
        var (traveler, travelerId) = await MemberAsync("Afi");
        var (sender, _) = await MemberAsync("Codjo");
        var city = $"Abomey{Guid.NewGuid():N}"[..14];
        var trip = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(toCity: city)));
        Assert.Equal(8, trip.Trip.CapacityKg);

        // Recherche par pays, ville (sans accent ni casse) et catégorie
        var found = await sender.GetFromJsonAsync<CardPage>($"api/trips?from=FR&to=BJ&toCity={city.ToUpperInvariant()}&category=Clothes");
        Assert.Contains(found!.Items, t => t.Id == trip.Trip.Id);
        Assert.Empty((await sender.GetFromJsonAsync<CardPage>($"api/trips?from=FR&to=BJ&toCity={city}&category=Electronics"))!.Items);

        // Réservation : attestation obligatoire, catégorie acceptée, pas sur son propre trajet
        await Error(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody(certified: false)), HttpStatusCode.BadRequest);
        await Error(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody(category: "Electronics")), HttpStatusCode.BadRequest);
        await Error(traveler.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()), HttpStatusCode.BadRequest);
        var booking = await Ok<Booking>(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()));
        Assert.Equal("Pending", booking.Status);
        await Error(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()), HttpStatusCode.Conflict);
        Assert.True(await NotifiedAsync(traveler, "Nouvelle demande de réservation"));

        // Seul le voyageur accepte ; la conversation s'ouvre
        await Error(sender.PostAsync($"api/bookings/{booking.Id}/accept", null), HttpStatusCode.Forbidden);
        await Error(sender.PostAsync($"api/bookings/{booking.Id}/code", null), HttpStatusCode.BadRequest); // pas encore acceptée
        var accepted = await Ok<Booking>(traveler.PostAsync($"api/bookings/{booking.Id}/accept", null));
        Assert.Equal("Accepted", accepted.Status);
        Assert.NotNull(accepted.ConversationId);
        var conversations = await sender.GetFromJsonAsync<List<JsonElement>>("api/conversations");
        Assert.Contains(conversations!, c => c.GetProperty("conversation").GetProperty("id").GetGuid() == accepted.ConversationId);
        var detail = await sender.GetFromJsonAsync<Detail>($"api/trips/{trip.Trip.Id}");
        Assert.Equal(5, detail!.Trip.RemainingKg);
        Assert.False(detail.CanBook);

        // Ni téléphone ni email dans les réponses
        var raw = await sender.GetStringAsync($"api/trips/{trip.Trip.Id}");
        Assert.DoesNotContain("@exemple.test", raw);
        Assert.DoesNotContain("phone", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Afi V.", raw);

        // Code de remise : réservé à l'expéditeur, 5 essais puis nouveau code
        await Error(traveler.PostAsync($"api/bookings/{booking.Id}/code", null), HttpStatusCode.Forbidden);
        await Error(traveler.PostAsJsonAsync($"api/bookings/{booking.Id}/deliver", new { code = "123456" }), HttpStatusCode.BadRequest); // aucun code
        var code = await Ok<CodeBody>(sender.PostAsync($"api/bookings/{booking.Id}/code", null));
        Assert.Matches("^[0-9]{6}$", code.Code);
        Assert.Contains(code.Code, code.ShareText);
        var wrong = code.Code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            await Error(traveler.PostAsJsonAsync($"api/bookings/{booking.Id}/deliver", new { code = wrong }), HttpStatusCode.BadRequest);
        }

        var locked = await sender.GetFromJsonAsync<Booking>($"api/bookings/{booking.Id}");
        Assert.True(locked!.CodeLocked);
        await Error(traveler.PostAsJsonAsync($"api/bookings/{booking.Id}/deliver", new { code = code.Code }), HttpStatusCode.BadRequest); // bloqué
        Assert.True(await NotifiedAsync(sender, "Code de remise bloqué"));
        await Error(sender.PostAsJsonAsync($"api/bookings/{booking.Id}/review", new { rating = 5 }), HttpStatusCode.BadRequest);

        var fresh = await Ok<CodeBody>(sender.PostAsync($"api/bookings/{booking.Id}/code", null));
        var delivered = await Ok<Booking>(traveler.PostAsJsonAsync($"api/bookings/{booking.Id}/deliver", new { code = fresh.Code }));
        Assert.Equal("Delivered", delivered.Status);
        Assert.True(delivered.CanReview);
        Assert.True(await NotifiedAsync(sender, "Colis livré"));

        // Avis : un par côté
        await Ok<JsonElement>(sender.PostAsJsonAsync($"api/bookings/{booking.Id}/review", new { rating = 5, comment = "Colis arrivé intact." }));
        await Error(sender.PostAsJsonAsync($"api/bookings/{booking.Id}/review", new { rating = 4 }), HttpStatusCode.Conflict);
        await Ok<JsonElement>(traveler.PostAsJsonAsync($"api/bookings/{booking.Id}/review", new { rating = 5 }));
        var stats = await sender.GetFromJsonAsync<Stats>($"api/trips/users/{travelerId}/stats");
        Assert.Equal(1, stats!.DeliveriesAsTraveler);
        Assert.Equal(5, stats.RatingAverage);
        Assert.False(stats.TrustedTraveler); // 3 livraisons notées 4/5 ou plus
        Assert.Contains(stats.UpcomingTrips, t => t.Id == trip.Trip.Id);
    }

    [Fact]
    public async Task Capacity_is_checked_at_acceptance_and_trip_fills_up()
    {
        var (traveler, _) = await MemberAsync();
        var trip = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(kg: 5)));
        var (a, _) = await MemberAsync("Sena");
        var (b, _) = await MemberAsync("Kossi");
        var first = await Ok<Booking>(a.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody(kg: 4)));
        var second = await Ok<Booking>(b.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody(kg: 3)));
        await Error(b.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody(kg: 60)), HttpStatusCode.Conflict); // déjà une demande

        await Ok<Booking>(traveler.PostAsync($"api/bookings/{first.Id}/accept", null));
        Assert.Contains("kilos", await Error(traveler.PostAsync($"api/bookings/{second.Id}/accept", null), HttpStatusCode.BadRequest));
        await Ok<Booking>(traveler.PostAsJsonAsync($"api/bookings/{second.Id}/decline", new { reason = "Plus de place" }));
        Assert.True(await NotifiedAsync(b, "Réservation refusée"));

        // Modification interdite après une acceptation ; l'annulation prévient les expéditeurs
        await Error(traveler.PutAsJsonAsync($"api/trips/{trip.Trip.Id}", TripBody(kg: 10)), HttpStatusCode.BadRequest);
        var cancelled = await Ok<Detail>(traveler.PostAsync($"api/trips/{trip.Trip.Id}/cancel", null));
        Assert.Equal("Cancelled", cancelled.Trip.Status);
        Assert.True(await NotifiedAsync(a, "Trajet annulé"));
        Assert.Equal("Cancelled", (await a.GetFromJsonAsync<Booking>($"api/bookings/{first.Id}"))!.Status);
    }

    [Fact]
    public async Task Rules_on_publication_dates_and_limits()
    {
        var (traveler, _) = await MemberAsync();
        await Error(traveler.PostAsJsonAsync("api/trips", TripBody(inDays: -1)), HttpStatusCode.BadRequest);
        await Error(traveler.PostAsJsonAsync("api/trips", TripBody(inDays: 91)), HttpStatusCode.BadRequest);
        await Error(traveler.PostAsJsonAsync("api/trips", TripBody(kg: 0)), HttpStatusCode.BadRequest);
        await Error(traveler.PostAsJsonAsync("api/trips", TripBody(fromCity: "Cotonou", toCity: "cotonou", from: "BJ", to: "BJ")), HttpStatusCode.BadRequest);
        await Error(traveler.PostAsJsonAsync("api/trips", TripBody(from: "France")), HttpStatusCode.BadRequest);

        // Sens Bénin → Europe aussi ; au plus 5 trajets par jour
        for (var i = 0; i < 5; i++)
        {
            await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(fromCity: "Cotonou", toCity: "Lyon", from: "BJ", to: "FR", inDays: 10 + i)));
        }

        await Error(traveler.PostAsJsonAsync("api/trips", TripBody()), HttpStatusCode.BadRequest);
        var mine = await traveler.GetFromJsonAsync<List<MyTrip>>("api/trips/mine");
        Assert.Equal(5, mine!.Count);
    }

    [Fact]
    public async Task Blocked_members_do_not_see_each_other()
    {
        var (traveler, travelerId) = await MemberAsync();
        var (other, otherId) = await MemberAsync("Yao");
        var city = $"Natitingou{Guid.NewGuid():N}"[..16];
        var trip = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(toCity: city)));
        (await traveler.PostAsync($"api/users/{otherId}/block", null)).EnsureSuccessStatusCode();

        Assert.Empty((await other.GetFromJsonAsync<CardPage>($"api/trips?toCity={city}"))!.Items);
        await Error(other.GetAsync($"api/trips/{trip.Trip.Id}"), HttpStatusCode.NotFound);
        await Error(other.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()), HttpStatusCode.NotFound);
        await Error(other.GetAsync($"api/trips/users/{travelerId}/stats"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Route_alert_notifies_matching_members_only()
    {
        var (watcher, _) = await MemberAsync("Sena");
        var (traveler, _) = await MemberAsync();
        var city = $"Parakou{Guid.NewGuid():N}"[..13];
        await Ok<JsonElement>(watcher.PostAsJsonAsync("api/trips/alerts", new { fromCountry = "FR", toCountry = "BJ", toCity = city, before = Day(30) }));
        for (var i = 0; i < 4; i++)
        {
            await Ok<JsonElement>(watcher.PostAsJsonAsync("api/trips/alerts", new { fromCountry = "FR", toCountry = "BJ" }));
        }

        await Error(watcher.PostAsJsonAsync("api/trips/alerts", new { fromCountry = "FR", toCountry = "BJ" }), HttpStatusCode.BadRequest); // 5 au plus

        await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(toCity: city, inDays: 45))); // hors période : alerte « ville » muette
        var before = (await NotificationsAsync(watcher)).Count(n => n.Title == "Un membre part bientôt");
        await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(toCity: city, inDays: 12)));
        Assert.True(await NotifiedAsync(watcher, "Un membre part bientôt"));
        Assert.True((await NotificationsAsync(watcher)).Count(n => n.Title == "Un membre part bientôt") <= before + 1); // une seule notification

        var alerts = await watcher.GetFromJsonAsync<List<IdBody>>("api/trips/alerts");
        Assert.Equal(HttpStatusCode.NoContent, (await watcher.DeleteAsync($"api/trips/alerts/{alerts![0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await traveler.DeleteAsync($"api/trips/alerts/{alerts[1].Id}")).StatusCode);
    }

    [Fact]
    public async Task Dispute_reports_and_moderation()
    {
        var (traveler, _) = await MemberAsync();
        var (sender, _) = await MemberAsync("Codjo");
        var trip = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody()));
        var booking = await Ok<Booking>(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()));
        await Ok<Booking>(traveler.PostAsync($"api/bookings/{booking.Id}/accept", null));

        var disputed = await Ok<Booking>(sender.PostAsJsonAsync($"api/bookings/{booking.Id}/dispute", new { reason = "Le voyageur ne répond plus." }));
        Assert.Equal("Disputed", disputed.Status);
        Assert.True(await NotifiedAsync(traveler, "Litige ouvert"));

        // Console : comptable refusé, modération autorisée
        await Error((await AdminAsync("compta@beninconnect.test")).GetAsync("api/admin/trips/disputes"), HttpStatusCode.Forbidden);
        var moderator = await AdminAsync();
        var disputes = await moderator.GetFromJsonAsync<Page<AdminBooking>>("api/admin/trips/disputes");
        Assert.Contains(disputes!.Items, b => b.Id == booking.Id);
        await Error(moderator.PostAsJsonAsync($"api/admin/trips/bookings/{booking.Id}/resolve", new { outcome = "Cancelled", note = "" }), HttpStatusCode.BadRequest);
        var resolved = await Ok<AdminBooking>(moderator.PostAsJsonAsync($"api/admin/trips/bookings/{booking.Id}/resolve",
            new { outcome = "Cancelled", note = "Remise jamais effectuée." }));
        Assert.Equal("Cancelled", resolved.Status);
        Assert.True(await NotifiedAsync(sender, "Litige tranché"));

        // 3 signalements masquent le trajet ; la modération le rétablit
        var other = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(inDays: 30)));
        await Error(traveler.PostAsJsonAsync("api/trips/reports", new { targetType = "Trip", targetId = other.Trip.Id, reason = "Mon propre trajet" }),
            HttpStatusCode.BadRequest);
        for (var i = 0; i < 3; i++)
        {
            var (reporter, _) = await MemberAsync("Yao");
            await Ok<JsonElement>(reporter.PostAsJsonAsync("api/trips/reports", new { targetType = "Trip", targetId = other.Trip.Id, reason = "Annonce suspecte" }));
            if (i == 0)
            {
                await Error(reporter.PostAsJsonAsync("api/trips/reports", new { targetType = "Trip", targetId = other.Trip.Id, reason = "Encore" }),
                    HttpStatusCode.Conflict);
            }
        }

        await Error(sender.GetAsync($"api/trips/{other.Trip.Id}"), HttpStatusCode.NotFound);
        Assert.Equal("Reports", (await traveler.GetFromJsonAsync<Detail>($"api/trips/{other.Trip.Id}"))!.HiddenReason);
        var reported = await moderator.GetFromJsonAsync<Page<AdminTrip>>("api/admin/trips?view=Hidden");
        Assert.Contains(reported!.Items, t => t.Id == other.Trip.Id);
        await Ok<JsonElement>(moderator.PostAsync($"api/admin/trips/{other.Trip.Id}/show", null));
        Assert.Equal(HttpStatusCode.OK, (await sender.GetAsync($"api/trips/{other.Trip.Id}")).StatusCode);
        await Error(moderator.PostAsJsonAsync($"api/admin/trips/{other.Trip.Id}/hide", new { reason = "" }), HttpStatusCode.BadRequest);
        await Ok<JsonElement>(moderator.PostAsJsonAsync($"api/admin/trips/{other.Trip.Id}/hide", new { reason = "Transport de médicaments annoncé." }));
        Assert.True(await NotifiedAsync(traveler, "Votre trajet est masqué"));
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("api/admin/trips/overview")).StatusCode);
    }

    [Fact]
    public async Task Reminder_suspension_and_account_deletion()
    {
        var (traveler, travelerId) = await MemberAsync();
        var (sender, _) = await MemberAsync("Codjo");
        var trip = await Ok<Detail>(traveler.PostAsJsonAsync("api/trips", TripBody(inDays: 1)));
        var booking = await Ok<Booking>(sender.PostAsJsonAsync($"api/trips/{trip.Trip.Id}/bookings", BookingBody()));
        await Ok<Booking>(traveler.PostAsync($"api/bookings/{booking.Id}/accept", null));

        using (var scope = factory.Services.CreateScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<TripLifecycle>();
            Assert.True(await lifecycle.SendRemindersAsync(CancellationToken.None) >= 1);
        }

        Assert.True(await NotifiedAsync(sender, "Le voyageur part demain"));

        // Suspension : trajets masqués au passage suivant
        var admin = await AdminAsync();
        (await admin.PostAsJsonAsync($"api/admin/members/{travelerId}/suspend", new { reason = "Comportement à vérifier" })).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TripLifecycle>().SyncSuspendedAsync(CancellationToken.None);
        }

        await Error(sender.GetAsync($"api/trips/{trip.Trip.Id}"), HttpStatusCode.NotFound);
        (await admin.PostAsync($"api/admin/members/{travelerId}/reactivate", null)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TripLifecycle>().SyncSuspendedAsync(CancellationToken.None);
        }

        Assert.Equal(HttpStatusCode.OK, (await sender.GetAsync($"api/trips/{trip.Trip.Id}")).StatusCode);

        // Suppression du compte voyageur : l'expéditeur est prévenu, tout est effacé
        Assert.Equal(HttpStatusCode.NoContent, (await traveler.PostAsJsonAsync("api/users/me/delete", new { password = "Secret123!" })).StatusCode);
        Assert.True(await NotifiedAsync(sender, "Trajet annulé"));
        await Error(sender.GetAsync($"api/bookings/{booking.Id}"), HttpStatusCode.NotFound);
        Assert.Empty((await sender.GetFromJsonAsync<List<Booking>>("api/bookings/mine"))!);
    }
}
