namespace BunkFy.Modules.Guests.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Events;

internal sealed class GuestProfileCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<GuestProfileCreatedDomainEvent>
{
    public Task HandleAsync(GuestProfileCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(GuestsModuleMetadata.Name).EnqueueAsync(
            new GuestProfileCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.GuestId,
                domainEvent.OriginPropertyId,
                GuestProfileMappings.MapStatus(domainEvent.Status),
                domainEvent.GuestVersion),
            cancellationToken);
}

internal sealed class GuestProfileUpdatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<GuestProfileUpdatedDomainEvent>
{
    public Task HandleAsync(GuestProfileUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(GuestsModuleMetadata.Name).EnqueueAsync(
            new GuestProfileUpdatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.GuestId,
                GuestProfileMappings.MapStatus(domainEvent.Status),
                domainEvent.GuestVersion),
            cancellationToken);
}

internal sealed class GuestProfileArchivedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<GuestProfileArchivedDomainEvent>
{
    public Task HandleAsync(GuestProfileArchivedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(GuestsModuleMetadata.Name).EnqueueAsync(
            new GuestProfileArchivedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.GuestId,
                domainEvent.GuestVersion),
            cancellationToken);
}
