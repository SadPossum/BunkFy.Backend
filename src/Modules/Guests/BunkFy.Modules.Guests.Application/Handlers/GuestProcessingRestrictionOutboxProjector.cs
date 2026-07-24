namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class GuestProcessingRestrictionOutboxProjector(
    IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<GuestProcessingRestrictionChangedDomainEvent>
{
    public Task HandleAsync(
        GuestProcessingRestrictionChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(GuestsModuleMetadata.Name).EnqueueAsync(
            new GuestProcessingRestrictionChangedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                domainEvent.GuestId,
                domainEvent.ContractVersion,
                domainEvent.ProjectionRevision,
                domainEvent.IsRestricted),
            cancellationToken);
}
