using BeninConnect.Modules.Notifications.Application;
using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : ModuleDbContext(options, NotificationsDbContext.SchemaName), INotificationsDb, IModuleStatistics
{
    public const string SchemaName = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Notifications", await Notifications.CountAsync(ct)),
        new("Téléphones (push)", await PushDevices.CountAsync(ct))
    ];

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        b.Entity<PushDevice>(e =>
        {
            e.ToTable("push_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Token).HasMaxLength(PushDevice.MaxTokenLength);
            e.Property(x => x.Platform).HasMaxLength(16);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
        });
    }
}
