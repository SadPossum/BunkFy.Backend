namespace Properties.Application.Handlers;

using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class PropertyRetiredOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyRetiredDomainEvent>
{
    public Task HandleAsync(PropertyRetiredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyRetiredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.PropertyVersion),
            cancellationToken);
}
