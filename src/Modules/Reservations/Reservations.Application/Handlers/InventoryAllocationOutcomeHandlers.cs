namespace Reservations.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Inventory.Contracts;
using Reservations.Application.Ports;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationConfirmedHandlerName)]
internal sealed class InventoryAllocationConfirmedHandler(
    IReservationRepository reservations,
    IInventoryProjectionRepository projection,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationConfirmedIntegrationEvent>
{
    public async Task HandleAsync(InventoryAllocationConfirmedIntegrationEvent outcome, CancellationToken cancellationToken)
    {
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

        Reservation? reservation = await reservations
            .GetAsync(outcome.PropertyId, outcome.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for Inventory allocation confirmation.");
        }

        long version = reservation.Version;
        if (reservation.ConfirmAllocation(
                outcome.AllocationRequestId,
                outcome.AllocationId,
                outcome.AllocationVersion,
                idGenerator.NewId(),
                clock.UtcNow).IsFailure || reservation.Version == version)
        {
            return;
        }

        if (reservation.Status != ReservationState.Confirmed)
        {
            return;
        }

        await outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationConfirmedIntegrationEvent(
                idGenerator.NewId(),
                reservation.ScopeId,
                clock.UtcNow,
                reservation.Id,
                reservation.PropertyId,
                outcome.AllocationId,
                reservation.Version),
            cancellationToken).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationRejectedHandlerName)]
internal sealed class InventoryAllocationRejectedHandler(
    IReservationRepository reservations,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationRejectedIntegrationEvent>
{
    public async Task HandleAsync(InventoryAllocationRejectedIntegrationEvent outcome, CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations
            .GetAsync(outcome.PropertyId, outcome.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for Inventory allocation rejection.");
        }

        long version = reservation.Version;
        if (reservation.RejectAllocation(
                outcome.AllocationRequestId,
                (ReservationAllocationRejection)(int)outcome.Reason,
                idGenerator.NewId(),
                clock.UtcNow).IsFailure || reservation.Version == version)
        {
            return;
        }

        if (reservation.Status != ReservationState.AllocationRejected)
        {
            return;
        }

        await outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationAllocationRejectedIntegrationEvent(
                idGenerator.NewId(),
                reservation.ScopeId,
                clock.UtcNow,
                reservation.Id,
                reservation.PropertyId,
                outcome.Reason,
                reservation.Version),
            cancellationToken).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationReleasedHandlerName)]
internal sealed class InventoryAllocationReleasedHandler(
    IReservationRepository reservations,
    IInventoryProjectionRepository projection,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationReleasedIntegrationEvent>
{
    public async Task HandleAsync(InventoryAllocationReleasedIntegrationEvent outcome, CancellationToken cancellationToken)
    {
        await projection.ReleaseAllocationAsync(
            outcome.ScopeId,
            outcome.AllocationId,
            outcome.ReservationId,
            outcome.AllocationVersion,
            cancellationToken).ConfigureAwait(false);

        Reservation? reservation = await reservations
            .GetAsyncByReservationId(outcome.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for Inventory allocation release.");
        }

        long version = reservation.Version;
        if (reservation.CompleteCancellation(outcome.ReleaseRequestId, clock.UtcNow).IsFailure ||
            reservation.Version == version)
        {
            return;
        }

        await outboxWriters.GetRequired(ReservationsModuleMetadata.Name).EnqueueAsync(
            new ReservationCancelledIntegrationEvent(
                idGenerator.NewId(),
                reservation.ScopeId,
                clock.UtcNow,
                reservation.Id,
                reservation.PropertyId,
                reservation.Version),
            cancellationToken).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.AllocationReleaseRejectedHandlerName)]
internal sealed class InventoryAllocationReleaseRejectedHandler(
    IReservationRepository reservations,
    ISystemClock clock)
    : IIntegrationEventHandler<InventoryAllocationReleaseRejectedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationReleaseRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations
            .GetAsyncByReservationId(outcome.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation '{outcome.ReservationId}' was not found for Inventory allocation release rejection.");
        }

        reservation.RestoreAfterReleaseRejection(outcome.ReleaseRequestId, (int)outcome.Reason, clock.UtcNow);
    }
}
