using BeninConnect.Modules.AppVersionManagement.Application;
using BeninConnect.Modules.AppVersionManagement.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Infrastructure.Persistence;

public sealed class AppVersionsDbContext(DbContextOptions<AppVersionsDbContext> options) : ModuleDbContext(options, AppVersionsDbContext.SchemaName), IAppVersionsDb
{
    public const string SchemaName = "app_versions";

    public DbSet<AppInstallation> Installations => Set<AppInstallation>();
    public DbSet<AppRelease> Releases => Set<AppRelease>();
    public DbSet<UpdateCampaign> Campaigns => Set<UpdateCampaign>();
    public DbSet<CampaignRecipient> Recipients => Set<CampaignRecipient>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<AppInstallation>(e =>
        {
            e.ToTable("installations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AppCode).HasMaxLength(60);
            e.Property(x => x.Platform).HasMaxLength(10);
            e.Property(x => x.Version).HasMaxLength(24);
            e.Property(x => x.BuildNumber).HasMaxLength(VersionRules.MaxBuildLength);
            e.Property(x => x.OsVersion).HasMaxLength(VersionRules.MaxOsLength);
            e.Property(x => x.PushToken).HasMaxLength(AppInstallation.MaxPushTokenLength);
            e.HasIndex(x => new { x.InstallationId, x.AppCode }).IsUnique();
            e.HasIndex(x => new { x.AppCode, x.LastSeenAt });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<AppRelease>(e =>
        {
            e.ToTable("releases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AppCode).HasMaxLength(60);
            e.Property(x => x.Platform).HasMaxLength(10);
            e.Property(x => x.LatestVersion).HasMaxLength(24);
            e.Property(x => x.MinimumSupportedVersion).HasMaxLength(24);
            e.Property(x => x.StoreUrl).HasMaxLength(AppRelease.MaxStoreUrlLength);
            e.Property(x => x.UpdatedBy).HasMaxLength(120);
            e.HasIndex(x => new { x.AppCode, x.Platform }).IsUnique();
        });

        b.Entity<UpdateCampaign>(e =>
        {
            e.ToTable("campaigns");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AppCode).HasMaxLength(60);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DecidedBy).HasMaxLength(120);
            e.Property(x => x.RejectReason).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasMany(x => x.Recipients).WithOne().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<CampaignRecipient>(e =>
        {
            e.ToTable("campaign_recipients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Platform).HasMaxLength(10);
            e.Property(x => x.CurrentVersion).HasMaxLength(24);
            e.Property(x => x.TargetVersion).HasMaxLength(24);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.CampaignId, x.Status });
            e.HasIndex(x => new { x.UserId, x.Status, x.NotifiedAt });
        });
    }
}
