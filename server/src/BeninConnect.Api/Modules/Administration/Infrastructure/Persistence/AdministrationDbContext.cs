using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Domain;
using System.Text.Json;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BeninConnect.Modules.Administration.Infrastructure.Persistence;

public sealed class AdministrationDbContext(DbContextOptions<AdministrationDbContext> options)
    : ModuleDbContext(options, AdministrationDbContext.SchemaName), IAdministrationDb
{
    public const string SchemaName = "administration";

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<HomeBanner> Banners => Set<HomeBanner>();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<AdminUser>(e =>
        {
            e.ToTable("admin_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.DisplayName).HasMaxLength(80);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AdminName).HasMaxLength(80);
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.TargetType).HasMaxLength(32);
            e.Property(x => x.Details).HasMaxLength(500);
            e.HasIndex(x => x.At);
        });

        b.Entity<HomeBanner>(e =>
        {
            e.ToTable("banners");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(BannerRules.MaxTitle);
            e.Property(x => x.ImagePath).HasMaxLength(200);
            e.Property(x => x.Target).HasMaxLength(BannerRules.MaxUrl);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            // Boutons en JSON (4 au plus)
            e.Property(x => x.Actions).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<BannerAction>>(v, JsonOptions) ?? new List<BannerAction>(),
                new ValueComparer<List<BannerAction>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList()));
            e.HasIndex(x => x.Position);
        });
    }
}
