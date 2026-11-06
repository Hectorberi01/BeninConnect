using BeninConnect.Shared.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Shared.Infrastructure;

/// <summary>Livre l'événement aux modules abonnés, dans la même requête ; un abonné en échec ne bloque pas l'émetteur.</summary>
public sealed class InMemoryEventBus(IServiceProvider services, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            try
            {
                await handler.Handle(integrationEvent, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Échec de {Handler} pour {Event}", handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
