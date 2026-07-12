namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class RoomRetiredOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<RoomRetiredDomainEvent>
{
    public Task HandleAsync(RoomRetiredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new RoomRetiredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.RoomId,
                domainEvent.RoomVersion),
            cancellationToken);
}
