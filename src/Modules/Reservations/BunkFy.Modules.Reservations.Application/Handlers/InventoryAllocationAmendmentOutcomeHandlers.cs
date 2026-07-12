namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.External;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationAmendmentConfirmedHandlerName)]
internal sealed class InventoryAllocationAmendmentConfirmedHandler(
    IReservationRepository reservations,
    IInventoryProjectionRepository projection,
    ExternalReservationOperationCoordinator coordinator,
    ReservationInboxDomainEventDispatcher domainEvents,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationAmendmentConfirmedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationAmendmentConfirmedIntegrationEvent outcome,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            outcome.PropertyId,
            outcome.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for allocation amendment confirmation.");
        }

        if (reservation.PendingAllocationAmendmentId != outcome.AmendmentRequestId)
        {
            return;
        }

        Guid? externalOperationId = reservation.PendingDetailsExternalOperationId;
        Guid? connectionId = reservation.PendingDetailsAdapterConnectionId;
        Guid? receiptId = reservation.PendingDetailsCorrelationId;
        string? fingerprint = reservation.PendingAllocationAmendmentRequestFingerprint;
        await projection.ApplyAllocationAsync(
            new(
                outcome.ScopeId,
                outcome.AllocationId,
                outcome.ReservationId,
                outcome.PropertyId,
                outcome.Arrival,
                outcome.Departure,
                InventoryAllocationStatus.Active,
                outcome.InventoryUnitIds,
                outcome.AllocationVersion),
            cancellationToken).ConfigureAwait(false);
        if (reservation.CompleteAllocationAmendment(
                outcome.AmendmentRequestId,
                outcome.AllocationId,
                outcome.Arrival,
                outcome.Departure,
                outcome.InventoryUnitIds,
                outcome.AllocationVersion,
                idGenerator.NewId(),
                clock.UtcNow).IsFailure)
        {
            throw new InvalidOperationException("The confirmed allocation amendment did not match Reservations state.");
        }

        await domainEvents.DispatchAsync(reservation, cancellationToken).ConfigureAwait(false);

        if (externalOperationId.HasValue && connectionId.HasValue && receiptId.HasValue && fingerprint is not null)
        {
            await coordinator.CompleteAsync(
                new ExternalReservationOperationContext(
                    externalOperationId.Value,
                    reservation.ScopeId,
                    receiptId.Value,
                    connectionId.Value,
                    reservation.PropertyId),
                ExternalReservationOperationKind.Amend,
                fingerprint,
                ExternalReservationOperationOutcome.Applied,
                reservation.Id,
                reservation.DetailsRevision,
                reservation.Version,
                errorCode: null,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationAmendmentRejectedHandlerName)]
internal sealed class InventoryAllocationAmendmentRejectedHandler(
    IReservationRepository reservations,
    ExternalReservationOperationCoordinator coordinator,
    ISystemClock clock)
    : IIntegrationEventHandler<InventoryAllocationAmendmentRejectedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationAmendmentRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            outcome.PropertyId,
            outcome.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for allocation amendment rejection.");
        }

        if (reservation.PendingAllocationAmendmentId != outcome.AmendmentRequestId)
        {
            return;
        }

        Guid? externalOperationId = reservation.PendingDetailsExternalOperationId;
        Guid? connectionId = reservation.PendingDetailsAdapterConnectionId;
        Guid? receiptId = reservation.PendingDetailsCorrelationId;
        string? fingerprint = reservation.PendingAllocationAmendmentRequestFingerprint;
        if (reservation.RejectAllocationAmendment(
                outcome.AmendmentRequestId,
                outcome.AllocationId,
                (int)outcome.Reason,
                clock.UtcNow).IsFailure)
        {
            return;
        }

        if (externalOperationId.HasValue && connectionId.HasValue && receiptId.HasValue && fingerprint is not null)
        {
            await coordinator.CompleteAsync(
                new ExternalReservationOperationContext(
                    externalOperationId.Value,
                    reservation.ScopeId,
                    receiptId.Value,
                    connectionId.Value,
                    reservation.PropertyId),
                ExternalReservationOperationKind.Amend,
                fingerprint,
                ExternalReservationOperationOutcome.ValidationRejected,
                reservation.Id,
                reservation.DetailsRevision,
                reservation.Version,
                $"Inventory.AllocationAmendment.{outcome.Reason}",
                cancellationToken).ConfigureAwait(false);
        }
    }
}
