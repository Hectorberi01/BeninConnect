using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Shared.Infrastructure;

/// <summary>DbContext d'un module : schéma PostgreSQL dédié, aucune clé étrangère vers un autre module.</summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    public string Schema { get; } = schema;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums en texte : lisibles en base et stables si l'ordre change.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b) => b.HasDefaultSchema(Schema);
}
