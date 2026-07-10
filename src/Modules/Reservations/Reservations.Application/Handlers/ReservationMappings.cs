namespace Reservations.Application.Handlers;

using Inventory.Contracts;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

internal static class ReservationMappings
{
    public static ReservationDto ToDto(this Reservation reservation) => new(
        reservation.Id,
        reservation.PropertyId,
        reservation.Arrival,
        reservation.Departure,
        reservation.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
        reservation.PrimaryGuestName,
        reservation.Email,
        reservation.Phone,
        reservation.GuestCount,
        reservation.Source == ReservationSource.Direct ? ReservationSourceKind.Direct : ReservationSourceKind.External,
        reservation.SourceSystem,
        reservation.SourceReference,
        reservation.Notes,
        reservation.Status switch
        {
            ReservationState.PendingAllocation => ReservationStatus.PendingAllocation,
            ReservationState.Confirmed => ReservationStatus.Confirmed,
            ReservationState.AllocationRejected => ReservationStatus.AllocationRejected,
            ReservationState.CancellationPending => ReservationStatus.CancellationPending,
            ReservationState.Cancelled => ReservationStatus.Cancelled,
            _ => ReservationStatus.Unknown
        },
        reservation.AllocationRequestId,
        reservation.AllocationId,
        reservation.AllocationVersion,
        reservation.AllocationRejection.HasValue
            ? (InventoryAllocationRejectionReason)(int)reservation.AllocationRejection.Value
            : null,
        reservation.Version,
        reservation.CreatedAtUtc,
        reservation.UpdatedAtUtc);
}
