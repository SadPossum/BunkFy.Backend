namespace Properties.Application.Handlers;

using Properties.Application.Mapping;
using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class RoomCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<RoomCreatedDomainEvent>
{
    public Task HandleAsync(RoomCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new RoomCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.Name,
                domainEvent.BuildingLabel,
                domainEvent.FloorLabel,
                PropertiesMapper.MapStatus(domainEvent.Status),
                domainEvent.RoomVersion),
            cancellationToken);
}
