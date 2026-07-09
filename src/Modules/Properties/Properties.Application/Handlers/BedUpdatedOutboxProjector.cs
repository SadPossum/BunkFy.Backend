namespace Properties.Application.Handlers;

using Properties.Application.Mapping;
using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class BedUpdatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<BedUpdatedDomainEvent>
{
    public Task HandleAsync(BedUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedUpdatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.BedId,
                domainEvent.Label,
                PropertiesMapper.MapStatus(domainEvent.Status)),
            cancellationToken);
}
