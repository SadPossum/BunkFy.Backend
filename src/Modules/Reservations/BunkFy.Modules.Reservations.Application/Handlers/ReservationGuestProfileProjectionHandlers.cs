namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(ReservationsModuleMetadata.GuestCreatedHandlerName)]
internal sealed class GuestProfileCreatedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileCreatedIntegrationEvent>
{
    public async Task HandleAsync(
        GuestProfileCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, integrationEvent.OriginPropertyId, integrationEvent.Status, integrationEvent.GuestVersion),
            cancellationToken).ConfigureAwait(false);
        await profiles.ApplyRestrictionAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.OriginPropertyId,
                integrationEvent.GuestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 0,
                IsRestricted: false),
            cancellationToken).ConfigureAwait(false);
    }
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

[IntegrationEventHandler(ReservationsModuleMetadata.GuestRestrictionChangedHandlerName)]
internal sealed class GuestProcessingRestrictionChangedProjectionHandler(
    IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProcessingRestrictionChangedIntegrationEvent>
{
    public Task HandleAsync(
        GuestProcessingRestrictionChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        profiles.ApplyRestrictionAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.GuestId,
                integrationEvent.ContractVersion,
                integrationEvent.ProjectionRevision,
                integrationEvent.IsRestricted),
            cancellationToken);
}
