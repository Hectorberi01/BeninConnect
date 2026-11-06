using BeninConnect.Modules.Events.Application;
using BeninConnect.Modules.Events.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Infrastructure.Persistence;

public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : ModuleDbContext(options, EventsDbContext.SchemaName), IEventsDb
{
    public const string SchemaName = "events";

    public DbSet<CommunityEvent> Events => Set<CommunityEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<CommunityEvent>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.StartsAt);
            e.HasIndex(x => x.CommunityId);
            e.Ignore(x => x.IsFull);
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<EventParticipant>(e =>
        {
            e.ToTable("participants");
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasIndex(x => x.UserId);
        });
    }
}
