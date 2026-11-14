using BeninConnect.Modules.Voyages.Application;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Infrastructure.Persistence;

public sealed class VoyagesDbContext(DbContextOptions<VoyagesDbContext> options) : ModuleDbContext(options, VoyagesDbContext.SchemaName), IVoyagesDb
{
    public const string SchemaName = "voyages";

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<TripReview> Reviews => Set<TripReview>();

    public DbSet<RouteAlert> Alerts => Set<RouteAlert>();

    public DbSet<TripReport> Reports => Set<TripReport>();

    public DbSet<VoyagesSettings> Settings => Set<VoyagesSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Trip>(e =>
        {
            e.ToTable("trips");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.ReservedKg);
            e.Ignore(x => x.RemainingKg);
            e.Ignore(x => x.IsVisible);
            e.Property(x => x.FromCountry).HasMaxLength(2);
            e.Property(x => x.ToCountry).HasMaxLength(2);
            e.Property(x => x.FromCity).HasMaxLength(80);
            e.Property(x => x.ToCity).HasMaxLength(80);
            e.Property(x => x.FromCityKey).HasMaxLength(80);
            e.Property(x => x.ToCityKey).HasMaxLength(80);
            e.Property(x => x.CapacityKg).HasPrecision(5, 1);
            e.Property(x => x.PricePerKg).HasPrecision(10, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.PrimitiveCollection(x => x.AcceptedCategories).ElementType(t => t.HasConversion<string>());
            e.Property(x => x.DropOffZones).HasMaxLength(300);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.HiddenNote).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasMany(x => x.Bookings).WithOne().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);
            // Recherche : itinéraire et date, trajets ouverts seulement
            e.HasIndex(x => new { x.FromCountry, x.ToCountry, x.DepartureDate }).HasFilter("\"Status\" = 'Open'");
            e.HasIndex(x => new { x.TravelerId, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.DepartureDate });
        });

        b.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.IsActive);
            e.Ignore(x => x.HoldsCapacity);
            e.Ignore(x => x.CodeLocked);
            e.Property(x => x.WeightKg).HasPrecision(5, 1);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DeliveryCodeHash).HasMaxLength(200);
            e.Property(x => x.DeclineReason).HasMaxLength(300);
            e.Property(x => x.DisputeReason).HasMaxLength(500);
            e.Property(x => x.ResolutionNote).HasMaxLength(500);
            e.HasIndex(x => new { x.SenderId, x.CreatedAt });
            e.HasIndex(x => new { x.TravelerId, x.Status });
            e.HasIndex(x => x.Status);
        });

        b.Entity<TripReview>(e =>
        {
            e.ToTable("reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Comment).HasMaxLength(500);
            e.Property(x => x.HiddenReason).HasMaxLength(500);
            e.HasIndex(x => new { x.BookingId, x.AuthorId }).IsUnique();
            e.HasIndex(x => new { x.TargetId, x.CreatedAt });
            e.HasIndex(x => x.TripId);
        });

        b.Entity<RouteAlert>(e =>
        {
            e.ToTable("route_alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FromCountry).HasMaxLength(2);
            e.Property(x => x.ToCountry).HasMaxLength(2);
            e.Property(x => x.FromCity).HasMaxLength(80);
            e.Property(x => x.ToCity).HasMaxLength(80);
            e.Property(x => x.FromCityKey).HasMaxLength(80);
            e.Property(x => x.ToCityKey).HasMaxLength(80);
            e.HasIndex(x => new { x.FromCountry, x.ToCountry });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<VoyagesSettings>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UpdatedBy).HasMaxLength(120);
        });

        b.Entity<TripReport>(e =>
        {
            e.ToTable("reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Decision).HasMaxLength(500);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.AuthorId }).IsUnique();
            e.HasIndex(x => new { x.Handled, x.CreatedAt });
        });
    }
}
