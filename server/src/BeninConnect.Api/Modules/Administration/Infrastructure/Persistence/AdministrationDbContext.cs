using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Infrastructure.Persistence;

public sealed class AdministrationDbContext(DbContextOptions<AdministrationDbContext> options)
    : ModuleDbContext(options, AdministrationDbContext.SchemaName), IAdministrationDb
{
    public const string SchemaName = "administration";

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
    }
}
