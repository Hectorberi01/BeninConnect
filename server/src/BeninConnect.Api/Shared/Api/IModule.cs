using Microsoft.AspNetCore.Routing;

namespace BeninConnect.Shared.Api;

/// <summary>
/// Point d'entrée d'un module :
/// ses services puis ses routes. L'hôte ne connaît que cette interface.
/// </summary>
public interface IModule
{
    string Name { get; }

    void AddServices(WebApplicationBuilder builder);

    void MapEndpoints(IEndpointRouteBuilder app);
}

/// <summary>
/// Module qui ajoute un middleware au pipeline HTTP (avant l'authentification).
/// </summary>
public interface IModulePipeline
{
    void UsePipeline(WebApplication app);
}
