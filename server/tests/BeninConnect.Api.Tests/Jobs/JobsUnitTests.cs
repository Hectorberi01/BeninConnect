using System.Text.Json;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Modules.Jobs.Infrastructure.Sources;

namespace BeninConnect.Api.Tests.Jobs;

/// <summary>Empreinte, mapping des sources, transitions de statut, mots-clés d'alerte.</summary>
public sealed class JobsUnitTests
{
    private static JsonElement Sample(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Jobs", "Samples", $"{name}.json"))).RootElement;

    private static ImportedOfferData Data(string title = "Comptable", DateTimeOffset? closing = null) =>
        new("X1", title, "Description", "Société", null, "FR", "Paris", null, "75", null, null, ContractType.Cdi, "CDI", false, null, null, null,
            "https://exemple.test/offre", null, DateTimeOffset.UtcNow.AddDays(-1), closing);

    [Fact]
    public void Fingerprint_ignores_case_accents_and_punctuation()
    {
        Assert.Equal("developpeur net h f", JobText.Normalize("Développeur .NET – H/F"));
        Assert.Equal(JobText.Fingerprint("Développeur .NET (H/F)", "ACME", "Paris"), JobText.Fingerprint("developpeur net h-f", "Acmé", " PARIS "));
        Assert.NotEqual(JobText.Fingerprint("Développeur", "ACME", "Paris"), JobText.Fingerprint("Développeur", "ACME", "Lyon"));
        Assert.Equal(64, JobText.Fingerprint("a", null, null).Length);
    }

    [Fact]
    public void France_travail_offer_is_mapped()
    {
        var offer = FranceTravailSource.Map(Sample("francetravail"))!;
        Assert.Equal("201XKQM", offer.ExternalId);
        Assert.Equal("FR", offer.Country);
        Assert.Equal("75", offer.Department);
        Assert.Equal("75011", offer.PostalCode);
        Assert.Equal(ContractType.Cdi, offer.ContractType);
        Assert.Equal("ACME SERVICES", offer.CompanyName);
        Assert.True(offer.IsRemote);
        Assert.Equal("Temps plein", offer.WorkingTime);
        Assert.StartsWith("https://candidat.francetravail.fr/", offer.ApplyUrl);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero), offer.PublishedAt);
        Assert.Equal(ContractType.Alternance, FranceTravailSource.Contract("CDD", "Contrat apprentissage", false));
        Assert.Equal(ContractType.Interim, FranceTravailSource.Contract("MIS", "", false));
    }

    [Fact]
    public void Reliefweb_offer_is_mapped_with_plain_text_and_closing_date()
    {
        var offer = ReliefWebSource.Map(Sample("reliefweb"))!;
        Assert.Equal("4123456", offer.ExternalId);
        Assert.Equal("BJ", offer.Country);
        Assert.Equal("Cotonou, Benin", offer.LocationLabel);
        Assert.Equal("Médecins du Monde", offer.CompanyName);
        Assert.Equal("ReliefWeb · Médecins du Monde", offer.SourceLabel);
        Assert.Equal("Le projet couvre Cotonou.\n• Suivi\n• Rapports", offer.Description);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), offer.ClosingAt);
        Assert.Equal(ContractType.Stage, ReliefWebSource.Contract("Internship"));
    }

    [Fact]
    public void Adzuna_offer_is_mapped()
    {
        var offer = AdzunaSource.Map(Sample("adzuna"))!;
        Assert.Equal("Développeur C# confirmé", offer.Title);
        Assert.Equal(ContractType.Cdi, offer.ContractType);
        Assert.Equal("Lyon, Rhône", offer.LocationLabel);
        Assert.Contains("42", offer.SalaryLabel);
        Assert.Equal("https://www.adzuna.fr/land/ad/4870000001", offer.ApplyUrl);
    }

    [Fact]
    public void A_hidden_offer_is_never_shown_again_by_an_import()
    {
        var now = DateTimeOffset.UtcNow;
        var offer = JobOffer.Import(JobSource.FranceTravail, Data(), "test", now);
        offer.Hide(HiddenReason.Admin);
        offer.Refresh(Data("Comptable confirmé"), "test", now.AddHours(1));
        Assert.Equal(JobOfferStatus.Hidden, offer.Status);
        Assert.Equal("Comptable confirmé", offer.Title);

        var expired = JobOffer.Import(JobSource.FranceTravail, Data(), "test", now);
        Assert.True(expired.Expire(now));
        expired.Refresh(Data(), "test", now.AddHours(1));
        Assert.Equal(JobOfferStatus.Active, expired.Status);

        var closed = JobOffer.Import(JobSource.ReliefWeb, Data(closing: now.AddDays(-1)), "test", now);
        Assert.Equal(JobOfferStatus.Expired, closed.Status);
    }

    [Fact]
    public void Reports_hide_the_offer_at_the_threshold_until_review()
    {
        var offer = JobOffer.Import(JobSource.FranceTravail, Data(), "test", DateTimeOffset.UtcNow);
        Assert.False(offer.AddReport(3));
        Assert.False(offer.AddReport(3));
        Assert.True(offer.AddReport(3));
        Assert.Equal(JobOfferStatus.Hidden, offer.Status);
        Assert.True(offer.NeedsReview);
        offer.Show(DateTimeOffset.UtcNow);
        Assert.Equal(JobOfferStatus.Active, offer.Status);
        Assert.Equal(0, offer.ReportCount);
    }

    [Fact]
    public void Alert_keywords_are_found_whatever_the_accents_and_case()
    {
        string[] keywords = ["frais de dossier", "Western Union"];
        Assert.Equal("frais de dossier", JobText.FindAlert(keywords, "Poste à Paris", "Des FRAIS DE DOSSIER sont demandés."));
        Assert.Equal("Western Union", JobText.FindAlert(keywords, "Paiement par western-union"));
        Assert.Null(JobText.FindAlert(keywords, "Aucun frais", "dossiers à traiter"));
    }

    [Fact]
    public void Source_configuration_is_validated()
    {
        var config = JobSourceConfig.CreateDefault(JobSource.ReliefWeb);
        Assert.Throws<BeninConnect.Shared.Domain.DomainException>(() => config.Configure(true, 360, null, config.Searches));
        Assert.Throws<BeninConnect.Shared.Domain.DomainException>(() => config.Configure(false, 10, "x", config.Searches));
        Assert.Throws<BeninConnect.Shared.Domain.DomainException>(() => config.Configure(false, 360, "x", [new("Mauvaise Clé")]));
        config.Configure(true, 360, "beninconnect-x", config.Searches);
        Assert.True(config.IsEnabled);
        Assert.Throws<BeninConnect.Shared.Domain.DomainException>(() =>
            JobSourceConfig.CreateDefault(JobSource.Remotive).Configure(true, 360, null, [new("dev", Category: "software-dev")]));
    }
}
