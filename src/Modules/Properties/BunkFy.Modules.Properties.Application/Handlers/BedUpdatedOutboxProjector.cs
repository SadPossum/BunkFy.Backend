namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class BedUpdatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<BedUpdatedDomainEvent>
{
    public Task HandleAsync(BedUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedUpdatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.BedId,
                domainEvent.Label,
                PropertiesMapper.MapStatus(domainEvent.Status),
                domainEvent.RoomVersion,
                domainEvent.BedVersion),
            cancellationToken);
}
