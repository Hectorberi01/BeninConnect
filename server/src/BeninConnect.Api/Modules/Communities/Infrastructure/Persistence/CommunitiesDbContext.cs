using BeninConnect.Modules.Communities.Application;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Infrastructure.Persistence;

public sealed class CommunitiesDbContext(DbContextOptions<CommunitiesDbContext> options)
    : ModuleDbContext(options, CommunitiesDbContext.SchemaName), ICommunitiesDb
{
    public const string SchemaName = "communities";

    public DbSet<Community> Communities => Set<Community>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Community>(e =>
        {
            e.ToTable("communities");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Ignore(x => x.IsPrivate);
            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.JoinRequests).WithOne().HasForeignKey(r => r.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.JoinRequests).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // UserId : identifiant du module Identity, sans clé étrangère.
        b.Entity<CommunityMember>(e =>
        {
            e.ToTable("members");
            e.HasKey(x => new { x.CommunityId, x.UserId });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<JoinRequest>(e =>
        {
            e.ToTable("join_requests");
            e.HasKey(x => x.Id);
            // Id fixé par le domaine : sans cela, une demande ajoutée à la collection est prise pour une mise à jour.
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.CommunityId, x.UserId }).IsUnique();
        });
    }
}
