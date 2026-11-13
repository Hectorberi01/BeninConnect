using System.Text.Json;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BeninConnect.Modules.Jobs.Infrastructure.Persistence;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : ModuleDbContext(options, JobsDbContext.SchemaName), IJobsDb
{
    public const string SchemaName = "jobs";

    public DbSet<JobOffer> Offers => Set<JobOffer>();

    public DbSet<JobOfferReport> Reports => Set<JobOfferReport>();

    public DbSet<JobOfferFavorite> Favorites => Set<JobOfferFavorite>();

    public DbSet<JobSourceConfig> Sources => Set<JobSourceConfig>();

    public DbSet<JobSettings> Settings => Set<JobSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<JobOffer>(e =>
        {
            e.ToTable("imported_job_offers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Number).UseIdentityAlwaysColumn();
            e.Ignore(x => x.IsVisible);
            e.Property(x => x.ExternalId).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(10_000);
            e.Property(x => x.CompanyName).HasMaxLength(200);
            e.Property(x => x.CompanyLogoUrl).HasMaxLength(1000);
            e.Property(x => x.Country).HasMaxLength(2);
            e.Property(x => x.LocationLabel).HasMaxLength(200);
            e.Property(x => x.PostalCode).HasMaxLength(10);
            e.Property(x => x.Department).HasMaxLength(3);
            e.Property(x => x.ContractLabel).HasMaxLength(100);
            e.Property(x => x.WorkingTime).HasMaxLength(100);
            e.Property(x => x.SalaryLabel).HasMaxLength(200);
            e.Property(x => x.SectorLabel).HasMaxLength(200);
            e.Property(x => x.ApplyUrl).HasMaxLength(2000);
            e.Property(x => x.SourceLabel).HasMaxLength(100);
            e.Property(x => x.AlertKeyword).HasMaxLength(100);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.SearchKey).HasMaxLength(50);
            e.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.Fingerprint);
            // Liste des membres : offres visibles, les plus récentes d'abord
            e.HasIndex(x => new { x.Status, x.PublishedAt, x.Number });
            e.HasIndex(x => new { x.Source, x.Status, x.LastSeenAt });
            e.HasIndex(x => x.DuplicateOfId);
            e.HasIndex(x => x.NeedsReview);
        });

        b.Entity<JobOfferReport>(e =>
        {
            e.ToTable("job_offer_reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.OfferId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<JobOffer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<JobOfferFavorite>(e =>
        {
            e.ToTable("job_offer_favorites");
            e.HasKey(x => new { x.UserId, x.OfferId });
            e.HasIndex(x => x.OfferId);
            e.HasOne<JobOffer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        });

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        b.Entity<JobSourceConfig>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Source);
            e.Property(x => x.ClientId).HasMaxLength(200);
            e.Property(x => x.EncryptedSecret).HasMaxLength(2000);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.Property(x => x.Searches).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, json),
                v => JsonSerializer.Deserialize<List<JobSearchDefinition>>(v, json) ?? new List<JobSearchDefinition>(),
                new ValueComparer<List<JobSearchDefinition>>(
                    (a, c) => JsonSerializer.Serialize(a, json) == JsonSerializer.Serialize(c, json),
                    v => JsonSerializer.Serialize(v, json).GetHashCode(),
                    v => v.ToList()));
        });

        b.Entity<JobSettings>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}
