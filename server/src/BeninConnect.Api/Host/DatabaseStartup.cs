using BeninConnect.Host.DemoData;
using BeninConnect.Modules.Administration.Infrastructure;
using BeninConnect.Modules.Identity.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Host;

/// <summary>Migrations de chaque module (un schéma PostgreSQL chacun), jeu de démonstration, premier SuperAdmin.</summary>
public static class DatabaseStartup
{
    public static async Task InitializeAsync(IServiceProvider root, IConfiguration config, CancellationToken ct = default)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        await MigrateAllAsync(services, ct);

        if (config.GetValue("Database:SeedDemoData", true) && !await services.GetRequiredService<IdentityDbContext>().Users.AnyAsync(ct))
        {
            await services.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
        }

        await AdminBootstrap.EnsureAsync(services, config, ct);
    }

    public static async Task MigrateAllAsync(IServiceProvider services, CancellationToken ct)
    {
        foreach (var module in services.GetServices<ModuleDatabase>())
        {
            var db = (DbContext)services.GetRequiredService(module.ContextType);
            if (!db.Database.GetMigrations().Any())
            {
                throw new InvalidOperationException($"Aucune migration EF pour le module {module.Module} : lancez tools/ef-migrations.sh.");
            }

            await db.Database.MigrateAsync(ct);
        }
    }
}

/// <summary>Développement : base effacée, migrée puis remplie avec le jeu de démonstration.</summary>
public sealed class DemoDataReset(IServiceProvider services, IEnumerable<ModuleDatabase> databases, DemoDataSeeder seeder) : IDemoDataReset
{
    public async Task ResetAsync(CancellationToken ct)
    {
        var first = (DbContext)services.GetRequiredService(databases.First().ContextType);
        await first.Database.EnsureDeletedAsync(ct);
        await DatabaseStartup.MigrateAllAsync(services, ct);
        await seeder.SeedAsync(ct);
    }
}
