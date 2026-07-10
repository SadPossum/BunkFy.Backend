namespace Reservations.Persistence.Repositories;

using Gma.Framework.Pagination;
using Inventory.Contracts;
using Microsoft.EntityFrameworkCore;
using Reservations.Application.Ports;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

internal sealed class ReservationRepository(ReservationsDbContext dbContext) : IReservationRepository
{
    public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        dbContext.Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<Reservation?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId && reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken);

    public Task<bool> ExternalSourceExistsAsync(
        string sourceSystem,
        string sourceReference,
        CancellationToken cancellationToken) =>
        dbContext.Reservations.AsNoTracking().AnyAsync(
            reservation => reservation.SourceSystem == sourceSystem && reservation.SourceReference == sourceReference,
            cancellationToken);

    public async Task<ReservationListResponse> ListAsync(
        Guid propertyId,
        ReservationStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Reservation> query = dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.RequestedUnits)
            .Where(reservation => reservation.PropertyId == propertyId);
        if (status is { } requestedStatus)
        {
            ReservationState? state = MapStatus(requestedStatus);
            if (state.HasValue)
            {
                query = query.Where(reservation => reservation.Status == state.Value);
            }
        }

        Reservation[] rows = await query
            .OrderByDescending(reservation => reservation.CreatedAtUtc)
            .ThenBy(reservation => reservation.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(rows.Select(Map).ToArray(), pageRequest.Page, pageRequest.PageSize);
    }

    private static ReservationDto Map(Reservation reservation) => new(
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
        MapStatus(reservation.Status),
        reservation.AllocationRequestId,
        reservation.AllocationId,
        reservation.AllocationVersion,
        reservation.AllocationRejection.HasValue
            ? (InventoryAllocationRejectionReason)(int)reservation.AllocationRejection.Value
            : null,
        reservation.Version,
        reservation.CreatedAtUtc,
        reservation.UpdatedAtUtc);

    private static ReservationStatus MapStatus(ReservationState status) => status switch
    {
        ReservationState.PendingAllocation => ReservationStatus.PendingAllocation,
        ReservationState.Confirmed => ReservationStatus.Confirmed,
        ReservationState.AllocationRejected => ReservationStatus.AllocationRejected,
        ReservationState.CancellationPending => ReservationStatus.CancellationPending,
        ReservationState.Cancelled => ReservationStatus.Cancelled,
        _ => ReservationStatus.Unknown
    };

    private static ReservationState? MapStatus(ReservationStatus status) => status switch
    {
        ReservationStatus.PendingAllocation => ReservationState.PendingAllocation,
        ReservationStatus.Confirmed => ReservationState.Confirmed,
        ReservationStatus.AllocationRejected => ReservationState.AllocationRejected,
        ReservationStatus.CancellationPending => ReservationState.CancellationPending,
        ReservationStatus.Cancelled => ReservationState.Cancelled,
        _ => null
    };
}
