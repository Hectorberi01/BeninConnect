using BeninConnect.Modules.Editorial.Application;
using BeninConnect.Modules.Editorial.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Editorial.Infrastructure.Persistence;

public sealed class EditorialDbContext(DbContextOptions<EditorialDbContext> options) : ModuleDbContext(options, EditorialDbContext.SchemaName), IEditorialDb
{
    public const string SchemaName = "editorial";

    public DbSet<ContentCandidate> Candidates => Set<ContentCandidate>();

    public DbSet<EditorialSource> Sources => Set<EditorialSource>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ContentCandidate>(e =>
        {
            e.ToTable("content_candidates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.IsManual);
            e.Property(x => x.SourceName).HasMaxLength(100);
            e.Property(x => x.SourceUrl).HasMaxLength(2000);
            e.Property(x => x.SourceHash).HasMaxLength(64);
            e.Property(x => x.SourceTitle).HasMaxLength(500);
            e.Property(x => x.SourceExcerpt).HasMaxLength(2000);
            e.Property(x => x.DraftContent).HasMaxLength(5000);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            // Dédoublonnage : un article = un candidat (les publications manuelles n'ont pas d'empreinte)
            e.HasIndex(x => x.SourceHash).IsUnique().HasFilter("\"SourceHash\" <> ''");
            e.HasIndex(x => new { x.Status, x.ScheduledAt });
            e.HasIndex(x => new { x.Status, x.CollectedAt });
        });

        b.Entity<EditorialSource>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Url).HasMaxLength(2000);
            e.Property(x => x.LastError).HasMaxLength(500);
            e.HasIndex(x => x.Url).IsUnique();
        });
    }
}
