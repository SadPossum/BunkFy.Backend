namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
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
                domainEvent.PropertyVersion,
                domainEvent.ActorId),
            cancellationToken);
}
