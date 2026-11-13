using BeninConnect.Modules.Media.Domain;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Api.Tests.Services;

/// <summary>Transitions des demandes, avis vérifiés, note bayésienne, mots-clés d'alerte, distance, métadonnées des photos.</summary>
public sealed class ServicesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static ProviderProfile Provider(Guid userId)
    {
        var p = ProviderProfile.Create(userId, new ProviderDetails("Afi Coiffure", "Tresses à domicile", null, "+33 6 12 34 56 78", false,
            ContactPreference.InApp, LegalStatus.Individual, null, null, "FR", "Paris", "75011", 48.86, 2.38, 15, true, false), false, Now);
        p.Submit(1, false, Now);
        p.Approve(Now);
        return p;
    }

    private static (ServiceRequest Request, ProviderProfile Provider, Guid Client) NewRequest()
    {
        var provider = Provider(Guid.NewGuid());
        var offering = ServiceOffering.Create(provider.Id,
            new OfferingDetails(Guid.NewGuid(), "Tresses africaines", "Pose complète", PriceType.From, 60, 180, [], true), "EUR", Now);
        var client = Guid.NewGuid();
        return (ServiceRequest.Create(offering, provider, client, "Bonjour, disponible samedi ?", null, "Paris", Now), provider, client);
    }

    [Fact]
    public void A_request_goes_from_sent_to_completed_and_rejects_invalid_steps()
    {
        var (request, provider, client) = NewRequest();
        Assert.Throws<DomainException>(() => request.Complete(client, Now)); // pas encore acceptée
        Assert.Throws<ForbiddenException>(() => request.Accept(client, Now)); // seul le prestataire accepte
        request.MarkViewed(Now);
        Assert.Equal(RequestStatus.Viewed, request.Status);
        request.Accept(provider.UserId, Now);
        Assert.Throws<DomainException>(() => request.Decline(provider.UserId, null, Now));
        request.Complete(client, Now.AddDays(1));
        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Throws<DomainException>(() => request.Cancel(client, Now));

        var (declined, p2, c2) = NewRequest();
        declined.Decline(p2.UserId, "Complet ce mois-ci", Now);
        Assert.Throws<DomainException>(() => declined.Accept(p2.UserId, Now));
        Assert.Throws<ForbiddenException>(() => declined.Cancel(p2.UserId, Now));
        Assert.Equal("Complet ce mois-ci", declined.DeclineReason);
        _ = c2;
    }

    [Fact]
    public void Nobody_requests_or_reviews_their_own_services()
    {
        var provider = Provider(Guid.NewGuid());
        var offering = ServiceOffering.Create(provider.Id, new OfferingDetails(Guid.NewGuid(), "Ménage", null, PriceType.Hourly, 15, null, [], true), "EUR", Now);
        Assert.Throws<DomainException>(() => ServiceRequest.Create(offering, provider, provider.UserId, "Pour moi-même, test", null, null, Now));
    }

    [Fact]
    public void A_review_needs_a_completed_request_and_is_frozen_after_seven_days()
    {
        var (request, provider, client) = NewRequest();
        Assert.Throws<DomainException>(() => Review.Create(request, client, 5, "Top", Now));
        request.Accept(provider.UserId, Now);
        request.Complete(provider.UserId, Now);
        Assert.Throws<ForbiddenException>(() => Review.Create(request, provider.UserId, 5, "Moi", Now));
        var review = Review.Create(request, client, 4, "Très bien", Now);
        review.Edit(client, 5, "Parfait", Now.AddDays(6));
        Assert.Equal(5, review.Rating);
        Assert.Throws<DomainException>(() => review.Edit(client, 1, "Changement", Now.AddDays(8)));
        review.Reply("Merci !", Now);
        Assert.Throws<DomainException>(() => review.Reply("Encore merci", Now));
        Assert.Throws<DomainException>(() => review.Hide("court")); // motif documenté obligatoire
        Assert.Throws<DomainException>(() => Review.Create(request, client, 6, null, Now));
    }

    [Fact]
    public void Bayesian_rating_does_not_favour_a_single_five_star_review()
    {
        Assert.True(ServiceRules.Bayesian(4.8, 40) > ServiceRules.Bayesian(5, 1));
        Assert.Equal(ServiceRules.PriorMean, ServiceRules.Bayesian(0, 0));
        Assert.True(ServiceRules.Score(4.8, 40, 2, 25, 0.9) > ServiceRules.Score(4.8, 40, 20, 25, 0.9)); // plus proche d'abord
    }

    [Fact]
    public void Alert_keywords_match_whole_words_without_accents()
    {
        string[] keywords = ["western union", "paiement d'avance", "arme"];
        Assert.Equal("western union", ServiceRules.FindAlert(keywords, "Paiement par Western-Union"));
        Assert.Equal("paiement d'avance", ServiceRules.FindAlert(keywords, "PAIEMENT D’AVANCE obligatoire"));
        Assert.Null(ServiceRules.FindAlert(keywords, "Coiffure", "Je charme vos cheveux"));
    }

    [Fact]
    public void Distance_and_currency()
    {
        Assert.InRange(ServiceRules.DistanceKm(48.8566, 2.3522, 45.7640, 4.8357), 390, 395); // Paris - Lyon
        var box = ServiceRules.BoundingBox(6.37, 2.39, 10);
        Assert.True(box.MinLat < 6.37 && box.MaxLng > 2.39);
        Assert.Equal("XOF", ServiceRules.CurrencyFor("bj"));
        Assert.Equal("EUR", ServiceRules.CurrencyFor("FR"));
    }

    [Fact]
    public void Profile_rules_for_pro_badge_and_review()
    {
        var p = Provider(Guid.NewGuid());
        Assert.Throws<DomainException>(() => p.Verify(VerificationLevel.Pro, Now)); // SIRET ou IFU requis
        p.Verify(VerificationLevel.Identity, Now);
        Assert.Equal(VerificationLevel.Identity, p.VerificationLevel);
        Assert.False(p.AddReport(3));
        Assert.False(p.AddReport(3));
        Assert.True(p.AddReport(3));
        Assert.False(p.IsSearchable);
        p.Reinstate();
        Assert.True(p.IsSearchable);
    }

    [Fact]
    public void Photo_metadata_is_removed()
    {
        byte[] jpeg =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0,
            0xFF, 0xE1, 0x00, 0x0E, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, (byte)'G', (byte)'P', (byte)'S', 1, 2, 3,
            0xFF, 0xDA, 0x00, 0x04, 1, 2, 0x55, 0x66, 0xFF, 0xD9
        ];
        var clean = ImageMetadata.Strip(jpeg, "jpg");
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(clean));
        Assert.Contains("JFIF", System.Text.Encoding.ASCII.GetString(clean));
        Assert.Equal(new byte[] { 0xFF, 0xD9 }, clean[^2..]);
        Assert.Equal(jpeg.Length - 16, clean.Length);
    }
}
