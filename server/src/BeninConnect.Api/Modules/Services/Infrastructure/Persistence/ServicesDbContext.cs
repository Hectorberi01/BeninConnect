using BeninConnect.Modules.Services.Application;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Infrastructure.Persistence;

public sealed class ServicesDbContext(DbContextOptions<ServicesDbContext> options) : ModuleDbContext(options, ServicesDbContext.SchemaName), IServicesDb
{
    public const string SchemaName = "services";

    public DbSet<ServiceCategory> Categories => Set<ServiceCategory>();

    public DbSet<ProviderProfile> Providers => Set<ProviderProfile>();

    public DbSet<ProviderDocument> Documents => Set<ProviderDocument>();

    public DbSet<ServiceOffering> Offerings => Set<ServiceOffering>();

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ServiceReport> Reports => Set<ServiceReport>();

    public DbSet<ProviderFavorite> Favorites => Set<ProviderFavorite>();

    public DbSet<ServicesSettings> Settings => Set<ServicesSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ServiceCategory>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Slug).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.Icon).HasMaxLength(60);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.ParentId);
        });

        b.Entity<ProviderProfile>(e =>
        {
            e.ToTable("providers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.IsSearchable);
            e.Ignore(x => x.NeedsReview);
            e.Ignore(x => x.Currency);
            e.Ignore(x => x.HasBusinessNumber);
            e.Property(x => x.DisplayName).HasMaxLength(80);
            e.Property(x => x.Bio).HasMaxLength(1500);
            e.Property(x => x.PhotoPath).HasMaxLength(300);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Siret).HasMaxLength(14);
            e.Property(x => x.Ifu).HasMaxLength(20);
            e.Property(x => x.Country).HasMaxLength(2);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.PostalCode).HasMaxLength(10);
            e.Property(x => x.ReviewNote).HasMaxLength(500);
            e.Property(x => x.AlertKeyword).HasMaxLength(100);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => new { x.Status, x.City });
            // Recherche géographique : pré-filtre rectangle sur latitude puis longitude
            e.HasIndex(x => new { x.Status, x.Latitude, x.Longitude });
        });

        b.Entity<ProviderDocument>(e =>
        {
            e.ToTable("provider_documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.OriginalName).HasMaxLength(150);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.ReviewNote).HasMaxLength(500);
            e.HasIndex(x => new { x.Status, x.UploadedAt });
            e.HasIndex(x => x.ProviderId);
        });

        b.Entity<ServiceOffering>(e =>
        {
            e.ToTable("offerings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.IsHidden);
            e.Ignore(x => x.IsListed);
            e.Ignore(x => x.NeedsReview);
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(3000);
            e.Property(x => x.PriceAmount).HasPrecision(14, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.AlertKeyword).HasMaxLength(100);
            e.Property(x => x.ModerationNote).HasMaxLength(500);
            e.HasIndex(x => new { x.HiddenReason, x.CategoryId });
            e.HasIndex(x => x.ProviderId);
        });

        b.Entity<ServiceRequest>(e =>
        {
            e.ToTable("requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.DeclineReason).HasMaxLength(500);
            e.HasIndex(x => new { x.ClientId, x.CreatedAt });
            e.HasIndex(x => new { x.ProviderUserId, x.CreatedAt });
            e.HasIndex(x => new { x.ProviderId, x.Status });
            e.HasIndex(x => new { x.Status, x.CompletedAt });
        });

        b.Entity<Review>(e =>
        {
            e.ToTable("reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.ProviderReply).HasMaxLength(1000);
            e.Property(x => x.ModerationNote).HasMaxLength(500);
            e.HasIndex(x => x.RequestId).IsUnique();
            e.HasIndex(x => new { x.ProviderId, x.Status, x.CreatedAt });
            e.HasIndex(x => x.AuthorId);
        });

        b.Entity<ServiceReport>(e =>
        {
            e.ToTable("reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Decision).HasMaxLength(200);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.AuthorId }).IsUnique();
            e.HasIndex(x => new { x.Handled, x.CreatedAt });
        });

        b.Entity<ProviderFavorite>(e =>
        {
            e.ToTable("favorites");
            e.HasKey(x => new { x.UserId, x.ProviderId });
            e.HasIndex(x => x.ProviderId);
        });

        b.Entity<ServicesSettings>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}
