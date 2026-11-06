using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Shared.Infrastructure;

/// <summary>Base de données d'un module, migrée au démarrage (dans l'ordre d'enregistrement).</summary>
public sealed record ModuleDatabase(string Module, Type ContextType);

public static class ModuleRegistration
{
    /// <summary>
    /// ConnectionStrings:{module} si présente (base séparée, utile au découpage), sinon ConnectionStrings:Database.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration config, string module, string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = config.GetConnectionString(module) ?? config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database manquante.");

        services.AddDbContext<TContext>(o => o.UseNpgsql(connectionString, n => n
            .MigrationsHistoryTable("__EFMigrationsHistory", schema)
            .EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null))); // base distante : coupures passagères
        services.AddSingleton(new ModuleDatabase(module, typeof(TContext)));
        return services;
    }

    /// <summary>Handlers CQRS et abonnés aux événements dont l'espace de noms commence par le préfixe du module.</summary>
    public static IServiceCollection AddModuleHandlers(this IServiceCollection services, string namespacePrefix)
    {
        var contracts = new[] { typeof(IRequestHandler<,>), typeof(IIntegrationEventHandler<>) };
        var types = typeof(ModuleRegistration).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true);

        foreach (var type in types)
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && contracts.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }
}
