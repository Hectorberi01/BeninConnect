using BeninConnect.Modules.Moderation.Application;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Moderation.Infrastructure.Persistence;

public sealed class ModerationDbContext(DbContextOptions<ModerationDbContext> options) : ModuleDbContext(options, ModerationDbContext.SchemaName), IModerationDb
{
    public const string SchemaName = "moderation";

    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        // La cible (publication, commentaire...) peut disparaître : le signalement reste.
        b.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Details).HasMaxLength(Report.MaxDetails);
            e.Property(x => x.Resolution).HasMaxLength(500);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.HasIndex(x => x.ReporterId);
        });
    }
}
