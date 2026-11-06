namespace BeninConnect.Shared.Application;

/// <summary>Fait publié par un module après enregistrement (contrat public, dans Modules/X/Contracts).</summary>
public interface IIntegrationEvent;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken ct);
}

/// <summary>Bus en mémoire aujourd'hui ; à remplacer par un broker (RabbitMQ, Service Bus) lors du découpage.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent;
}
