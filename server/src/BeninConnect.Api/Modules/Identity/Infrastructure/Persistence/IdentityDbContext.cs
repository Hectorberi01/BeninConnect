using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options, IdentityDbContext.SchemaName), IIdentityDb
{
    public const string SchemaName = "identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.FirstName).HasMaxLength(40);
            e.Property(x => x.LastName).HasMaxLength(40);
            e.Property(x => x.Bio).HasMaxLength(300);
            e.Property(x => x.AvatarColor).HasMaxLength(9);
            e.Property(x => x.AvatarImage).HasMaxLength(200);
            e.Property(x => x.CoverImage).HasMaxLength(200);
            e.Property(x => x.VerificationCode).HasMaxLength(6);
            e.Property(x => x.PasswordResetCode).HasMaxLength(6);
            e.Property(x => x.SuspensionReason).HasMaxLength(300);
            e.Ignore(x => x.DisplayName);
        });
    }
}
