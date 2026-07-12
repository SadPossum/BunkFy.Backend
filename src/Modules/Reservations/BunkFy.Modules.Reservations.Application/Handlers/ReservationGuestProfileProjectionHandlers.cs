namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;

[IntegrationEventHandler(ReservationsModuleMetadata.GuestCreatedHandlerName)]
internal sealed class GuestProfileCreatedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileCreatedIntegrationEvent>
{
    public Task HandleAsync(GuestProfileCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, integrationEvent.OriginPropertyId, integrationEvent.Status, integrationEvent.GuestVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestUpdatedHandlerName)]
internal sealed class GuestProfileUpdatedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileUpdatedIntegrationEvent>
{
    public Task HandleAsync(GuestProfileUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, null, integrationEvent.Status, integrationEvent.GuestVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestArchivedHandlerName)]
internal sealed class GuestProfileArchivedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileArchivedIntegrationEvent>
{
    public Task HandleAsync(GuestProfileArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, null, GuestStatus.Archived, integrationEvent.GuestVersion),
            cancellationToken);
}
