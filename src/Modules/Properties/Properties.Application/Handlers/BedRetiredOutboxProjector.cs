namespace Properties.Application.Handlers;

using Properties.Contracts;
using Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class BedRetiredOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<BedRetiredDomainEvent>
{
    public Task HandleAsync(BedRetiredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedRetiredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.BedId),
            cancellationToken);
}
