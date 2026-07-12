namespace BunkFy.Modules.Guests.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;

[IntegrationEventHandler(GuestsModuleMetadata.ReservationGuestLinkedHandlerName)]
internal sealed class ReservationGuestLinkedStayHandler(IGuestStayHistoryRepository stays)
    : IIntegrationEventHandler<ReservationGuestLinkedIntegrationEvent>
{
    public Task HandleAsync(ReservationGuestLinkedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        stays.ApplyAsync(Map(integrationEvent), cancellationToken);

    private static GuestStayHistoryWriteModel Map(ReservationGuestLinkedIntegrationEvent integrationEvent) => new(
        integrationEvent.ScopeId,
        integrationEvent.GuestId,
        integrationEvent.ReservationId,
        integrationEvent.PropertyId,
        integrationEvent.Role,
        integrationEvent.Arrival,
        integrationEvent.Departure,
        integrationEvent.Status,
        integrationEvent.CheckedInBusinessDate,
        integrationEvent.NoShowBusinessDate,
        integrationEvent.CheckedOutBusinessDate,
        IsCurrentParticipant: true,
        ReservationVersion: integrationEvent.ReservationVersion);
}

[IntegrationEventHandler(GuestsModuleMetadata.ReservationGuestStayChangedHandlerName)]
internal sealed class ReservationGuestStayChangedHandler(IGuestStayHistoryRepository stays)
    : IIntegrationEventHandler<ReservationGuestStayChangedIntegrationEvent>
{
    public Task HandleAsync(ReservationGuestStayChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        stays.ApplyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.GuestId,
                integrationEvent.ReservationId,
                integrationEvent.PropertyId,
                integrationEvent.Role,
                integrationEvent.Arrival,
                integrationEvent.Departure,
                integrationEvent.Status,
                integrationEvent.CheckedInBusinessDate,
                integrationEvent.NoShowBusinessDate,
                integrationEvent.CheckedOutBusinessDate,
                integrationEvent.IsCurrentParticipant,
                integrationEvent.ReservationVersion),
            cancellationToken);
}
