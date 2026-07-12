namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class BedRetiredOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<BedRetiredDomainEvent>
{
    public Task HandleAsync(BedRetiredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedRetiredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.BedId,
                domainEvent.RoomVersion,
                domainEvent.BedVersion),
            cancellationToken);
}
